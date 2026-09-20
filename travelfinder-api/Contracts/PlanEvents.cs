namespace TravelfinderAPI.Contracts;

public static class PlanEventNames
{
    public const string Clarification = "clarification";
    public const string PlanSpec = "plan_spec";
    public const string Places = "places";
    public const string PlanDelta = "plan_delta";
    public const string Error = "error";
    public const string Done = "done";
}

public static class PlanErrorCode
{
    public const string Validation = "validation";
    public const string NoPlaces = "no_places";
    public const string ModelUnavailable = "model_unavailable";
    public const string Timeout = "timeout";
    public const string Internal = "internal";
}

public sealed class ClarificationPayload
{
    public required string Message { get; init; }
}

public sealed class PlacesPayload
{
    public required Place[] Places { get; init; }
}

public sealed class ErrorPayload
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string RequestId { get; init; }
    public required bool Retryable { get; init; }
}

public sealed class DonePayload
{
    public string? ProviderUsed { get; init; }
}
