using System.Diagnostics;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;

namespace TravelfinderAPI.Host;

public sealed class PlanOrchestrator
{
    private readonly IPlanner _planner;
    private readonly IRenderer _renderer;
    private readonly IPlaceService _placeService;
    private readonly ILogger<PlanOrchestrator> _logger;
    private readonly IModelFailover? _failover;
    private readonly TimeSpan _operationTimeout;

    public PlanOrchestrator(
        IPlanner planner,
        IRenderer renderer,
        IPlaceService placeService,
        ILogger<PlanOrchestrator> logger,
        IModelFailover? failover = null,
        IConfiguration? configuration = null,
        TimeSpan? operationTimeout = null)
    {
        _planner = planner;
        _renderer = renderer;
        _placeService = placeService;
        _logger = logger;
        _failover = failover;
        _operationTimeout = operationTimeout
            ?? TimeSpan.FromSeconds(configuration?.GetValue("Planning:StreamDeadlineSeconds", 45) ?? 45);
    }

    public async Task RunAsync(PlanRequest request, ISseWriter writer, CancellationToken clientCancellationToken)
    {
        var events = new List<string>();
        string? errorCode = null;
        long planMs = 0;
        long gisMs = 0;
        long renderMs = 0;
        int googleCount = 0;
        int arcgisCount = 0;
        int cacheHits = 0;
        var requestId = request.RequestId;

        try
        {
            var validationError = PlanRequestValidator.TryValidate(request);
            if (validationError is not null)
            {
                errorCode = validationError.Code;
                await writer.WriteAsync(PlanEventNames.Error, validationError, clientCancellationToken);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, clientCancellationToken);
                events.Add(PlanEventNames.Done);
                return;
            }

            using var timeoutCts = new CancellationTokenSource(_operationTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(clientCancellationToken, timeoutCts.Token);
            var cancellationToken = linkedCts.Token;

            var language = string.IsNullOrWhiteSpace(request.Language) ? "en-us" : request.Language!;

            PlannerOutcome outcome;
            var planStopwatch = Stopwatch.StartNew();
            try
            {
                outcome = await _planner.PlanAsync(
                    request.Messages,
                    request.Latitude,
                    request.Longitude,
                    language,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                errorCode = PlanErrorCode.Timeout;
                await WriteErrorAsync(writer, requestId, PlanErrorCode.Timeout, "The request timed out.", retryable: true, CancellationToken.None);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, CancellationToken.None);
                events.Add(PlanEventNames.Done);
                return;
            }
            catch (ModelUnavailableException)
            {
                errorCode = PlanErrorCode.ModelUnavailable;
                await WriteErrorAsync(writer, requestId, PlanErrorCode.ModelUnavailable, "The planning service is temporarily unavailable.", retryable: true, cancellationToken);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, cancellationToken);
                events.Add(PlanEventNames.Done);
                return;
            }
            finally
            {
                planMs = planStopwatch.ElapsedMilliseconds;
            }

            if (outcome.IsClarification)
            {
                await writer.WriteAsync(
                    PlanEventNames.Clarification,
                    new ClarificationPayload { Message = outcome.Clarification! },
                    cancellationToken);
                events.Add(PlanEventNames.Clarification);
                await WriteDoneAsync(writer, cancellationToken);
                events.Add(PlanEventNames.Done);
                return;
            }

            if (outcome.Spec is null)
            {
                throw new InvalidOperationException("Planner returned an empty outcome.");
            }

            var spec = outcome.Spec;
            spec.ClampRadius();
            await writer.WriteAsync(PlanEventNames.PlanSpec, spec, cancellationToken);
            events.Add(PlanEventNames.PlanSpec);

            MergedPlacesResult merged;
            var gisStopwatch = Stopwatch.StartNew();
            try
            {
                merged = await _placeService.GetMergedPlaces(
                    spec,
                    new GeoPoint(request.Latitude, request.Longitude),
                    language,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                errorCode = PlanErrorCode.Timeout;
                await WriteErrorAsync(writer, requestId, PlanErrorCode.Timeout, "The request timed out.", retryable: true, CancellationToken.None);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, CancellationToken.None);
                events.Add(PlanEventNames.Done);
                return;
            }
            finally
            {
                gisMs = gisStopwatch.ElapsedMilliseconds;
            }

            googleCount = merged.GoogleCount;
            arcgisCount = merged.ArcgisCount;
            cacheHits = merged.CacheHits;

            if (merged.Places.Count == 0)
            {
                errorCode = PlanErrorCode.NoPlaces;
                await WriteErrorAsync(writer, requestId, PlanErrorCode.NoPlaces, "No places matched your search. Try a wider radius or fewer filters.", retryable: true, cancellationToken);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, cancellationToken);
                events.Add(PlanEventNames.Done);
                return;
            }

            await writer.WriteAsync(
                PlanEventNames.Places,
                new PlacesPayload { Places = merged.Places.ToArray() },
                cancellationToken);
            events.Add(PlanEventNames.Places);

            var renderStopwatch = Stopwatch.StartNew();
            try
            {
                await foreach (var stop in _renderer.RenderAsync(spec, merged.Places, cancellationToken))
                {
                    await writer.WriteAsync(PlanEventNames.PlanDelta, stop, cancellationToken);
                    events.Add(PlanEventNames.PlanDelta);
                }
            }
            catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                errorCode = PlanErrorCode.Timeout;
                await WriteErrorAsync(writer, requestId, PlanErrorCode.Timeout, "The request timed out.", retryable: true, CancellationToken.None);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, CancellationToken.None);
                events.Add(PlanEventNames.Done);
                return;
            }
            finally
            {
                renderMs = renderStopwatch.ElapsedMilliseconds;
            }

            await WriteDoneAsync(writer, cancellationToken);
            events.Add(PlanEventNames.Done);
        }
        catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            errorCode = PlanErrorCode.Internal;
            _logger.LogError(ex, "Planning stream failed for {RequestId}", requestId);
            try
            {
                await WriteErrorAsync(writer, requestId, PlanErrorCode.Internal, "Something went wrong.", retryable: false, clientCancellationToken);
                events.Add(PlanEventNames.Error);
                await WriteDoneAsync(writer, clientCancellationToken);
                events.Add(PlanEventNames.Done);
            }
            catch (OperationCanceledException) when (clientCancellationToken.IsCancellationRequested)
            {
            }
        }
        finally
        {
            _logger.LogInformation(
                "Plan stream completed requestId={RequestId} planMs={PlanMs} gisMs={GisMs} renderMs={RenderMs} providerUsed={ProviderUsed} googleCount={GoogleCount} arcgisCount={ArcgisCount} cacheHits={CacheHits} events={Events} errorCode={ErrorCode}",
                requestId,
                planMs,
                gisMs,
                renderMs,
                _failover?.LastProviderUsed,
                googleCount,
                arcgisCount,
                cacheHits,
                string.Join(",", events),
                errorCode);
        }
    }

    private async Task WriteDoneAsync(ISseWriter writer, CancellationToken cancellationToken)
    {
        var providerUsed = _failover?.LastProviderUsed;
        var payload = providerUsed is null
            ? new DonePayload()
            : new DonePayload { ProviderUsed = providerUsed };
        await writer.WriteAsync(PlanEventNames.Done, payload, cancellationToken);
    }

    private static Task WriteErrorAsync(
        ISseWriter writer,
        string requestId,
        string code,
        string message,
        bool retryable,
        CancellationToken cancellationToken) =>
        writer.WriteAsync(
            PlanEventNames.Error,
            new ErrorPayload
            {
                Code = code,
                Message = message,
                RequestId = requestId,
                Retryable = retryable
            },
            cancellationToken);
}
