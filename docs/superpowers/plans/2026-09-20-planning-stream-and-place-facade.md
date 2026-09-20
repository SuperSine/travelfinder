# Planning Stream and Place Facade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconstruct Travelfinder's planning loop so the Angular UI talks only to `POST /plan/stream`, GIS is a single `IPlaceService` facade, and LLM calls run through Microsoft Agent Framework with Azure primary / xAI failover.

**Architecture:** In-place strangler of the existing monorepo. Keep `travelfinder-api` and `travelfinder-web` as the two processes. Freeze `ChatController` and the old `*ApiClient` types. New traffic goes Host → Agents → Facade → Providers. Frontend is Angular 17 + Tailwind; Ionic and UI5 leave every shipped page.

**Tech Stack:** ASP.NET Core 8, xUnit, `Microsoft.Agents.AI` + `Microsoft.Extensions.AI` (`IChatClient`), `IHttpClientFactory`, `IMemoryCache`, Angular 17 standalone, Tailwind 3, Jasmine/Karma, `@arcgis/core` 4.29.

**Spec:** `docs/superpowers/specs/2026-09-20-travelfinder-architecture-design.md`

## Global Constraints

- Target framework stays `net8.0`. Do not upgrade to .NET 9/10.
- New JSON is `System.Text.Json` camelCase. Do not send `systemId` or place JSON from the client.
- Business / agent code must not reference `AzureAIApiClient`, `xAIApiClient`, `OpenAIApiClient`, `GmpGisApiClient`, or `ArcGisApiClient`.
- New UI calls only `POST /plan/stream`. Leave `/Chat/*` compiling and unused by new pages.
- No live Google or ArcGIS HTTP in CI. Providers are tested with fake handlers.
- Do not assert LLM prose quality. Assert structure, event order, and tool/call counts.
- Radius clamp is `[500, 20000]`. Default search radius is `5000`. `dayCount` defaults to `1`. Language default is `en-us`.
- Provider timeout is 5 seconds each. Whole-stream deadline is 45 seconds.
- Per-source cap 20. Merged hard cap 40. Cache TTL: 10 minutes Nearby/Text, 24 hours reverse geocode.
- Shipped page DOM must contain no `ion-` or `ui5-` elements. No Angular Material. Capacitor packages may stay in `package.json`.
- Auth, saved itineraries, sharing, `ApplyEdits`, Capacitor packaging, basemap vendor change, and multi-agent graphs beyond Planner + Renderer are out of scope.
- `ENABLE_PROXY` remains the env switch. `HttpClient` is a singleton per provider via `IHttpClientFactory`. No `new HttpClient()` in new GIS code.

---

## File Structure

### Backend (create)

| File | Responsibility |
|---|---|
| `travelfinder-api/Contracts/GeoPoint.cs` | Lat/lng value object |
| `travelfinder-api/Contracts/Place.cs` | Unified place (`source:sourceId`) |
| `travelfinder-api/Contracts/PlanSpec.cs` | Planner output; no invented places |
| `travelfinder-api/Contracts/ChatMessageDto.cs` | `{ role, content }` |
| `travelfinder-api/Contracts/PlanRequest.cs` | `POST /plan/stream` body |
| `travelfinder-api/Contracts/ItineraryStop.cs` | Renderer patch (`dayIndex` + `stopIndex`) |
| `travelfinder-api/Contracts/PlanEvents.cs` | SSE payload types + error codes |
| `travelfinder-api/Gis/GeoMath.cs` | Haversine meters + name normalize |
| `travelfinder-api/Gis/Geohash.cs` | Precision-7/8 encode for cache keys |
| `travelfinder-api/Gis/AllowedGoogleTypes.cs` | Clip Nearby types to the allowed set |
| `travelfinder-api/Gis/PlaceDedupe.cs` | Same-id and name+80 m collapse |
| `travelfinder-api/Gis/PlaceScoring.cs` | Weighted score 0–1 |
| `travelfinder-api/Gis/PlaceCacheKeys.cs` | Nearby / text / reverse key + TTL |
| `travelfinder-api/Gis/IPlaceProvider.cs` | Provider port |
| `travelfinder-api/Gis/IPlaceService.cs` | Facade port (`SearchText`, `Nearby`, `ReverseGeocode`, `GetMergedPlaces`) |
| `travelfinder-api/Gis/Providers/GooglePlaceProvider.cs` | Text Search, Nearby, Geocode |
| `travelfinder-api/Gis/Providers/ArcGisPlaceProvider.cs` | Read-only Feature Layer query |
| `travelfinder-api/Gis/PlaceService.cs` | Fan-out, cache, merge, score |
| `travelfinder-api/Agents/PlannerOutcome.cs` | Spec or clarification |
| `travelfinder-api/Agents/IPlanner.cs` | Planner port |
| `travelfinder-api/Agents/IRenderer.cs` | Renderer port |
| `travelfinder-api/Agents/IModelFailover.cs` | `LastProviderUsed` |
| `travelfinder-api/Agents/FailoverChatClient.cs` | Azure → one xAI retry on timeout/429/5xx |
| `travelfinder-api/Agents/PlannerAgent.cs` | MAF planner; no GIS loop |
| `travelfinder-api/Agents/RendererAgent.cs` | MAF renderer; stops only from `Place[]` |
| `travelfinder-api/Host/PlanJson.cs` | Shared camelCase serializer options |
| `travelfinder-api/Host/PlanRequestValidator.cs` | Coordinates, messages, `requestId` |
| `travelfinder-api/Host/ISseWriter.cs` | Typed `event:` + `data:` frames |
| `travelfinder-api/Host/HttpSseWriter.cs` | ASP.NET SSE implementation |
| `travelfinder-api/Host/PlanOrchestrator.cs` | 45 s deadline, stage sequence, logging |
| `travelfinder-api/Controllers/PlanController.cs` | `POST /plan/stream` only |
| `travelfinder-api/TravelfinderAPI.Tests/**` | xUnit GIS / host / agent tests |

### Backend (modify)

| File | Change |
|---|---|
| `travelfinder-api/TravelfinderAPI.csproj` | MAF + MEAI + test internals |
| `travelfinder-api/TravelfinderAPI.sln` | Add test project |
| `travelfinder-api/Program.cs` | Register facade, named HttpClients, chat clients, agents, orchestrator |
| `travelfinder-api/appsettings.json` | Azure / xAI / GIS timeout / planning section |

Leave untouched this slice: `Controllers/ChatController.cs`, `AzureAIApiClient.cs`, `xAIApiClient.cs`, `OpenAIApiClient.cs`, `GmpGisApiClient.cs`, `ArcGisApiClient.cs`, `Controllers/GisController.cs`, `Controllers/MapController.cs`.

### Frontend (create)

| File | Responsibility |
|---|---|
| `travelfinder-web/postcss.config.js` | Tailwind + autoprefixer |
| `travelfinder-web/src/app/core/domain/models.ts` | `Place`, `PlanSpec`, `PlanEvent`, `ItineraryStop` |
| `travelfinder-web/src/app/core/api/sse-parser.ts` | Frame parser; bad JSON → `error` |
| `travelfinder-web/src/app/core/api/plan-client.ts` | `POST /plan/stream` only |
| `travelfinder-web/src/app/core/state/planning-session.store.ts` | Phase + merge-by-index; new plan is the only reset |
| `travelfinder-web/src/app/ui/ui-button.component.ts` | Tiny button primitive |
| `travelfinder-web/src/app/ui/ui-panel.component.ts` | Tiny panel primitive |
| `travelfinder-web/src/app/ui/ui-spinner.component.ts` | Tiny spinner primitive |
| `travelfinder-web/src/app/features/chat/chat-panel.component.ts` | Messages, clarification, phase |
| `travelfinder-web/src/app/features/plan/itinerary-cards.component.ts` | Stops from merged deltas |
| `travelfinder-web/src/app/pages/detail/detail.page.ts` | Read-only itinerary / place |

### Frontend (modify / replace)

| File | Change |
|---|---|
| `travelfinder-web/src/global.scss` | Tailwind in; Ionic CSS out |
| `travelfinder-web/src/theme/variables.scss` | Drop Ionic theme vars; keep Tailwind layers |
| `travelfinder-web/src/main.ts` | Drop `provideIonicAngular` / `IonicRouteStrategy` |
| `travelfinder-web/src/app/app.component.ts` + `.html` | Tailwind shell (top bar + main) |
| `travelfinder-web/src/app/app.routes.ts` | `home` / `plan` / `detail` without menu parent |
| `travelfinder-web/src/app/pages/home/**` | Prompt + geolocation; no `ion-` / `ui5-` |
| `travelfinder-web/src/app/pages/plan/**` | Chat + map workspace |
| `travelfinder-web/src/app/components/map/**` → `features/map/**` | Inputs are `Place[]`, selected id, stop order; incremental graphics |

`src/app/pages/menu/**`, `src/app/services/api.service.ts`, and old `plan-detail` stay in the tree until Task 18 removes Ionic/UI5 usage from shipped routes. New pages must not import them.

---

### Task 1: Contracts and test project

**Files:**
- Create: `travelfinder-api/TravelfinderAPI.Tests/TravelfinderAPI.Tests.csproj`
- Create: `travelfinder-api/TravelfinderAPI.Tests/Contracts/PlanJsonRoundtripTests.cs`
- Create: `travelfinder-api/Contracts/GeoPoint.cs`
- Create: `travelfinder-api/Contracts/Place.cs`
- Create: `travelfinder-api/Contracts/PlanSpec.cs`
- Create: `travelfinder-api/Contracts/ChatMessageDto.cs`
- Create: `travelfinder-api/Contracts/PlanRequest.cs`
- Create: `travelfinder-api/Contracts/ItineraryStop.cs`
- Create: `travelfinder-api/Contracts/PlanEvents.cs`
- Create: `travelfinder-api/Host/PlanJson.cs`
- Modify: `travelfinder-api/TravelfinderAPI.csproj`
- Modify: `travelfinder-api/TravelfinderAPI.sln`

**Interfaces:**
- Consumes: nothing
- Produces: `GeoPoint`, `Place`, `PlanSpec`, `ChatMessageDto`, `PlanRequest`, `ItineraryStop`, `ClarificationPayload`, `PlacesPayload`, `ErrorPayload`, `DonePayload`, `PlanErrorCode`, `PlanJson.Options`

- [ ] **Step 1: Create the xUnit project and add it to the solution**

```bash
dotnet new xunit -n TravelfinderAPI.Tests -o travelfinder-api/TravelfinderAPI.Tests --framework net8.0 --force
dotnet sln travelfinder-api/TravelfinderAPI.sln add travelfinder-api/TravelfinderAPI.Tests/TravelfinderAPI.Tests.csproj
dotnet add travelfinder-api/TravelfinderAPI.Tests/TravelfinderAPI.Tests.csproj reference travelfinder-api/TravelfinderAPI.csproj
```

Replace the generated csproj with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\TravelfinderAPI.csproj" />
  </ItemGroup>
</Project>
```

Delete the template `UnitTest1.cs` if it exists.

- [ ] **Step 2: Write the failing round-trip test**

```csharp
using System.Text.Json;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPI.Tests.Contracts;

public class PlanJsonRoundtripTests
{
    [Fact]
    public void Place_serializes_id_as_source_colon_sourceId()
    {
        var place = new Place
        {
            Id = "google:abc",
            Source = PlaceSource.Google,
            SourceId = "abc",
            Name = "Fort Canning",
            Address = "River Valley Rd",
            PrimaryType = "park",
            Categories = ["park"],
            Rating = 4.6,
            PriceLevel = "PRICE_LEVEL_MODERATE",
            Location = new GeoPoint(1.295, 103.846),
            Score = 0.81
        };

        var json = JsonSerializer.Serialize(place, PlanJson.Options);

        Assert.Contains("\"id\":\"google:abc\"", json);
        Assert.Contains("\"source\":\"google\"", json);
        Assert.Contains("\"sourceId\":\"abc\"", json);
        Assert.Contains("\"latitude\":1.295", json);
    }

    [Fact]
    public void PlanRequest_does_not_accept_systemId_property()
    {
        const string body = """
            {"messages":[{"role":"user","content":"coffee"}],"latitude":1.35,"longitude":103.82,"requestId":"r1","systemId":"gis_helper"}
            """;

        var request = JsonSerializer.Deserialize<PlanRequest>(body, PlanJson.Options);

        Assert.NotNull(request);
        Assert.Equal("r1", request!.RequestId);
        Assert.Null(request.GetType().GetProperty("SystemId"));
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanJsonRoundtripTests -v n
```

Expected: FAIL with `The type or namespace name 'Place' could not be found`.

- [ ] **Step 4: Write the contract types**

`travelfinder-api/Host/PlanJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TravelfinderAPI.Host;

public static class PlanJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

`travelfinder-api/Contracts/GeoPoint.cs`:

```csharp
namespace TravelfinderAPI.Contracts;

public sealed record GeoPoint(double Latitude, double Longitude);
```

`travelfinder-api/Contracts/Place.cs`:

```csharp
using System.Text.Json.Serialization;

namespace TravelfinderAPI.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlaceSource
{
    [JsonStringEnumMemberName("google")]
    Google,
    [JsonStringEnumMemberName("arcgis")]
    Arcgis
}

public sealed class Place
{
    public required string Id { get; init; }
    public required PlaceSource Source { get; init; }
    public required string SourceId { get; init; }
    public required string Name { get; init; }
    public string? Address { get; init; }
    public string? PrimaryType { get; init; }
    public string[] Categories { get; init; } = [];
    public double? Rating { get; init; }
    public string? PriceLevel { get; init; }
    public required GeoPoint Location { get; init; }
    public double Score { get; set; }

    public static string ComposeId(PlaceSource source, string sourceId) =>
        source == PlaceSource.Google ? $"google:{sourceId}" : $"arcgis:{sourceId}";
}
```

`travelfinder-api/Contracts/PlanSpec.cs`:

```csharp
using System.Text.Json.Serialization;

namespace TravelfinderAPI.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BudgetLevel
{
    [JsonStringEnumMemberName("low")]
    Low,
    [JsonStringEnumMemberName("moderate")]
    Moderate,
    [JsonStringEnumMemberName("high")]
    High
}

public sealed class PlanSpec
{
    public string Language { get; init; } = "en-us";
    public string AreaLabel { get; init; } = "current location";
    public int RadiusMeters { get; set; } = 5000;
    public string[] Categories { get; init; } = [];
    public string[] PointOfInterests { get; init; } = [];
    public BudgetLevel BudgetLevel { get; init; } = BudgetLevel.Moderate;
    public int DayCount { get; set; } = 1;
    public string Notes { get; init; } = "";

    public const int DefaultRadiusMeters = 5000;
    public const int MinRadiusMeters = 500;
    public const int MaxRadiusMeters = 20000;

    public void ClampRadius()
    {
        if (RadiusMeters <= 0)
        {
            RadiusMeters = DefaultRadiusMeters;
        }

        RadiusMeters = Math.Clamp(RadiusMeters, MinRadiusMeters, MaxRadiusMeters);
    }
}
```

`travelfinder-api/Contracts/ChatMessageDto.cs`:

```csharp
namespace TravelfinderAPI.Contracts;

public sealed class ChatMessageDto
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}
```

`travelfinder-api/Contracts/PlanRequest.cs`:

```csharp
namespace TravelfinderAPI.Contracts;

public sealed class PlanRequest
{
    public List<ChatMessageDto> Messages { get; init; } = [];
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string? Language { get; init; }
    public required string RequestId { get; init; }
}
```

`travelfinder-api/Contracts/ItineraryStop.cs`:

```csharp
namespace TravelfinderAPI.Contracts;

public sealed class ItineraryStop
{
    public int DayIndex { get; init; }
    public int StopIndex { get; init; }
    public required string PlaceId { get; init; }
    public required string Name { get; init; }
    public string? Reason { get; init; }
    public int? DurationMinutes { get; init; }
}
```

`travelfinder-api/Contracts/PlanEvents.cs`:

```csharp
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
```

`JsonStringEnumMemberName` requires `System.Text.Json` 9+ or a custom converter on net8. If the build fails on that attribute, replace the enums with this converter in `Host/PlanJson.cs` and keep the serialized strings exactly `google`, `arcgis`, `low`, `moderate`, `high`:

```csharp
public sealed class LowercaseEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Enum.Parse<T>(reader.GetString()!, ignoreCase: true);

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}
```

Then annotate `PlaceSource` and `BudgetLevel` with `[JsonConverter(typeof(LowercaseEnumConverter<PlaceSource>))]` (and the matching budget converter). `arcgis` must serialize as `arcgis`, not `arcGis`.

- [ ] **Step 5: Run the tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanJsonRoundtripTests -v n
```

Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add travelfinder-api/Contracts travelfinder-api/Host/PlanJson.cs travelfinder-api/TravelfinderAPI.Tests travelfinder-api/TravelfinderAPI.sln travelfinder-api/TravelfinderAPI.csproj
git commit -m "feat: add planning contracts and xUnit project"
```

---

### Task 2: Place dedupe

**Files:**
- Create: `travelfinder-api/Gis/GeoMath.cs`
- Create: `travelfinder-api/Gis/PlaceDedupe.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceDedupeTests.cs`

**Interfaces:**
- Consumes: `Place`, `GeoPoint`, `PlaceSource`
- Produces: `GeoMath.DistanceMeters`, `GeoMath.NormalizeName`, `PlaceDedupe.Merge(IEnumerable<Place>)`

- [ ] **Step 1: Write the failing tests**

```csharp
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class PlaceDedupeTests
{
    private static Place P(string source, string sourceId, string name, double lat, double lng, double? rating = null) =>
        new()
        {
            Id = Place.ComposeId(source == "google" ? PlaceSource.Google : PlaceSource.Arcgis, sourceId),
            Source = source == "google" ? PlaceSource.Google : PlaceSource.Arcgis,
            SourceId = sourceId,
            Name = name,
            Categories = [],
            Rating = rating,
            Location = new GeoPoint(lat, lng)
        };

    [Fact]
    public void Same_source_and_sourceId_collapses_to_one_row()
    {
        var merged = PlaceDedupe.Merge([
            P("google", "abc", "Park", 1.3, 103.8, 4.2),
            P("google", "abc", "Park", 1.3, 103.8, 4.2)
        ]);

        Assert.Single(merged);
        Assert.Equal("google:abc", merged[0].Id);
    }

    [Fact]
    public void Near_duplicate_name_within_80m_collapses()
    {
        var merged = PlaceDedupe.Merge([
            P("google", "1", "Fort Canning Park", 1.295484, 103.845735, 4.5),
            P("arcgis", "9", "fort canning park", 1.295700, 103.845900)
        ]);

        Assert.Single(merged);
        Assert.Equal(PlaceSource.Google, merged[0].Source);
        Assert.Equal(4.5, merged[0].Rating);
    }

    [Fact]
    public void Same_name_beyond_80m_stays_two_rows()
    {
        var merged = PlaceDedupe.Merge([
            P("google", "1", "Coffee Shop", 1.2950, 103.8450, 4.1),
            P("google", "2", "Coffee Shop", 1.2970, 103.8470, 4.0)
        ]);

        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void Distinct_arcgis_names_are_kept()
    {
        var merged = PlaceDedupe.Merge([
            P("arcgis", "1", "Secret Lookout", 1.2950, 103.8450),
            P("arcgis", "2", "Hidden Mural", 1.2951, 103.8451)
        ]);

        Assert.Equal(2, merged.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceDedupeTests -v n
```

Expected: FAIL with `The type or namespace name 'PlaceDedupe' could not be found`.

- [ ] **Step 3: Write GeoMath and PlaceDedupe**

`travelfinder-api/Gis/GeoMath.cs`:

```csharp
using System.Text.RegularExpressions;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class GeoMath
{
    private static readonly Regex CollapseWs = new(@"\s+", RegexOptions.Compiled);
    private static readonly string[] TrivialSuffixes = ["park", "restaurant", "cafe", "bar", "store", "museum", "gallery"];

    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        const double earth = 6371000;
        var dLat = DegreesToRadians(b.Latitude - a.Latitude);
        var dLon = DegreesToRadians(b.Longitude - a.Longitude);
        var lat1 = DegreesToRadians(a.Latitude);
        var lat2 = DegreesToRadians(b.Latitude);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earth * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static string NormalizeName(string name)
    {
        var n = CollapseWs.Replace(name.Trim().ToLowerInvariant(), " ");
        foreach (var suffix in TrivialSuffixes)
        {
            if (n.EndsWith(" " + suffix, StringComparison.Ordinal))
            {
                n = n[..^ (suffix.Length + 1)].Trim();
            }
        }

        return n;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
```

`travelfinder-api/Gis/PlaceDedupe.cs`:

```csharp
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceDedupe
{
    public const double CollapseMeters = 80;

    public static IReadOnlyList<Place> Merge(IEnumerable<Place> places)
    {
        var byId = new Dictionary<string, Place>(StringComparer.Ordinal);
        foreach (var place in places)
        {
            if (byId.TryGetValue(place.Id, out var existing))
            {
                byId[place.Id] = Prefer(existing, place);
            }
            else
            {
                byId[place.Id] = place;
            }
        }

        var remaining = byId.Values.ToList();
        var kept = new List<Place>();

        foreach (var candidate in remaining)
        {
            var matchIndex = kept.FindIndex(existing => IsNearDuplicate(existing, candidate));
            if (matchIndex < 0)
            {
                kept.Add(candidate);
                continue;
            }

            kept[matchIndex] = Prefer(kept[matchIndex], candidate);
        }

        return kept;
    }

    private static bool IsNearDuplicate(Place a, Place b)
    {
        if (GeoMath.NormalizeName(a.Name) != GeoMath.NormalizeName(b.Name))
        {
            return false;
        }

        return GeoMath.DistanceMeters(a.Location, b.Location) <= CollapseMeters;
    }

    private static Place Prefer(Place a, Place b)
    {
        var aRated = a.Rating.HasValue;
        var bRated = b.Rating.HasValue;
        if (aRated != bRated)
        {
            return aRated ? a : b;
        }

        if (a.Source != b.Source)
        {
            return a.Source == PlaceSource.Google ? a : b;
        }

        return a;
    }
}
```

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceDedupeTests -v n
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis/GeoMath.cs travelfinder-api/Gis/PlaceDedupe.cs travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceDedupeTests.cs
git commit -m "feat: dedupe places by id and near-name"
```

---

### Task 3: Place scoring

**Files:**
- Create: `travelfinder-api/Gis/PlaceScoring.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceScoringTests.cs`

**Interfaces:**
- Consumes: `Place`, `PlanSpec`, `GeoMath.DistanceMeters`
- Produces: `PlaceScoring.Apply(IReadOnlyList<Place>, PlanSpec, GeoPoint origin)` — mutates `Score`, returns ordered list

- [ ] **Step 1: Write the failing tests**

```csharp
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class PlaceScoringTests
{
    private static Place P(string source, string name, string type, double lat, double lng, double? rating = null) =>
        new()
        {
            Id = Place.ComposeId(source == "google" ? PlaceSource.Google : PlaceSource.Arcgis, name),
            Source = source == "google" ? PlaceSource.Google : PlaceSource.Arcgis,
            SourceId = name,
            Name = name,
            PrimaryType = type,
            Categories = [type],
            Rating = rating,
            Location = new GeoPoint(lat, lng)
        };

    [Fact]
    public void Type_match_outranks_unrelated_type_at_same_point()
    {
        var spec = new PlanSpec { Categories = ["park"], RadiusMeters = 5000 };
        var origin = new GeoPoint(1.3, 103.8);
        var ranked = PlaceScoring.Apply(
            [P("google", "Park", "park", 1.3, 103.8, 4.0), P("google", "Shop", "store", 1.3, 103.8, 4.0)],
            spec,
            origin);

        Assert.Equal("Park", ranked[0].Name);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void Distance_decays_to_zero_at_radius()
    {
        var spec = new PlanSpec { RadiusMeters = 1000 };
        var origin = new GeoPoint(1.3, 103.8);
        var far = P("google", "Far", "park", 1.3 + 0.02, 103.8, 5.0);
        var scored = PlaceScoring.Apply([far], spec, origin);
        var distanceComponent = scored[0].Score - (0.20 * 1.0) - (0.10 * 0.6);

        Assert.InRange(distanceComponent, -0.01, 0.05);
    }

    [Fact]
    public void Arcgis_point_is_not_fully_buried_under_google_rows()
    {
        var spec = new PlanSpec { Categories = ["park"], RadiusMeters = 5000 };
        var origin = new GeoPoint(1.3, 103.8);
        var googleCrowd = Enumerable.Range(0, 8)
            .Select(i => P("google", $"G{i}", "store", 1.3001, 103.8001, 3.0))
            .Append(P("arcgis", "UserSpot", "park", 1.3, 103.8))
            .ToArray();

        var ranked = PlaceScoring.Apply(googleCrowd, spec, origin);

        Assert.Equal("UserSpot", ranked[0].Name);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceScoringTests -v n
```

Expected: FAIL with `The type or namespace name 'PlaceScoring' could not be found`.

- [ ] **Step 3: Write PlaceScoring**

```csharp
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceScoring
{
    public static IReadOnlyList<Place> Apply(IReadOnlyList<Place> places, PlanSpec spec, GeoPoint origin)
    {
        foreach (var place in places)
        {
            place.Score = 0.45 * TypeMatch(place, spec)
                          + 0.25 * DistanceScore(place, spec, origin)
                          + 0.20 * RatingScore(place)
                          + 0.10 * SourcePrior(place);
        }

        return places.OrderByDescending(p => p.Score).ToList();
    }

    private static double TypeMatch(Place place, PlanSpec spec)
    {
        var needles = spec.Categories.Concat(spec.PointOfInterests)
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .ToHashSet();
        if (needles.Count == 0)
        {
            return 0.5;
        }

        var hay = place.Categories.Append(place.PrimaryType ?? "").Append(place.Name)
            .Select(s => s.ToLowerInvariant());
        return hay.Any(h => needles.Any(n => h.Contains(n, StringComparison.Ordinal))) ? 1.0 : 0.0;
    }

    private static double DistanceScore(Place place, PlanSpec spec, GeoPoint origin)
    {
        var radius = spec.RadiusMeters <= 0 ? PlanSpec.DefaultRadiusMeters : spec.RadiusMeters;
        var meters = GeoMath.DistanceMeters(origin, place.Location);
        return Math.Clamp(1.0 - (meters / radius), 0, 1);
    }

    private static double RatingScore(Place place) => place.Rating.HasValue ? Math.Clamp(place.Rating.Value / 5.0, 0, 1) : 0.5;

    private static double SourcePrior(Place place) => place.Source == PlaceSource.Arcgis ? 0.8 : 0.6;
}
```

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceScoringTests -v n
```

Expected: PASS (3 tests). If the distance test is brittle because 0.02° is not exactly 1000 m, adjust the far point so `DistanceMeters` is ≥ `RadiusMeters` (use `1.3 + 0.03` if needed) rather than loosening the formula.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis/PlaceScoring.cs travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceScoringTests.cs
git commit -m "feat: score merged places for sort and truncation"
```

---

### Task 4: Cache keys

**Files:**
- Create: `travelfinder-api/Gis/Geohash.cs`
- Create: `travelfinder-api/Gis/PlaceCacheKeys.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceCacheKeyTests.cs`

**Interfaces:**
- Consumes: `GeoPoint`
- Produces: `PlaceCacheKeys.Nearby`, `PlaceCacheKeys.Text`, `PlaceCacheKeys.Reverse`, `NearbyTtl = 10 min`, `TextTtl = 10 min`, `ReverseTtl = 24 h`

- [ ] **Step 1: Write the failing tests**

```csharp
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class PlaceCacheKeyTests
{
    [Fact]
    public void Same_geohash7_and_radius_bucket_hits()
    {
        var a = PlaceCacheKeys.Nearby("google", new GeoPoint(1.35210, 103.81980), 5000, ["park"], "en-us");
        var b = PlaceCacheKeys.Nearby("google", new GeoPoint(1.35212, 103.81982), 5000, ["park"], "en-us");

        Assert.Equal(a, b);
        Assert.StartsWith("nearby|google|", a);
    }

    [Fact]
    public void Category_change_misses()
    {
        var a = PlaceCacheKeys.Nearby("google", new GeoPoint(1.3521, 103.8198), 5000, ["park"], "en-us");
        var b = PlaceCacheKeys.Nearby("google", new GeoPoint(1.3521, 103.8198), 5000, ["cafe"], "en-us");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Text_key_normalizes_query()
    {
        var a = PlaceCacheKeys.Text("google", "  Fort   Canning ", new GeoPoint(1.3521, 103.8198), 5000, "en-us");
        var b = PlaceCacheKeys.Text("google", "fort canning", new GeoPoint(1.3521, 103.8198), 5000, "en-us");

        Assert.Equal(a, b);
        Assert.StartsWith("text|google|fort canning|", a);
    }

    [Fact]
    public void Reverse_uses_geohash8_and_24h_ttl()
    {
        var key = PlaceCacheKeys.Reverse(new GeoPoint(1.3521, 103.8198));

        Assert.StartsWith("rev|", key);
        Assert.Equal(TimeSpan.FromHours(24), PlaceCacheKeys.ReverseTtl);
        Assert.Equal(TimeSpan.FromMinutes(10), PlaceCacheKeys.NearbyTtl);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceCacheKeyTests -v n
```

Expected: FAIL with `PlaceCacheKeys` not found.

- [ ] **Step 3: Write Geohash and PlaceCacheKeys**

`travelfinder-api/Gis/Geohash.cs`:

```csharp
namespace TravelfinderAPI.Gis;

public static class Geohash
{
    private const string Alphabet = "0123456789bcdefghjkmnpqrstuvwxyz";

    public static string Encode(double latitude, double longitude, int precision)
    {
        var minLat = -90.0;
        var maxLat = 90.0;
        var minLon = -180.0;
        var maxLon = 180.0;
        var hash = new char[precision];
        var bit = 0;
        var ch = 0;
        var even = true;
        var idx = 0;

        while (idx < precision)
        {
            if (even)
            {
                var mid = (minLon + maxLon) / 2;
                if (longitude >= mid) { ch |= 1 << (4 - bit); minLon = mid; }
                else { maxLon = mid; }
            }
            else
            {
                var mid = (minLat + maxLat) / 2;
                if (latitude >= mid) { ch |= 1 << (4 - bit); minLat = mid; }
                else { maxLat = mid; }
            }

            even = !even;
            if (bit < 4)
            {
                bit++;
            }
            else
            {
                hash[idx++] = Alphabet[ch];
                bit = 0;
                ch = 0;
            }
        }

        return new string(hash);
    }
}
```

`travelfinder-api/Gis/PlaceCacheKeys.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public static class PlaceCacheKeys
{
    public static readonly TimeSpan NearbyTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TextTtl = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan ReverseTtl = TimeSpan.FromHours(24);

    public static string Nearby(string provider, GeoPoint point, int radiusMeters, IEnumerable<string> types, string language)
    {
        var hash = TypesHash(types);
        return $"nearby|{provider}|{Geohash.Encode(point.Latitude, point.Longitude, 7)}|{RadiusBucket(radiusMeters)}|{hash}|{language.ToLowerInvariant()}";
    }

    public static string Text(string provider, string query, GeoPoint point, int radiusMeters, string language)
    {
        var normalized = Regex.Replace(query.Trim().ToLowerInvariant(), @"\s+", " ");
        return $"text|{provider}|{normalized}|{Geohash.Encode(point.Latitude, point.Longitude, 7)}|{RadiusBucket(radiusMeters)}|{language.ToLowerInvariant()}";
    }

    public static string Reverse(GeoPoint point) =>
        $"rev|{Geohash.Encode(point.Latitude, point.Longitude, 8)}";

    public static int RadiusBucket(int radiusMeters) =>
        Math.Clamp(radiusMeters, PlanSpec.MinRadiusMeters, PlanSpec.MaxRadiusMeters) / 500;

    private static string TypesHash(IEnumerable<string> types)
    {
        var joined = string.Join(",", types.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).OrderBy(t => t));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceCacheKeyTests -v n
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis/Geohash.cs travelfinder-api/Gis/PlaceCacheKeys.cs travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceCacheKeyTests.cs
git commit -m "feat: grid cache keys for nearby, text, and reverse geocode"
```

---

### Task 5: Provider port and Google adapter

**Files:**
- Create: `travelfinder-api/Gis/IPlaceProvider.cs`
- Create: `travelfinder-api/Gis/AllowedGoogleTypes.cs`
- Create: `travelfinder-api/Gis/Providers/GooglePlaceProvider.cs`
- Create: `travelfinder-api/TravelfinderAPI.Tests/Gis/FakeHttpMessageHandler.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/GooglePlaceProviderTests.cs`

**Interfaces:**
- Consumes: `Place`, `GeoPoint`, `PlaceCacheKeys`
- Produces: `IPlaceProvider.SearchText`, `Nearby`, `ReverseGeocode`; `GooglePlaceProvider` maps Google JSON onto `Place` with `Id = google:{sourceId}`

- [ ] **Step 1: Write the failing provider test**

```csharp
using System.Net;
using System.Text;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Gis.Providers;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class GooglePlaceProviderTests
{
    [Fact]
    public async Task SearchText_maps_display_name_and_composes_id()
    {
        var json = """
            {"places":[{"id":"abc","displayName":{"text":"Fort Canning"},"formattedAddress":"River Valley Rd","primaryType":"park","rating":4.6,"location":{"latitude":1.295,"longitude":103.846},"priceLevel":"PRICE_LEVEL_MODERATE"}]}
            """;
        var http = new HttpClient(new FakeHttpMessageHandler(json)) { BaseAddress = new Uri("https://places.googleapis.com/") };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var places = await provider.SearchText("fort canning", 1.3, 103.8, 5000, "en-us", CancellationToken.None);

        Assert.Single(places);
        Assert.Equal("google:abc", places[0].Id);
        Assert.Equal("Fort Canning", places[0].Name);
        Assert.Equal(4.6, places[0].Rating);
    }

    [Fact]
    public async Task Nearby_returns_empty_on_http_failure()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("nope", HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("https://places.googleapis.com/")
        };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var places = await provider.Nearby(1.3, 103.8, 5000, "en-us", ["park"], CancellationToken.None);

        Assert.Empty(places);
    }

    [Fact]
    public async Task ReverseGeocode_falls_back_to_empty_string()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("""{"results":[],"status":"ZERO_RESULTS"}"""))
        {
            BaseAddress = new Uri("https://maps.googleapis.com/")
        };
        var provider = new GooglePlaceProvider(http, MemoryCache());

        var label = await provider.ReverseGeocode(1.3, 103.8, CancellationToken.None);

        Assert.Equal("", label);
    }

    private static Microsoft.Extensions.Caching.Memory.IMemoryCache MemoryCache() =>
        new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
}
```

`travelfinder-api/TravelfinderAPI.Tests/Gis/FakeHttpMessageHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace TravelfinderAPI.Tests.Gis;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _status;
    public int Calls { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _body = body;
        _status = status;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        });
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~GooglePlaceProviderTests -v n
```

Expected: FAIL with `GooglePlaceProvider` not found.

- [ ] **Step 3: Write the port, type clip, and Google provider**

`travelfinder-api/Gis/IPlaceProvider.cs`:

```csharp
using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Gis;

public interface IPlaceProvider
{
    string Name { get; }
    int LastCacheHits { get; }
    Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken);
    Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken);
}
```

`travelfinder-api/Gis/AllowedGoogleTypes.cs`:

```csharp
namespace TravelfinderAPI.Gis;

public static class AllowedGoogleTypes
{
    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        "park", "restaurant", "art_gallery", "museum", "historical_landmark",
        "cafe", "bar", "library", "night_club", "store", "jewelry_store"
    };

    public static string[] Clip(IEnumerable<string> categories) =>
        categories.Select(c => c.Trim()).Where(c => All.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
```

`travelfinder-api/Gis/Providers/GooglePlaceProvider.cs` — use the injected `HttpClient` (already configured with API key and timeout). Field masks: `places.displayName,places.formattedAddress,places.primaryType,places.rating,places.location,places.id,places.priceLevel`. Text Search POST `https://places.googleapis.com/v1/places:searchText`. Nearby POST `https://places.googleapis.com/v1/places:searchNearby` with `includedTypes` clipped by `AllowedGoogleTypes.Clip`. Reverse GET `https://maps.googleapis.com/maps/api/geocode/json?result_type=administrative_area_level_1&latlng={lat},{lng}&key={key}`. Cache mapped `IReadOnlyList<Place>` (or the reverse label) with `IMemoryCache` and `PlaceCacheKeys`. On any exception or non-success status, return `Array.Empty<Place>()` / `""`. Cap results at 20. Map:

```csharp
new Place
{
    Id = Place.ComposeId(PlaceSource.Google, id),
    Source = PlaceSource.Google,
    SourceId = id,
    Name = displayName.Text,
    Address = formattedAddress,
    PrimaryType = primaryType,
    Categories = string.IsNullOrEmpty(primaryType) ? [] : [primaryType],
    Rating = rating,
    PriceLevel = priceLevel,
    Location = new GeoPoint(latitude, longitude)
};
```

Read `X-Goog-Api-Key` from `HttpClient.DefaultRequestHeaders` or constructor `apiKey` argument. Constructor signature:

```csharp
public GooglePlaceProvider(HttpClient httpClient, IMemoryCache cache, string? apiKey = null)
```

`HttpClient.Timeout` is set to 5 seconds in `Program.cs`, not inside the provider.

Expose `int LastCacheHits { get; }` on each provider (or on `IPlaceProvider`). Increment once per cache hit inside `SearchText` / `Nearby` / `ReverseGeocode`. `PlaceService` sums these into `MergedPlacesResult.CacheHits` so a cache hit counts as GIS success in the orchestrator log.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~GooglePlaceProviderTests -v n
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis travelfinder-api/TravelfinderAPI.Tests/Gis
git commit -m "feat: add Google place provider with fake-HTTP tests"
```

---

### Task 6: ArcGIS provider

**Files:**
- Create: `travelfinder-api/Gis/Providers/ArcGisPlaceProvider.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/ArcGisPlaceProviderTests.cs`

**Interfaces:**
- Consumes: `IPlaceProvider`, `PlaceCacheKeys`
- Produces: `ArcGisPlaceProvider` — Feature Layer query only; `SearchText` and `ReverseGeocode` return empty / `""`; `Id = arcgis:{OBJECTID}`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using TravelfinderAPI.Gis.Providers;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class ArcGisPlaceProviderTests
{
    [Fact]
    public async Task Nearby_maps_feature_layer_rows()
    {
        var json = """
            {"features":[{"attributes":{"OBJECTID":7,"Name":"Secret Lookout","Category":"park","FormattedAddress":"Hill"},"geometry":{"x":103.846,"y":1.295}}]}
            """;
        var http = new HttpClient(new FakeHttpMessageHandler(json)) { BaseAddress = new Uri("https://services8.arcgis.com/") };
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://services8.arcgis.com/layer/FeatureServer/0");

        var places = await provider.Nearby(1.3, 103.8, 5000, "en-us", [], CancellationToken.None);

        Assert.Single(places);
        Assert.Equal("arcgis:7", places[0].Id);
        Assert.Equal("Secret Lookout", places[0].Name);
        Assert.Equal(1.295, places[0].Location.Latitude);
    }

    [Fact]
    public async Task SearchText_is_empty_this_slice()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("{}"));
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://example/layer");

        Assert.Empty(await provider.SearchText("x", 1, 2, 5000, "en-us", CancellationToken.None));
        Assert.Equal("", await provider.ReverseGeocode(1, 2, CancellationToken.None));
    }

    [Fact]
    public async Task Nearby_returns_empty_when_layer_fails()
    {
        var http = new HttpClient(new FakeHttpMessageHandler("down", HttpStatusCode.BadGateway));
        var provider = new ArcGisPlaceProvider(http, MemoryCache(), "https://example/layer");

        Assert.Empty(await provider.Nearby(1, 2, 5000, "en-us", [], CancellationToken.None));
    }

    private static Microsoft.Extensions.Caching.Memory.IMemoryCache MemoryCache() =>
        new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~ArcGisPlaceProviderTests -v n
```

Expected: FAIL with `ArcGisPlaceProvider` not found.

- [ ] **Step 3: Write ArcGisPlaceProvider**

POST form to `{pointLayerUrl}/query` with `f=json`, `returnGeometry=true`, `outFields=*`, `geometry={lng},{lat}`, `geometryType=esriGeometryPoint`, `spatialRel=esriSpatialRelIntersects`, `distance={radius}`, `units=esriSRUnit_Meter`, `inSR=4326`, `outSR=4326`, `resultRecordCount=20`. Token from config via `HttpClient.DefaultRequestHeaders` or constructor. No `applyEdits`. No ArcGIS Places near-point API. Cache Nearby results only. `Name` on `IPlaceProvider` is `"arcgis"`.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~ArcGisPlaceProviderTests -v n
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis/Providers/ArcGisPlaceProvider.cs travelfinder-api/TravelfinderAPI.Tests/Gis/ArcGisPlaceProviderTests.cs
git commit -m "feat: add read-only ArcGIS feature-layer place provider"
```

---

### Task 7: Place facade

**Files:**
- Create: `travelfinder-api/Gis/IPlaceService.cs`
- Create: `travelfinder-api/Gis/PlaceService.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceServiceTests.cs`

**Interfaces:**
- Consumes: `IPlaceProvider`, `PlaceDedupe`, `PlaceScoring`, `AllowedGoogleTypes`, `PlanSpec.ClampRadius`
- Produces: `IPlaceService.SearchText`, `Nearby`, `ReverseGeocode`, `GetMergedPlaces(PlanSpec, GeoPoint, string language, CancellationToken)`

```csharp
public interface IPlaceService
{
    Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken);
    Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken);
    Task<MergedPlacesResult> GetMergedPlaces(PlanSpec spec, GeoPoint origin, string language, CancellationToken cancellationToken);
}

public sealed class MergedPlacesResult
{
    public required IReadOnlyList<Place> Places { get; init; }
    public int GoogleCount { get; init; }
    public int ArcgisCount { get; init; }
    public int CacheHits { get; init; }
    public bool PartialFailure { get; init; }
}
```

- [ ] **Step 1: Write the failing facade tests**

```csharp
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPI.Tests.Gis;

public class PlaceServiceTests
{
    [Fact]
    public async Task Google_throw_still_returns_arcgis_rows()
    {
        var google = new StubProvider("google") { ThrowOnNearby = true, ThrowOnText = true };
        var arcgis = new StubProvider("arcgis");
        arcgis.NearbyResult.Add(Place("arcgis", "1", "User Spot", 1.3, 103.8));
        var service = new PlaceService([google, arcgis]);
        var spec = new PlanSpec { Categories = ["park"], RadiusMeters = 5000 };

        var result = await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Single(result.Places);
        Assert.Equal("arcgis:1", result.Places[0].Id);
        Assert.True(result.PartialFailure);
        Assert.Equal(0, result.GoogleCount);
        Assert.Equal(1, result.ArcgisCount);
    }

    [Fact]
    public async Task Empty_merge_is_zero_places()
    {
        var service = new PlaceService([new StubProvider("google"), new StubProvider("arcgis")]);
        var result = await service.GetMergedPlaces(new PlanSpec(), new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Empty(result.Places);
    }

    [Fact]
    public async Task Radius_is_clamped_before_provider_calls()
    {
        var google = new StubProvider("google");
        var service = new PlaceService([google, new StubProvider("arcgis")]);
        var spec = new PlanSpec { RadiusMeters = 5, PointOfInterests = ["coffee"] };

        await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Equal(500, spec.RadiusMeters);
        Assert.Equal(500, google.LastRadius);
    }

    [Fact]
    public async Task Fanout_runs_each_poi_text_plus_nearby_plus_arcgis()
    {
        var google = new StubProvider("google");
        var arcgis = new StubProvider("arcgis");
        var service = new PlaceService([google, arcgis]);
        var spec = new PlanSpec
        {
            PointOfInterests = ["fort canning", "hawker"],
            Categories = ["park"]
        };

        await service.GetMergedPlaces(spec, new GeoPoint(1.3, 103.8), "en-us", CancellationToken.None);

        Assert.Equal(2, google.TextQueries.Count);
        Assert.True(google.NearbyCalled);
        Assert.True(arcgis.NearbyCalled);
    }

    private static Place Place(string source, string id, string name, double lat, double lng) =>
        new()
        {
            Id = TravelfinderAPI.Contracts.Place.ComposeId(source == "google" ? PlaceSource.Google : PlaceSource.Arcgis, id),
            Source = source == "google" ? PlaceSource.Google : PlaceSource.Arcgis,
            SourceId = id,
            Name = name,
            Categories = ["park"],
            Location = new GeoPoint(lat, lng)
        };

    private sealed class StubProvider : IPlaceProvider
    {
        public StubProvider(string name) => Name = name;
        public string Name { get; }
        public int LastCacheHits { get; set; }
        public bool ThrowOnNearby { get; set; }
        public bool ThrowOnText { get; set; }
        public bool NearbyCalled { get; private set; }
        public int LastRadius { get; private set; }
        public List<string> TextQueries { get; } = [];
        public List<Place> NearbyResult { get; } = [];
        public List<Place> TextResult { get; } = [];

        public Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken)
        {
            LastRadius = radiusMeters;
            TextQueries.Add(query);
            if (ThrowOnText) throw new HttpRequestException("google down");
            return Task.FromResult<IReadOnlyList<Place>>(TextResult);
        }

        public Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken)
        {
            NearbyCalled = true;
            LastRadius = radiusMeters;
            if (ThrowOnNearby) throw new HttpRequestException("google down");
            return Task.FromResult<IReadOnlyList<Place>>(NearbyResult);
        }

        public Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken) =>
            Task.FromResult("Singapore");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceServiceTests -v n
```

Expected: FAIL with `PlaceService` / `IPlaceService` not found.

- [ ] **Step 3: Write PlaceService**

`GetMergedPlaces` sequence:

1. `spec.ClampRadius()`.
2. Origin = user `GeoPoint`.
3. Start parallel tasks:
   - each `pointOfInterests` term → Google `SearchText` (skip if no Google provider)
   - if `categories` is non-empty → Google `Nearby` with `AllowedGoogleTypes.Clip(categories)`
   - always → ArcGIS `Nearby`
4. `await Task.WhenAll`. A thrown provider is caught, counted as `PartialFailure`, and treated as empty.
5. Union rows, `PlaceDedupe.Merge`, `PlaceScoring.Apply`, take 40.
6. `SearchText` / `Nearby` / `ReverseGeocode` on the facade delegate to the Google provider when present, otherwise ArcGIS / `""`.

Hard caps: take at most 20 rows from each provider before merge.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlaceServiceTests -v n
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Gis/IPlaceService.cs travelfinder-api/Gis/PlaceService.cs travelfinder-api/TravelfinderAPI.Tests/Gis/PlaceServiceTests.cs
git commit -m "feat: merge, score, and tolerate partial GIS failure in IPlaceService"
```

---

### Task 8: SSE writer and request validation

**Files:**
- Create: `travelfinder-api/Host/PlanRequestValidator.cs`
- Create: `travelfinder-api/Host/ISseWriter.cs`
- Create: `travelfinder-api/Host/RecordingSseWriter.cs` in the test project (or `Host/InMemorySseWriter.cs` used by tests)
- Test: `travelfinder-api/TravelfinderAPI.Tests/Host/PlanRequestValidatorTests.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Host/SseWriterTests.cs`

**Interfaces:**
- Consumes: `PlanRequest`, `PlanJson.Options`, `ErrorPayload`
- Produces: `PlanRequestValidator.TryValidate` → `ErrorPayload?`; `ISseWriter.WriteAsync(string eventName, object payload, CancellationToken)`

- [ ] **Step 1: Write the failing tests**

```csharp
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPI.Tests.Host;

public class PlanRequestValidatorTests
{
    [Fact]
    public void Missing_requestId_is_validation()
    {
        var error = PlanRequestValidator.TryValidate(new PlanRequest
        {
            RequestId = " ",
            Messages = [new ChatMessageDto { Role = "user", Content = "hi" }],
            Latitude = 1.3,
            Longitude = 103.8
        });

        Assert.NotNull(error);
        Assert.Equal(PlanErrorCode.Validation, error!.Code);
        Assert.False(error.Retryable);
    }

    [Fact]
    public void Empty_messages_is_validation()
    {
        var error = PlanRequestValidator.TryValidate(new PlanRequest
        {
            RequestId = "r1",
            Messages = [],
            Latitude = 1.3,
            Longitude = 103.8
        });

        Assert.Equal(PlanErrorCode.Validation, error!.Code);
    }

    [Fact]
    public void Out_of_range_coordinates_are_validation()
    {
        var error = PlanRequestValidator.TryValidate(new PlanRequest
        {
            RequestId = "r1",
            Messages = [new ChatMessageDto { Role = "user", Content = "hi" }],
            Latitude = 91,
            Longitude = 103.8
        });

        Assert.Equal(PlanErrorCode.Validation, error!.Code);
    }

    [Fact]
    public void Valid_request_returns_null()
    {
        var error = PlanRequestValidator.TryValidate(new PlanRequest
        {
            RequestId = "r1",
            Messages = [new ChatMessageDto { Role = "user", Content = "hi" }],
            Latitude = 1.3521,
            Longitude = 103.8198
        });

        Assert.Null(error);
    }
}
```

```csharp
using System.Text;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPI.Tests.Host;

public class SseWriterTests
{
    [Fact]
    public async Task Writes_event_name_and_closed_json_data()
    {
        var buffer = new MemoryStream();
        var writer = new StreamSseWriter(buffer);

        await writer.WriteAsync(PlanEventNames.PlanSpec, new PlanSpec { AreaLabel = "Singapore" }, CancellationToken.None);

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        Assert.Contains("event: plan_spec\n", text);
        Assert.Contains("data: {", text);
        Assert.Contains("\"areaLabel\":\"Singapore\"", text);
        Assert.EndsWith("\n\n", text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanRequestValidatorTests --filter FullyQualifiedName~SseWriterTests -v n
```

xUnit accepts one `--filter`. Run:

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter "FullyQualifiedName~PlanRequestValidatorTests|FullyQualifiedName~SseWriterTests" -v n
```

Expected: FAIL, types not found.

- [ ] **Step 3: Write validator and writers**

`PlanRequestValidator.TryValidate`:
- `RequestId` whitespace → `ErrorPayload { Code = validation, Message = "requestId is required.", Retryable = false }`
- no messages or all contents whitespace → `"messages must not be empty."`
- latitude not in `[-90, 90]` or longitude not in `[-180, 180]`, or non-finite → `"coordinates are required."`
- otherwise `null`

`ISseWriter`:

```csharp
public interface ISseWriter
{
    Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken);
}
```

`StreamSseWriter` (used by tests and as the body of `HttpSseWriter`):

```csharp
public sealed class StreamSseWriter : ISseWriter
{
    private readonly Stream _stream;
    public StreamSseWriter(Stream stream) => _stream = stream;

    public async Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, payload.GetType(), PlanJson.Options);
        var frame = $"event: {eventName}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _stream.WriteAsync(bytes, cancellationToken);
        await _stream.FlushAsync(cancellationToken);
    }
}
```

`HttpSseWriter` wraps `HttpResponse.Body` after setting `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `Connection: keep-alive`. Do not reuse `SSESendDataAsync` (it does not prefix `data:` or emit a blank line).

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter "FullyQualifiedName~PlanRequestValidatorTests|FullyQualifiedName~SseWriterTests" -v n
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Host travelfinder-api/TravelfinderAPI.Tests/Host
git commit -m "feat: validate plan requests and write typed SSE frames"
```

---

### Task 9: Planning host with fake agents

**Files:**
- Create: `travelfinder-api/Agents/PlannerOutcome.cs`
- Create: `travelfinder-api/Agents/IPlanner.cs`
- Create: `travelfinder-api/Agents/IRenderer.cs`
- Create: `travelfinder-api/Host/PlanOrchestrator.cs`
- Create: `travelfinder-api/Controllers/PlanController.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Host/PlanOrchestratorTests.cs`

**Interfaces:**
- Consumes: `IPlanner`, `IRenderer`, `IPlaceService`, `ISseWriter`, `IModelFailover` (optional this task — use `"fake"` until Task 10)
- Produces: event sequence documented below; `PlanController` is a thin SSE host

```csharp
public sealed class PlannerOutcome
{
    public PlanSpec? Spec { get; init; }
    public string? Clarification { get; init; }
    public bool IsClarification => !string.IsNullOrWhiteSpace(Clarification);
}

public interface IPlanner
{
    Task<PlannerOutcome> PlanAsync(
        IReadOnlyList<ChatMessageDto> messages,
        double latitude,
        double longitude,
        string language,
        CancellationToken cancellationToken);
}

public interface IRenderer
{
    IAsyncEnumerable<ItineraryStop> RenderAsync(
        PlanSpec spec,
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write the failing orchestrator tests**

```csharp
using System.Text.Json;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPI.Tests.Host;

public class PlanOrchestratorTests
{
    [Fact]
    public async Task Happy_path_emits_spec_places_delta_done()
    {
        var writer = new RecordingSseWriter();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Spec = new PlanSpec { AreaLabel = "Singapore", Categories = ["park"] } }),
            places: [SamplePlace()],
            stops: [new ItineraryStop { DayIndex = 0, StopIndex = 0, PlaceId = "google:1", Name = "Park" }]);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal(
            [PlanEventNames.PlanSpec, PlanEventNames.Places, PlanEventNames.PlanDelta, PlanEventNames.Done],
            writer.Names);
        Assert.DoesNotContain(PlanEventNames.Clarification, writer.Names);
    }

    [Fact]
    public async Task Clarification_emits_no_places()
    {
        var writer = new RecordingSseWriter();
        var places = new TrackingPlaceService();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Clarification = "How many days?" }),
            placeService: places);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal([PlanEventNames.Clarification, PlanEventNames.Done], writer.Names);
        Assert.False(places.MergedCalled);
    }

    [Fact]
    public async Task Client_abort_stops_further_events()
    {
        var writer = new RecordingSseWriter();
        using var cts = new CancellationTokenSource();
        var planner = new FakePlanner(new PlannerOutcome { Spec = new PlanSpec() }, onPlan: cts.Cancel);
        var orchestrator = Create(planner: planner, places: [SamplePlace()]);

        await orchestrator.RunAsync(ValidRequest(), writer, cts.Token);

        Assert.DoesNotContain(PlanEventNames.Places, writer.Names);
        Assert.DoesNotContain(PlanEventNames.PlanDelta, writer.Names);
    }

    [Fact]
    public async Task No_places_emits_error_then_done()
    {
        var writer = new RecordingSseWriter();
        var orchestrator = Create(
            planner: new FakePlanner(new PlannerOutcome { Spec = new PlanSpec { Categories = ["park"] } }),
            places: []);

        await orchestrator.RunAsync(ValidRequest(), writer, CancellationToken.None);

        Assert.Equal([PlanEventNames.PlanSpec, PlanEventNames.Error, PlanEventNames.Done], writer.Names);
        var error = JsonSerializer.Deserialize<ErrorPayload>(writer.Data[1], PlanJson.Options);
        Assert.Equal(PlanErrorCode.NoPlaces, error!.Code);
    }

    private static PlanOrchestrator Create(
        IPlanner planner,
        IReadOnlyList<Place>? places = null,
        IReadOnlyList<ItineraryStop>? stops = null,
        IPlaceService? placeService = null) =>
        new(planner, new FakeRenderer(stops ?? []), placeService ?? new TrackingPlaceService(places ?? []), NullLogger());

    private static PlanRequest ValidRequest() => new()
    {
        RequestId = "r1",
        Messages = [new ChatMessageDto { Role = "user", Content = "a day in the park" }],
        Latitude = 1.3521,
        Longitude = 103.8198,
        Language = "en-us"
    };

    private static Place SamplePlace() => new()
    {
        Id = "google:1",
        Source = PlaceSource.Google,
        SourceId = "1",
        Name = "Park",
        Categories = ["park"],
        Location = new GeoPoint(1.3, 103.8)
    };

    private static Microsoft.Extensions.Logging.ILogger<PlanOrchestrator> NullLogger() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanOrchestrator>.Instance;
}
```

Add `RecordingSseWriter`, `FakePlanner`, `FakeRenderer`, and `TrackingPlaceService` in `travelfinder-api/TravelfinderAPI.Tests/Host/Fakes.cs`:

```csharp
internal sealed class RecordingSseWriter : ISseWriter
{
    public List<string> Names { get; } = [];
    public List<string> Data { get; } = [];
    public async Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Names.Add(eventName);
        Data.Add(JsonSerializer.Serialize(payload, payload.GetType(), PlanJson.Options));
        await Task.CompletedTask;
    }
}

internal sealed class FakePlanner : IPlanner
{
    private readonly PlannerOutcome _outcome;
    private readonly Action? _onPlan;
    public FakePlanner(PlannerOutcome outcome, Action? onPlan = null) { _outcome = outcome; _onPlan = onPlan; }
    public Task<PlannerOutcome> PlanAsync(IReadOnlyList<ChatMessageDto> messages, double latitude, double longitude, string language, CancellationToken cancellationToken)
    {
        _onPlan?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_outcome);
    }
}

internal sealed class FakeRenderer : IRenderer
{
    private readonly IReadOnlyList<ItineraryStop> _stops;
    public FakeRenderer(IReadOnlyList<ItineraryStop> stops) => _stops = stops;
    public async IAsyncEnumerable<ItineraryStop> RenderAsync(PlanSpec spec, IReadOnlyList<Place> places, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var stop in _stops)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return stop;
            await Task.Yield();
        }
    }
}

internal sealed class TrackingPlaceService : IPlaceService
{
    private readonly IReadOnlyList<Place> _places;
    public bool MergedCalled { get; private set; }
    public TrackingPlaceService(IReadOnlyList<Place>? places = null) => _places = places ?? [];
    public Task<IReadOnlyList<Place>> SearchText(string query, double latitude, double longitude, int radiusMeters, string language, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Place>>([]);
    public Task<IReadOnlyList<Place>> Nearby(double latitude, double longitude, int radiusMeters, string language, string[] categories, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Place>>([]);
    public Task<string> ReverseGeocode(double latitude, double longitude, CancellationToken cancellationToken) =>
        Task.FromResult("current location");
    public Task<MergedPlacesResult> GetMergedPlaces(PlanSpec spec, GeoPoint origin, string language, CancellationToken cancellationToken)
    {
        MergedCalled = true;
        return Task.FromResult(new MergedPlacesResult { Places = _places, GoogleCount = _places.Count, ArcgisCount = 0, CacheHits = 0 });
    }
}
```

Add `Microsoft.Extensions.Logging.Abstractions` if the test project cannot see `NullLogger` through the web project reference.

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanOrchestratorTests -v n
```

Expected: FAIL with `PlanOrchestrator` not found.

- [ ] **Step 3: Write PlanOrchestrator and PlanController**

`RunAsync` sequence:

1. If `PlanRequestValidator.TryValidate` is not null → `error` + `done` and return.
2. Create a linked CTS: `CancelAfter(TimeSpan.FromSeconds(45))` plus the client token.
3. `language = request.Language` if not blank, else `"en-us"`.
4. Call planner. On `OperationCanceledException` from the 45 s clock → `error(timeout)` + `done`. On model failure → `error(model_unavailable)` + `done`.
5. If clarification → `clarification` + `done`. Do not call `GetMergedPlaces`.
6. `spec.ClampRadius()`; emit `plan_spec`.
7. `GetMergedPlaces`. If zero places → `error(no_places)` + `done`. Else emit `places` once (additional `places` events are allowed if you batch; one is enough).
8. Renderer: each stop → `plan_delta`. Renderer must not invent `PlaceId` values that are not in the merged list — the fake already obeys this; the real renderer is Task 12.
9. Always end with `done` (`providerUsed` from `IModelFailover.LastProviderUsed` when registered, else omit).
10. Catch unexpected exceptions → `error(internal)` with user message `"Something went wrong."` + `done`. Log `requestId`.
11. One structured log line: `requestId`, `planMs`, `gisMs`, `renderMs`, `providerUsed`, `googleCount`, `arcgisCount`, `cacheHits`, event name sequence, `errorCode`.

`PlanController`:

```csharp
[ApiController]
[Route("plan")]
public sealed class PlanController : ControllerBase
{
    [HttpPost("stream")]
    public async Task Stream([FromBody] PlanRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("Connection", "keep-alive");
        var writer = new StreamSseWriter(Response.Body);
        await _orchestrator.RunAsync(request, writer, cancellationToken);
    }
}
```

Do not register the real MAF agents yet. In `Program.cs` register `FakePlanner` / `FakeRenderer` only if you need the API to boot; prefer leaving agent DI for Task 13 and constructing the orchestrator in tests directly this task.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanOrchestratorTests -v n
```

Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Agents travelfinder-api/Host/PlanOrchestrator.cs travelfinder-api/Controllers/PlanController.cs travelfinder-api/TravelfinderAPI.Tests/Host
git commit -m "feat: orchestrate typed planning SSE with fake agents"
```

---

### Task 10: Azure → xAI failover client

**Files:**
- Create: `travelfinder-api/Agents/IModelFailover.cs`
- Create: `travelfinder-api/Agents/FailoverChatClient.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Agents/FailoverChatClientTests.cs`
- Modify: `travelfinder-api/TravelfinderAPI.csproj` — add MEAI packages

**Interfaces:**
- Consumes: two `IChatClient` instances (Azure primary, xAI backup)
- Produces: `FailoverChatClient : IChatClient, IModelFailover` with `LastProviderUsed` in `{ "azure", "xai" }`

- [ ] **Step 1: Add packages**

```bash
dotnet add travelfinder-api/TravelfinderAPI.csproj package Microsoft.Extensions.AI
dotnet add travelfinder-api/TravelfinderAPI.csproj package Microsoft.Extensions.AI.Abstractions
dotnet add travelfinder-api/TravelfinderAPI.csproj package Microsoft.Extensions.AI.OpenAI
dotnet add travelfinder-api/TravelfinderAPI.csproj package Azure.AI.OpenAI
dotnet add travelfinder-api/TravelfinderAPI.csproj package Microsoft.Agents.AI
```

Pick versions that support `net8.0`. Do not change `TargetFramework`.

- [ ] **Step 2: Write the failing tests**

```csharp
using System.Net;
using Microsoft.Extensions.AI;
using TravelfinderAPI.Agents;
using Xunit;

namespace TravelfinderAPI.Tests.Agents;

public class FailoverChatClientTests
{
    [Fact]
    public async Task Azure_429_retries_xai_exactly_once()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("throttled", null, HttpStatusCode.TooManyRequests));
        var xai = new StubChatClient("xai", response: "ok");
        var client = new FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("ok", response.Text);
        Assert.Equal(1, azure.Calls);
        Assert.Equal(1, xai.Calls);
        Assert.Equal("xai", client.LastProviderUsed);
    }

    [Fact]
    public async Task Azure_success_does_not_call_xai()
    {
        var azure = new StubChatClient("azure", response: "from-azure");
        var xai = new StubChatClient("xai", response: "from-xai");
        var client = new FailoverChatClient(azure, xai);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.Equal("from-azure", response.Text);
        Assert.Equal(1, azure.Calls);
        Assert.Equal(0, xai.Calls);
        Assert.Equal("azure", client.LastProviderUsed);
    }

    [Fact]
    public async Task Both_models_fail_throws()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("down", null, HttpStatusCode.BadGateway));
        var xai = new StubChatClient("xai", new HttpRequestException("down", null, HttpStatusCode.ServiceUnavailable));
        var client = new FailoverChatClient(azure, xai);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]).AsTask());

        Assert.Equal(1, azure.Calls);
        Assert.Equal(1, xai.Calls);
    }

    [Fact]
    public async Task Argument_exception_does_not_failover()
    {
        var azure = new StubChatClient("azure", new ArgumentException("bad prompt"));
        var xai = new StubChatClient("xai", response: "should-not-run");
        var client = new FailoverChatClient(azure, xai);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]).AsTask());

        Assert.Equal(0, xai.Calls);
    }
}

Put `StubChatClient` in `travelfinder-api/TravelfinderAPI.Tests/Agents/StubChatClient.cs`. It implements `IChatClient`: increment `Calls` on every `GetResponseAsync` / `GetStreamingResponseAsync`; throw `Error` if set; otherwise return `new ChatResponse([new ChatMessage(ChatRole.Assistant, response)])`. `GetService` returns `null`. `Dispose` is a no-op.

```csharp
using Microsoft.Extensions.AI;

namespace TravelfinderAPI.Tests.Agents;

internal sealed class StubChatClient : IChatClient
{
    public StubChatClient(string name, Exception? error = null, string response = "")
    {
        Name = name;
        Error = error;
        Response = response;
    }

    public string Name { get; }
    public Exception? Error { get; }
    public string Response { get; }
    public int Calls { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Error is not null) throw Error;
        return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, Response)]));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Error is not null) throw Error;
        yield return new ChatResponseUpdate(ChatRole.Assistant, Response);
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~FailoverChatClientTests -v n
```

Expected: FAIL with `FailoverChatClient` not found.

- [ ] **Step 4: Write FailoverChatClient**

```csharp
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace TravelfinderAPI.Agents;

public interface IModelFailover
{
    string LastProviderUsed { get; }
}

public sealed class FailoverChatClient : IChatClient, IModelFailover
{
    private readonly IChatClient _azure;
    private readonly IChatClient _xai;

    public FailoverChatClient(IChatClient azure, IChatClient xai)
    {
        _azure = azure;
        _xai = xai;
    }

    public string LastProviderUsed { get; private set; } = "azure";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        try
        {
            LastProviderUsed = "azure";
            return await _azure.GetResponseAsync(materialized, options, cancellationToken);
        }
        catch (Exception ex) when (IsFailoverWorthy(ex) && !cancellationToken.IsCancellationRequested)
        {
            LastProviderUsed = "xai";
            return await _xai.GetResponseAsync(materialized, options, cancellationToken);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materialized = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        IAsyncEnumerable<ChatResponseUpdate> source;
        try
        {
            LastProviderUsed = "azure";
            source = _azure.GetStreamingResponseAsync(materialized, options, cancellationToken);
        }
        catch (Exception ex) when (IsFailoverWorthy(ex) && !cancellationToken.IsCancellationRequested)
        {
            LastProviderUsed = "xai";
            source = _xai.GetStreamingResponseAsync(materialized, options, cancellationToken);
        }

        await foreach (var update in source.WithCancellation(cancellationToken))
        {
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IModelFailover) || serviceType == typeof(FailoverChatClient))
        {
            return this;
        }

        return _azure.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        _azure.Dispose();
        _xai.Dispose();
    }

    internal static bool IsFailoverWorthy(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is TaskCanceledException or TimeoutException or OperationCanceledException)
            {
                return current is not OperationCanceledException || current is TaskCanceledException;
            }

            if (current is HttpRequestException http &&
                http.StatusCode is HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError)
            {
                return true;
            }
        }

        return false;
    }
}
```

Streaming failover only applies if Azure throws before the first yielded update. Mid-stream Azure failure is terminal (matches MEAI `FailoverChatClient`). Do not walk every token to xAI.

- [ ] **Step 5: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~FailoverChatClientTests -v n
```

Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add travelfinder-api/TravelfinderAPI.csproj travelfinder-api/Agents/FailoverChatClient.cs travelfinder-api/Agents/IModelFailover.cs travelfinder-api/TravelfinderAPI.Tests/Agents
git commit -m "feat: fail over Azure chat client to xAI once on 429/5xx/timeout"
```

---

### Task 11: Planner agent

**Files:**
- Create: `travelfinder-api/Agents/PlannerAgent.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Agents/PlannerAgentTests.cs`

**Interfaces:**
- Consumes: `IChatClient`, `IPlaceService.ReverseGeocode`, `PlanSpec`
- Produces: `PlannerAgent : IPlanner` — sparse input → clarification and no `GetMergedPlaces`; complete input → clamped `PlanSpec` with allowed categories only. Planner must not put a concrete place list or invented lat/lng into the spec.

Planner talks to the model with one function tool `emit_plan_result`. Arguments:

```json
{
  "clarification": "string or empty",
  "language": "en-us",
  "areaLabel": "Singapore",
  "radiusMeters": 5000,
  "categories": ["park"],
  "pointOfInterests": ["fort canning"],
  "budgetLevel": "moderate",
  "dayCount": 1,
  "notes": ""
}
```

If `clarification` is non-empty, return `PlannerOutcome { Clarification }`. Otherwise map the rest to `PlanSpec`, `ClampRadius()`, drop categories not in `AllowedGoogleTypes.All` (unknown categories become POI terms if they look like names — actually: clip categories; leave `pointOfInterests` as the model emitted them). Area label comes from `ReverseGeocode`; if that returns empty, use `"current location"`.

Do not register `search_places` or `nearby_places` on the planner. `get_merged_places` is Host-only. Planner may call `reverse_geocode` via `IPlaceService` in process (Host/agent wrapper), not as a model-driven GIS loop.

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.Extensions.AI;
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using TravelfinderAPI.Tests.Host;
using Xunit;

namespace TravelfinderAPI.Tests.Agents;

public class PlannerAgentTests
{
    [Fact]
    public async Task Sparse_input_is_clarification_and_does_not_merge_places()
    {
        var places = new TrackingPlaceService();
        var chat = new ToolChatClient("""
            {"clarification":"How many days and what budget?","language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":[],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}
            """);
        var planner = new PlannerAgent(chat, places);

        var outcome = await planner.PlanAsync(
            [new ChatMessageDto { Role = "user", Content = "something fun" }],
            1.35, 103.82, "en-us", CancellationToken.None);

        Assert.True(outcome.IsClarification);
        Assert.Contains("days", outcome.Clarification!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(outcome.Spec);
        Assert.False(places.MergedCalled);
    }

    [Fact]
    public async Task Complete_input_returns_clamped_radius_and_allowed_categories()
    {
        var places = new TrackingPlaceService();
        var chat = new ToolChatClient("""
            {"clarification":"","language":"en-us","areaLabel":"Singapore","radiusMeters":99999,"categories":["park","spaceship","cafe"],"pointOfInterests":["fort canning"],"budgetLevel":"low","dayCount":2,"notes":"shade"}
            """);
        var planner = new PlannerAgent(chat, places);

        var outcome = await planner.PlanAsync(
            [new ChatMessageDto { Role = "user", Content = "two park days around Fort Canning, cheap, lots of shade" }],
            1.35, 103.82, "en-us", CancellationToken.None);

        Assert.False(outcome.IsClarification);
        Assert.Equal(20000, outcome.Spec!.RadiusMeters);
        Assert.Equal(["park", "cafe"], outcome.Spec.Categories);
        Assert.Equal(["fort canning"], outcome.Spec.PointOfInterests);
        Assert.Equal(BudgetLevel.Low, outcome.Spec.BudgetLevel);
        Assert.Equal(2, outcome.Spec.DayCount);
        Assert.False(places.MergedCalled);
    }
}
```

`ToolChatClient` lives in `travelfinder-api/TravelfinderAPI.Tests/Agents/ToolChatClient.cs`. It is an `IChatClient` that returns a `ChatResponse` whose first message has a function tool call named `emit_plan_result` with the constructor JSON as arguments. If the MEAI version exposes `FunctionCallContent` / `ChatToolMode`, use that. If constructing a tool call is version-awkward, have `PlannerAgent` also accept a raw JSON assistant message and parse `emit_plan_result` arguments from `response.Text` when it is a JSON object with those properties — tests then just return that JSON as `ChatResponse` text. Prefer the text-JSON path if tool-call types are unstable:

```csharp
internal sealed class ToolChatClient : IChatClient
{
    private readonly string _json;
    public ToolChatClient(string json) => _json = json;
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, _json)]));
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, _json);
        await Task.CompletedTask;
    }
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlannerAgentTests -v n
```

Expected: FAIL with `PlannerAgent` not found.

- [ ] **Step 3: Write PlannerAgent**

Constructor: `PlannerAgent(IChatClient chatClient, IPlaceService places)`.

If the installed `Microsoft.Agents.AI` package exposes `AsAIAgent`, create the planner with `chatClient.AsAIAgent(instructions: PlannerInstructions, name: "planner")` and invoke that agent. Otherwise call `IChatClient.GetResponseAsync` directly — that is the MAF/`Microsoft.Extensions.AI` substrate. Do not call Azure or xAI HTTP SDKs.

`PlanAsync`:

1. `areaLabel = await places.ReverseGeocode(...)`; if blank, `"current location"`.
2. Build chat messages: system instructions (no invented lat/lng, no concrete place list, use only provided categories from the allowed set, ask one clarification question when day count / area / interest is missing) + the user transcript + a line `User coordinates are for reverse-geocode context only. Area label: {areaLabel}.`
3. `GetResponseAsync`.
4. Deserialize assistant text as the result object. Empty / invalid JSON → `PlannerOutcome { Clarification = "I need a bit more detail about the trip." }`.
5. Non-empty `clarification` → clarification outcome.
6. Else build `PlanSpec`, clip categories through `AllowedGoogleTypes.Clip`, `ClampRadius()`, default `DayCount` to 1 when `< 1`.

Do not call `GetMergedPlaces`. Do not reference ASP.NET types or frozen `*ApiClient` types.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlannerAgentTests -v n
```

Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Agents/PlannerAgent.cs travelfinder-api/TravelfinderAPI.Tests/Agents
git commit -m "feat: planner agent returns PlanSpec or clarification"
```

---

### Task 12: Renderer agent

**Files:**
- Create: `travelfinder-api/Agents/RendererAgent.cs`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Agents/RendererAgentTests.cs`

**Interfaces:**
- Consumes: `IChatClient`, `PlanSpec`, `IReadOnlyList<Place>`
- Produces: `RendererAgent : IRenderer` — `IAsyncEnumerable<ItineraryStop>` whose `PlaceId` values are a subset of the provided places

The model must emit a JSON array of stops (or JSONL, one stop per streamed chunk). Each stop:

```json
{"dayIndex":0,"stopIndex":0,"placeId":"google:1","name":"Fort Canning","reason":"Shade and views","durationMinutes":90}
```

Drop any stop whose `placeId` is not in the provided `Place[]`. Do not invent coordinates. If the model returns nothing usable, yield no stops (Host already guaranteed non-empty places before calling the renderer).

- [ ] **Step 1: Write the failing tests**

```csharp
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Tests.Agents;
using Xunit;

namespace TravelfinderAPI.Tests.Agents;

public class RendererAgentTests
{
    [Fact]
    public async Task Writes_stops_only_from_provided_places()
    {
        var chat = new ToolChatClient("""
            [
              {"dayIndex":0,"stopIndex":0,"placeId":"google:1","name":"Fort Canning","reason":"Start","durationMinutes":90},
              {"dayIndex":0,"stopIndex":1,"placeId":"invented:99","name":"Fake Pier","reason":"Nope","durationMinutes":30}
            ]
            """);
        var renderer = new RendererAgent(chat);
        var places = new[]
        {
            new Place
            {
                Id = "google:1",
                Source = PlaceSource.Google,
                SourceId = "1",
                Name = "Fort Canning",
                Categories = ["park"],
                Location = new GeoPoint(1.295, 103.846)
            }
        };

        var stops = new List<ItineraryStop>();
        await foreach (var stop in renderer.RenderAsync(new PlanSpec { DayCount = 1 }, places, CancellationToken.None))
        {
            stops.Add(stop);
        }

        Assert.Single(stops);
        Assert.Equal("google:1", stops[0].PlaceId);
        Assert.Equal(0, stops[0].DayIndex);
        Assert.Equal(0, stops[0].StopIndex);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~RendererAgentTests -v n
```

Expected: FAIL with `RendererAgent` not found.

- [ ] **Step 3: Write RendererAgent**

`RenderAsync`:

1. System instructions: only use the supplied place ids; emit JSON array of stops; `dayIndex` is 0-based and `< spec.DayCount`; `stopIndex` is 0-based per day; do not invent places.
2. User message: serialized `PlanSpec` plus the place list (`id`, `name`, `primaryType`, `address`, `score` only).
3. `GetResponseAsync` (or stream and parse complete JSON when the array closes).
4. Deserialize `ItineraryStop[]`. Filter to known ids. Order by `DayIndex`, `StopIndex`. `yield return` each remaining stop.

- [ ] **Step 4: Run the test and make sure it passes**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~RendererAgentTests -v n
```

Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Agents/RendererAgent.cs travelfinder-api/TravelfinderAPI.Tests/Agents/RendererAgentTests.cs
git commit -m "feat: renderer agent emits itinerary stops from provided places"
```

---

### Task 13: Wire host DI, MAF clients, and observability

**Files:**
- Modify: `travelfinder-api/Program.cs`
- Modify: `travelfinder-api/appsettings.json`
- Modify: `travelfinder-api/Host/PlanOrchestrator.cs` — read `IModelFailover.LastProviderUsed` for `done`
- Test: `travelfinder-api/TravelfinderAPI.Tests/Host/PlanOrchestratorFailoverTests.cs`

**Interfaces:**
- Consumes: `FailoverChatClient`, `PlannerAgent`, `RendererAgent`, `PlaceService`, named `HttpClient`s
- Produces: bootable API where `/plan/stream` uses real agents; frozen clients stay registered for `ChatController`

- [ ] **Step 1: Write the failing test that `done` reports the failover provider**

```csharp
using TravelfinderAPI.Agents;
using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using TravelfinderAPI.Tests.Agents;
using Xunit;

namespace TravelfinderAPI.Tests.Host;

public class PlanOrchestratorFailoverTests
{
    [Fact]
    public async Task Done_includes_providerUsed_from_failover_client()
    {
        var azure = new StubChatClient("azure", new HttpRequestException("429", null, System.Net.HttpStatusCode.TooManyRequests));
        var xai = new ToolChatClient("""
            {"clarification":"Which neighborhood?","language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":[],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}
            """);
        var failover = new FailoverChatClient(azure, xai);
        var planner = new PlannerAgent(failover, new TrackingPlaceService());
        var orchestrator = new PlanOrchestrator(
            planner,
            new FakeRenderer([]),
            new TrackingPlaceService(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanOrchestrator>.Instance,
            failover);

        var writer = new RecordingSseWriter();
        await orchestrator.RunAsync(new PlanRequest
        {
            RequestId = "r1",
            Messages = [new ChatMessageDto { Role = "user", Content = "hi" }],
            Latitude = 1.35,
            Longitude = 103.82
        }, writer, CancellationToken.None);

        Assert.Equal(PlanEventNames.Done, writer.Names[^1]);
        Assert.Contains("\"providerUsed\":\"xai\"", writer.Data[^1]);
        Assert.Equal(1, azure.Calls);
    }
}
```

This requires `PlanOrchestrator` to accept optional `IModelFailover`. Add the constructor parameter in Step 3.

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln --filter FullyQualifiedName~PlanOrchestratorFailoverTests -v n
```

Expected: FAIL on constructor arity or missing `providerUsed`.

- [ ] **Step 3: Wire Program.cs and appsettings**

`appsettings.json` additions (do not delete existing keys):

```json
"Planning": {
  "AzureEndpoint": "https://travelfinder.openai.azure.com/",
  "AzureDeployment": "gpt-4-2",
  "XaiEndpoint": "https://api.x.ai/v1",
  "XaiModel": "grok-2-1212",
  "StreamDeadlineSeconds": 45,
  "ProviderTimeoutSeconds": 5
}
```

`Program.cs` additions after the existing frozen client registrations. Keep `GmpGisApiClient` and `ArcGisApiClient` scoped registrations exactly as they are so `ChatController` still builds.

```csharp
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("GooglePlaces", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri("https://places.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(config.GetValue("Planning:ProviderTimeoutSeconds", 5));
    var key = config["GMPGIS_API_KEY"] ?? "";
    if (!string.IsNullOrEmpty(key))
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Goog-Api-Key", key);
    }
}).ConfigurePrimaryHttpMessageHandler(sp => CreateHandler(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddHttpClient("ArcGisFeature", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.Timeout = TimeSpan.FromSeconds(config.GetValue("Planning:ProviderTimeoutSeconds", 5));
}).ConfigurePrimaryHttpMessageHandler(sp => CreateHandler(sp.GetRequiredService<IConfiguration>()));

builder.Services.AddSingleton<IPlaceProvider>(sp =>
    new GooglePlaceProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("GooglePlaces"),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<IConfiguration>()["GMPGIS_API_KEY"]));

builder.Services.AddSingleton<IPlaceProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var layers = config.GetSection("FEATURE_LAYER").Get<string[]>() ?? [];
    var layerUrl = layers.Length > 1 ? layers[1] : "";
    return new ArcGisPlaceProvider(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("ArcGisFeature"),
        sp.GetRequiredService<IMemoryCache>(),
        layerUrl);
});

builder.Services.AddSingleton<IPlaceService, PlaceService>();
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var azure = CreateAzureChatClient(config);
    var xai = CreateXaiChatClient(config);
    return new FailoverChatClient(azure, xai);
});
builder.Services.AddSingleton<IPlanner, PlannerAgent>();
builder.Services.AddSingleton<IRenderer, RendererAgent>();
builder.Services.AddSingleton<PlanOrchestrator>();
```

`CreateHandler` uses `ENABLE_PROXY`: when true, SOCKS/HTTP proxy as in `Utils.CreateProxy("socks5://127.0.0.1:2085")`; when false, `new HttpClientHandler()`. This is the only place new GIS HTTP is constructed.

`CreateAzureChatClient`:

```csharp
IChatClient CreateAzureChatClient(IConfiguration config)
{
    var endpoint = config["Planning:AzureEndpoint"] ?? "https://travelfinder.openai.azure.com/";
    var deployment = config["Planning:AzureDeployment"] ?? "gpt-4-2";
    var key = config["AZUREAI_API_KEY"] ?? "";
    return new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(key))
        .GetChatClient(deployment)
        .AsIChatClient();
}
```

`CreateXaiChatClient`:

```csharp
IChatClient CreateXaiChatClient(IConfiguration config)
{
    var endpoint = config["Planning:XaiEndpoint"] ?? "https://api.x.ai/v1";
    var model = config["Planning:XaiModel"] ?? "grok-2-1212";
    var key = config["XAI_API_KEY"] ?? "";
    var openAi = new OpenAI.Chat.ChatClient(model, new System.ClientModel.ApiKeyCredential(key), new OpenAI.OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint)
    });
    return openAi.AsIChatClient();
}
```

Exact `AsIChatClient` extension namespace is `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.OpenAI`. If the installed package uses a different factory, adapt to the package's documented `IChatClient` construction without calling `AzureAIApiClient` or `xAIApiClient`.

`PlanOrchestrator` constructor becomes `(IPlanner, IRenderer, IPlaceService, ILogger<PlanOrchestrator>, IModelFailover? failover = null)`. Resolve failover via `chatClient as IModelFailover` or `GetService`. `DonePayload.ProviderUsed = failover?.LastProviderUsed`.

Add a using for `Microsoft.Extensions.AI` at the top of `Program.cs`. Do not remove the existing CORS policy.

- [ ] **Step 4: Run the full backend suite**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln -v n
```

Expected: PASS, including `PlanOrchestratorFailoverTests`. `dotnet build travelfinder-api/TravelfinderAPI.sln` still compiles `ChatController`.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-api/Program.cs travelfinder-api/appsettings.json travelfinder-api/Host/PlanOrchestrator.cs travelfinder-api/TravelfinderAPI.Tests/Host/PlanOrchestratorFailoverTests.cs
git commit -m "feat: wire planning host to MAF clients and place facade"
```

---

### Task 14: Tailwind shell and home page

**Files:**
- Create: `travelfinder-web/postcss.config.js`
- Create: `travelfinder-web/src/app/ui/ui-button.component.ts`
- Create: `travelfinder-web/src/app/ui/ui-panel.component.ts`
- Create: `travelfinder-web/src/app/ui/ui-spinner.component.ts`
- Modify: `travelfinder-web/src/global.scss`
- Modify: `travelfinder-web/src/theme/variables.scss`
- Modify: `travelfinder-web/src/main.ts`
- Modify: `travelfinder-web/src/app/app.component.ts`
- Modify: `travelfinder-web/src/app/app.component.html`
- Modify: `travelfinder-web/src/app/app.routes.ts`
- Modify: `travelfinder-web/src/app/pages/home/home.page.ts`
- Modify: `travelfinder-web/src/app/pages/home/home.page.html`
- Modify: `travelfinder-web/src/app/pages/home/home.page.spec.ts`
- Test: `travelfinder-web/src/app/app.component.spec.ts`

**Interfaces:**
- Consumes: existing Tailwind config at `travelfinder-web/tailwind.config.js`
- Produces: shipped `app-root` and `/home` DOM with zero `ion-` / `ui5-` elements; home collects prompt + coordinates and navigates to `/plan`

- [ ] **Step 1: Write the failing home spec**

Replace `travelfinder-web/src/app/pages/home/home.page.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomePage } from './home.page';

describe('HomePage', () => {
  let fixture: ComponentFixture<HomePage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HomePage],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(HomePage);
    fixture.detectChanges();
  });

  it('should create without ionic or ui5 elements', () => {
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelectorAll('*')).toBeTruthy();
    expect(html.querySelector('ion-content')).toBeNull();
    expect(html.querySelector('ion-header')).toBeNull();
    expect(html.querySelector('ui5-button')).toBeNull();
    expect(html.querySelector('textarea')).toBeTruthy();
    expect(html.querySelector('button[data-role="start-plan"]')).toBeTruthy();
  });
});
```

Replace `travelfinder-web/src/app/app.component.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('renders a tailwind shell without ion-app', async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ion-app')).toBeNull();
    expect(html.querySelector('ion-router-outlet')).toBeNull();
    expect(html.querySelector('header')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run the specs to verify they fail**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/pages/home/home.page.spec.ts --include=src/app/app.component.spec.ts
```

If Karma has no `ChromeHeadless` browser yet, add it in this task:

`travelfinder-web/karma.conf.js` — set `browsers: ['ChromeHeadless']` when `process.env.CI`, otherwise keep `Chrome`. Add `customLaunchers` only if the default ChromeHeadless plugin is missing (`karma-chrome-launcher` is already a dependency).

Expected: FAIL because home still renders `ion-content`.

- [ ] **Step 3: Wire Tailwind and replace the shell**

`travelfinder-web/postcss.config.js`:

```javascript
module.exports = {
  plugins: {
    tailwindcss: {},
    autoprefixer: {}
  }
};
```

`travelfinder-web/src/global.scss` — replace the Ionic imports with:

```scss
@tailwind base;
@tailwind components;
@tailwind utilities;

html, body, app-root {
  height: 100%;
  margin: 0;
}

body {
  @apply bg-slate-50 text-slate-900 antialiased;
}
```

`travelfinder-web/src/theme/variables.scss` — delete Ionic CSS variables. Keep only:

```scss
:root {
  color-scheme: light;
}
```

Do not `@import` Tailwind here anymore (it now lives in `global.scss`).

`travelfinder-web/src/app/ui/ui-button.component.ts`:

```typescript
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [attr.data-role]="role"
      [disabled]="disabled"
      class="inline-flex items-center justify-center rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50">
      <ng-content></ng-content>
    </button>
  `
})
export class UiButtonComponent {
  @Input() disabled = false;
  @Input() role = '';
}
```

`ui-panel` is a `<section class="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">`. `ui-spinner` is an `aria-busy` SVG circle with `animate-spin`.

`app.component.ts`:

```typescript
import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  templateUrl: 'app.component.html'
})
export class AppComponent {}
```

`app.component.html`:

```html
<div class="flex min-h-full flex-col">
  <header class="border-b border-slate-200 bg-white">
    <div class="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
      <a routerLink="/home" class="text-sm font-semibold tracking-tight">Travelfinder</a>
      <nav class="flex gap-4 text-sm">
        <a routerLink="/home" class="hover:underline">Home</a>
        <a routerLink="/plan" class="hover:underline">Plan</a>
      </nav>
    </div>
  </header>
  <main class="flex-1">
    <router-outlet></router-outlet>
  </main>
</div>
```

`app.routes.ts`:

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  { path: 'home', loadComponent: () => import('./pages/home/home.page').then(m => m.HomePage) },
  { path: 'plan', loadComponent: () => import('./pages/plan/plan.page').then(m => m.PlanPage) },
  {
    path: 'detail',
    loadComponent: () =>
      import('./components/plan-detail/plan-detail.component').then(m => m.PlanDetailComponent)
  }
];
```

`detail.page` does not exist yet. Keep this route on the existing `PlanDetailComponent` so `ng serve` still compiles. Task 18 replaces it with `DetailPage`.

`main.ts` — remove Ionic:

```typescript
import { enableProdMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { routes } from './app/app.routes';
import { AppComponent } from './app/app.component';
import { environment } from './environments/environment';

if (environment.production) {
  enableProdMode();
}

bootstrapApplication(AppComponent, {
  providers: [provideRouter(routes)]
});
```

Home page: standalone, `CommonModule` + `FormsModule` + `UiButtonComponent` only. Template is a short heading, a textarea (`placeholder="Describe the trip"`), a geolocation status line, and `<ui-button role="start-plan">Start planning</ui-button>`. On click:

1. `requestId = crypto.randomUUID()`.
2. Try `navigator.geolocation.getCurrentPosition` (browser first). On failure, set `needsMapPick = true` and still navigate to `/plan` with the prompt; the plan page asks the user to click the map.
3. `sessionStorage.setItem('tf.pending', JSON.stringify({ requestId, prompt, latitude, longitude }))`.
4. `router.navigate(['/plan'])`.

Do not import `IonicsModule`, `UI5Module`, `MenuPage`, or `MapComponent` on home.

If `/plan` still imports Ionic this task, the home tests can pass while `ng serve` still compiles the old plan page. Do not rewrite plan yet.

- [ ] **Step 4: Run the specs and make sure they pass**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/pages/home/home.page.spec.ts --include=src/app/app.component.spec.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/postcss.config.js travelfinder-web/src/global.scss travelfinder-web/src/theme/variables.scss travelfinder-web/src/main.ts travelfinder-web/src/app/app.component.ts travelfinder-web/src/app/app.component.html travelfinder-web/src/app/app.component.spec.ts travelfinder-web/src/app/app.routes.ts travelfinder-web/src/app/ui travelfinder-web/src/app/pages/home travelfinder-web/karma.conf.js
git commit -m "feat: replace Ionic shell with Tailwind home and primitives"
```

---

### Task 15: PlanClient

**Files:**
- Create: `travelfinder-web/src/app/core/domain/models.ts`
- Create: `travelfinder-web/src/app/core/api/sse-parser.ts`
- Create: `travelfinder-web/src/app/core/api/plan-client.ts`
- Test: `travelfinder-web/src/app/core/api/sse-parser.spec.ts`
- Test: `travelfinder-web/src/app/core/api/plan-client.spec.ts`

**Interfaces:**
- Consumes: `environment.API_URL`
- Produces: `parseSseFrames(text)`, `PlanClient.stream(request, signal)` → `AsyncIterable<PlanEvent>`

```typescript
export type PlaceSource = 'google' | 'arcgis';
export type BudgetLevel = 'low' | 'moderate' | 'high';

export interface GeoPoint { latitude: number; longitude: number; }

export interface Place {
  id: string;
  source: PlaceSource;
  sourceId: string;
  name: string;
  address?: string;
  primaryType?: string;
  categories: string[];
  rating?: number;
  priceLevel?: string;
  location: GeoPoint;
  score: number;
}

export interface PlanSpec {
  language: string;
  areaLabel: string;
  radiusMeters: number;
  categories: string[];
  pointOfInterests: string[];
  budgetLevel: BudgetLevel;
  dayCount: number;
  notes: string;
}

export interface ChatMessageDto { role: 'user' | 'assistant'; content: string; }

export interface PlanRequest {
  messages: ChatMessageDto[];
  latitude: number;
  longitude: number;
  language?: string;
  requestId: string;
}

export interface ItineraryStop {
  dayIndex: number;
  stopIndex: number;
  placeId: string;
  name: string;
  reason?: string;
  durationMinutes?: number;
}

export interface PlanError {
  code: 'validation' | 'no_places' | 'model_unavailable' | 'timeout' | 'internal';
  message: string;
  requestId: string;
  retryable: boolean;
}

export type PlanEvent =
  | { type: 'clarification'; message: string }
  | { type: 'plan_spec'; spec: PlanSpec }
  | { type: 'places'; places: Place[] }
  | { type: 'plan_delta'; stop: ItineraryStop }
  | { type: 'error'; error: PlanError }
  | { type: 'done'; providerUsed?: string };
```

- [ ] **Step 1: Write the failing parser and client tests**

`sse-parser.spec.ts`:

```typescript
import { parseSseFrames } from './sse-parser';

describe('parseSseFrames', () => {
  it('parses named events into the union', () => {
    const frames = parseSseFrames(
      'event: plan_spec\ndata: {"language":"en-us","areaLabel":"Singapore","radiusMeters":5000,"categories":["park"],"pointOfInterests":[],"budgetLevel":"moderate","dayCount":1,"notes":""}\n\n' +
      'event: places\ndata: {"places":[]}\n\n'
    );

    expect(frames[0]).toEqual(jasmine.objectContaining({ type: 'plan_spec' }));
    expect(frames[1]).toEqual(jasmine.objectContaining({ type: 'places', places: [] }));
  });

  it('turns invalid json into error', () => {
    const frames = parseSseFrames('event: places\ndata: {not-json\n\n');
    expect(frames[0].type).toBe('error');
    expect((frames[0] as { type: 'error'; error: { code: string } }).error.code).toBe('internal');
  });
});
```

`plan-client.spec.ts`:

```typescript
import { PlanClient } from './plan-client';
import { environment } from '../../../environments/environment';

describe('PlanClient', () => {
  it('posts only to /plan/stream and yields parsed events', async () => {
    const body =
      'event: clarification\ndata: {"message":"How many days?"}\n\n' +
      'event: done\ndata: {}\n\n';
    spyOn(window, 'fetch').and.resolveTo(new Response(body, {
      headers: { 'Content-Type': 'text/event-stream' }
    }));

    const client = new PlanClient();
    const events = [];
    for await (const event of client.stream({
      messages: [{ role: 'user', content: 'hi' }],
      latitude: 1.35,
      longitude: 103.82,
      requestId: 'r1'
    })) {
      events.push(event);
    }

    expect(events.map(e => e.type)).toEqual(['clarification', 'done']);
    const [url, init] = (window.fetch as jasmine.Spy).calls.mostRecent().args;
    expect(url).toBe(`${environment.API_URL}/plan/stream`);
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body).systemId).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/api/sse-parser.spec.ts --include=src/app/core/api/plan-client.spec.ts
```

Expected: FAIL, modules not found.

- [ ] **Step 3: Implement parser and client**

`parseSseFrames(chunk: string, carry?: { event?: string; data: string[] })` splits on `\n`, reads `event:` and `data:` lines, and on a blank line emits one `PlanEvent`. Unknown event names become `error` with `code: 'internal'` and `message: 'Unknown event'`. `plan_spec` data is the spec object itself (not wrapped). `places` reads `{ places: Place[] }`. `plan_delta` data is an `ItineraryStop`. `clarification` reads `{ message }`. `error` reads `ErrorPayload`. `done` reads `{ providerUsed? }`.

`PlanClient.stream`:

```typescript
async *stream(request: PlanRequest, signal?: AbortSignal): AsyncIterable<PlanEvent> {
  const response = await fetch(`${environment.API_URL}/plan/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
    body: JSON.stringify(request),
    signal
  });
  if (!response.body) {
    yield { type: 'error', error: { code: 'internal', message: 'Empty response', requestId: request.requestId, retryable: true } };
    return;
  }
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    const parts = buffer.split('\n\n');
    buffer = parts.pop() ?? '';
    for (const frame of parts) {
      for (const event of parseSseFrames(frame + '\n\n')) {
        yield event;
      }
    }
  }
  if (buffer.trim()) {
    for (const event of parseSseFrames(buffer + '\n\n')) {
      yield event;
    }
  }
}
```

Do not import `http-streaming-request` or `best-effort-json-parser`. Do not call `/chat/*`.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/api/sse-parser.spec.ts --include=src/app/core/api/plan-client.spec.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/app/core
git commit -m "feat: parse typed /plan/stream events in PlanClient"
```

---

### Task 16: Planning session store

**Files:**
- Create: `travelfinder-web/src/app/core/state/planning-session.store.ts`
- Test: `travelfinder-web/src/app/core/state/planning-session.store.spec.ts`

**Interfaces:**
- Consumes: `PlanEvent`, `PlanRequest`, `PlanClient`
- Produces: `PlanningSessionStore` with `phase`, `places`, `stops`, `start(request)`, `apply(event)`, `reset()`

```typescript
export type SessionPhase =
  | 'idle'
  | 'planning'
  | 'clarifying'
  | 'retrieving'
  | 'rendering'
  | 'done'
  | 'error';
```

Transitions:

- `start` → `planning` and is the only path that clears places/spec/stops
- `clarification` → `clarifying`
- `plan_spec` → `retrieving`
- `places` → keep `retrieving` (or `rendering` if a delta already arrived); append places by `id`, do not wipe
- `plan_delta` → `rendering`; merge by `${dayIndex}:${stopIndex}`
- `error` → `error`; keep places and stops
- `done` → `done` unless phase is already `error` or `clarifying` (clarification-only turns stay `clarifying` until `done`, then remain `clarifying` so the user can answer; `done` after clarification sets phase to `clarifying`)

- [ ] **Step 1: Write the failing store tests**

```typescript
import { PlanningSessionStore } from './planning-session.store';
import { Place, PlanEvent, PlanSpec } from '../domain/models';

describe('PlanningSessionStore', () => {
  const spec: PlanSpec = {
    language: 'en-us',
    areaLabel: 'Singapore',
    radiusMeters: 5000,
    categories: ['park'],
    pointOfInterests: [],
    budgetLevel: 'moderate',
    dayCount: 1,
    notes: ''
  };

  const place = (id: string): Place => ({
    id,
    source: 'google',
    sourceId: id.split(':')[1],
    name: id,
    categories: ['park'],
    location: { latitude: 1.3, longitude: 103.8 },
    score: 0.5
  });

  it('merges plan_delta by dayIndex and stopIndex', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'old' } });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'A', reason: 'new' } });
    store.apply({ type: 'plan_delta', stop: { dayIndex: 0, stopIndex: 1, placeId: 'google:2', name: 'B' } });

    expect(store.snapshot().stops.length).toBe(2);
    expect(store.snapshot().stops[0].reason).toBe('new');
  });

  it('does not wipe places on a second places event', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'places', places: [place('google:1')] });
    store.apply({ type: 'places', places: [place('google:2')] });

    expect(store.snapshot().places.map(p => p.id)).toEqual(['google:1', 'google:2']);
  });

  it('error keeps places and only a new start clears them', () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({ type: 'plan_spec', spec });
    store.apply({ type: 'places', places: [place('google:1')] });
    store.apply({
      type: 'error',
      error: { code: 'timeout', message: 'timed out', requestId: 'r1', retryable: true }
    });

    expect(store.snapshot().phase).toBe('error');
    expect(store.snapshot().places.length).toBe(1);

    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r2' });
    expect(store.snapshot().places.length).toBe(0);
    expect(store.snapshot().phase).toBe('planning');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/state/planning-session.store.spec.ts
```

Expected: FAIL, store not found.

- [ ] **Step 3: Write the store**

`@Injectable({ providedIn: 'root' })`. Hold state in a private object. Expose `snapshot()`, `phase$` / `state$` as `BehaviorSubject`. `apply` is a pure switch on `event.type`. `start` copies the request, sets `phase = 'planning'`, and zeros `places`, `stops`, `spec`, `clarification`, `error`. Provide `orderedStops()` that sorts by `dayIndex`, `stopIndex`.

Also add `PlanningSessionStore.run(client: PlanClient, request: PlanRequest, signal?: AbortSignal): Promise<void>` that calls `start`, then `for await (const event of client.stream(request, signal)) apply(event)`. Tests may call `apply` directly.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/core/state/planning-session.store.spec.ts
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/app/core/state
git commit -m "feat: merge planning events in a session store"
```

---

### Task 17: Map, chat, itinerary, and plan workspace

**Files:**
- Create: `travelfinder-web/src/app/features/map/map.component.ts` (move from `components/map`)
- Create: `travelfinder-web/src/app/features/map/map.component.html`
- Create: `travelfinder-web/src/app/features/map/map.component.spec.ts`
- Create: `travelfinder-web/src/app/features/chat/chat-panel.component.ts`
- Create: `travelfinder-web/src/app/features/plan/itinerary-cards.component.ts`
- Modify: `travelfinder-web/src/app/pages/plan/plan.page.ts`
- Modify: `travelfinder-web/src/app/pages/plan/plan.page.html`
- Modify: `travelfinder-web/src/app/pages/plan/plan.page.spec.ts`

**Interfaces:**
- Consumes: `PlanningSessionStore`, `PlanClient`, `Place[]`
- Produces: plan workspace that streams `/plan/stream`, plots incremental graphics, merges itinerary cards

Map inputs:

```typescript
@Input() places: Place[] = [];
@Input() selectedId: string | null = null;
@Input() stopOrder: string[] = [];
@Output() ready = new EventEmitter<void>();
@Output() placeSelect = new EventEmitter<string>();
@Output() mapPick = new EventEmitter<GeoPoint>();
```

Graphics layer id `places`. On `places` input change, add or update graphics by `place.id`; do not `removeAll` unless the new array is empty after a store reset. Draw a `Polyline` in `stopOrder` when length ≥ 2. Click popup shows `name` + `address`. Do not call Google Places from the browser. Do not call ArcGIS Route/Network Analyst this slice.

- [ ] **Step 1: Write the failing map incremental test**

`features/map/map.component.spec.ts` must not import `IonicModule`. Test the incremental merge helper, not the ArcGIS view (jsdom cannot load the AMD build reliably). Extract `mergePlaceGraphics(existingIds: string[], incoming: Place[]): { add: Place[]; keep: string[] }` in `features/map/place-graphics.ts` and test that:

```typescript
import { mergePlaceGraphics } from './place-graphics';
import { Place } from '../../core/domain/models';

const p = (id: string): Place => ({
  id, source: 'google', sourceId: id, name: id, categories: [], location: { latitude: 1, longitude: 2 }, score: 0
});

describe('mergePlaceGraphics', () => {
  it('keeps existing ids and adds new ones', () => {
    const result = mergePlaceGraphics(['google:1'], [p('google:1'), p('google:2')]);
    expect(result.keep).toEqual(['google:1']);
    expect(result.add.map(x => x.id)).toEqual(['google:2']);
  });
});
```

`plan.page.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PlanPage } from './plan.page';
import { PlanClient } from '../../core/api/plan-client';
import { PlanningSessionStore } from '../../core/state/planning-session.store';

describe('PlanPage', () => {
  it('has no ion- or ui5- elements and sends through PlanClient', async () => {
    const stream = async function* () {
      yield { type: 'plan_spec' as const, spec: {
        language: 'en-us', areaLabel: 'Singapore', radiusMeters: 5000, categories: [], pointOfInterests: [],
        budgetLevel: 'moderate' as const, dayCount: 1, notes: ''
      }};
      yield { type: 'done' as const };
    };

    const client = jasmine.createSpyObj<PlanClient>('PlanClient', ['stream']);
    client.stream.and.returnValue(stream());

    await TestBed.configureTestingModule({
      imports: [PlanPage],
      providers: [
        provideRouter([]),
        PlanningSessionStore,
        { provide: PlanClient, useValue: client }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(PlanPage);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ion-content')).toBeNull();
    expect(html.querySelector('ui5-textarea')).toBeNull();
    expect(html.querySelector('textarea')).toBeTruthy();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/features/map/map.component.spec.ts --include=src/app/pages/plan/plan.page.spec.ts
```

Expected: FAIL (old plan page still has `ui5-textarea`).

- [ ] **Step 3: Implement the workspace**

`place-graphics.ts` as specified.

Move map component to `features/map`. Replace `@Input() layers` with `places` / `selectedId` / `stopOrder`. Keep `@arcgis/core` `WebMap` + `MapView` + `GraphicsLayer`. `ngOnChanges` calls `mergePlaceGraphics`, adds `Graphic` points, updates the polyline, and does not drop graphics already drawn. Geolocation is not Capacitor: if the parent asks for a pick (`needsMapPick`), the next map click emits `mapPick`.

`chat-panel.component.ts`: lists `messages`, shows clarification text, a phase label (`planning` / `clarifying` / …), a textarea, and `ui-button` Send. Emits `send(text)`.

`itinerary-cards.component.ts`: groups `orderedStops()` by `dayIndex`. Each card shows `name`, `reason`, `durationMinutes`. Click emits `select(placeId)`.

`plan.page.html` layout:

```html
<section class="mx-auto grid min-h-[calc(100vh-3.5rem)] max-w-6xl grid-cols-1 lg:grid-cols-2">
  <aside class="flex flex-col border-b border-slate-200 lg:border-b-0 lg:border-r">
    <app-chat-panel ...></app-chat-panel>
    <app-itinerary-cards ...></app-itinerary-cards>
  </aside>
  <app-map [places]="store.snapshot().places" [stopOrder]="stopIds" (placeSelect)="..." (mapPick)="..."></app-map>
</section>
```

`lg` is 1024 px; acceptance widths are 375 px (stacked) and 1280 px (side by side). Use `lg:grid-cols-2` so 1280 is two panes.

`plan.page.ts` on init: read `tf.pending` from `sessionStorage`. If present, `store.run(client, request)`. Subsequent chat sends append the user message and resend the full `messages` list (clarification turns). Coordinates come from pending geolocation or `mapPick`. `requestId` is reused until the user starts a new plan from Home.

Delete Ionic `ModalController`, `UI5Module`, `ApiService`, `best-effort-json-parser`, and `FeatureLayerService.applyEdits` usage from this page. User-contributed writes are out of scope.

- [ ] **Step 4: Run tests and make sure they pass**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/features/map/map.component.spec.ts --include=src/app/pages/plan/plan.page.spec.ts --include=src/app/core/state/planning-session.store.spec.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web/src/app/features travelfinder-web/src/app/pages/plan travelfinder-web/src/app/components/map
git commit -m "feat: wire plan workspace to typed events, map, and itinerary"
```

---

### Task 18: Detail page, strip Ionic/UI5, definition of done

**Files:**
- Create: `travelfinder-web/src/app/pages/detail/detail.page.ts`
- Create: `travelfinder-web/src/app/pages/detail/detail.page.html`
- Create: `travelfinder-web/src/app/pages/detail/detail.page.spec.ts`
- Modify: `travelfinder-web/src/app/app.routes.ts` — `detail` loads `DetailPage`
- Modify: `travelfinder-web/package.json` — remove `@ionic/angular`, `@ui5/webcomponents-ngx`, `@ui5/webcomponents-icons`, `ionicons`, `@ionic/angular-toolkit` from the shipped app if no remaining imports
- Modify leftover pages only if they are still routed

**Interfaces:**
- Consumes: `PlanningSessionStore`
- Produces: read-only detail of selected stop / place; every routed page has zero `ion-` / `ui5-`

- [ ] **Step 1: Write the failing detail spec**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DetailPage } from './detail.page';
import { PlanningSessionStore } from '../../core/state/planning-session.store';

describe('DetailPage', () => {
  it('renders store itinerary without ion- or ui5-', async () => {
    const store = new PlanningSessionStore();
    store.start({ messages: [], latitude: 1, longitude: 2, requestId: 'r1' });
    store.apply({
      type: 'plan_delta',
      stop: { dayIndex: 0, stopIndex: 0, placeId: 'google:1', name: 'Fort Canning', reason: 'Shade' }
    });

    await TestBed.configureTestingModule({
      imports: [DetailPage],
      providers: [provideRouter([]), { provide: PlanningSessionStore, useValue: store }]
    }).compileComponents();

    const fixture = TestBed.createComponent(DetailPage);
    fixture.detectChanges();
    const html = fixture.nativeElement as HTMLElement;
    expect(html.querySelector('ion-header')).toBeNull();
    expect(html.querySelector('ui5-dynamic-page')).toBeNull();
    expect(html.textContent).toContain('Fort Canning');
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

```bash
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless --include=src/app/pages/detail/detail.page.spec.ts
```

Expected: FAIL, `DetailPage` not found.

- [ ] **Step 3: Implement detail and strip leftover UI kits from shipped routes**

Detail is a Tailwind article: back link to `/plan`, spec summary (`areaLabel`, `dayCount`, `budgetLevel`), ordered stop list from the store. No hint flow, no `ApplyEdits`, no `/chat/hint`.

Point `app.routes.ts` `detail` at this page.

Grep shipped templates and specs:

```bash
rg -n "ion-|ui5-|IonicsModule|UI5Module|provideIonicAngular|IonicModule" travelfinder-web/src/app/pages travelfinder-web/src/app/app.component.ts travelfinder-web/src/app/app.routes.ts travelfinder-web/src/main.ts travelfinder-web/src/app/features travelfinder-web/src/app/ui
```

Expected after the fix: no matches in those shipped paths. `pages/menu`, `components/plan-detail`, `components/plan-item`, and `services/api.service.ts` may remain in the tree only if nothing routed imports them. Prefer leaving them unreferenced over a drive-by delete of frozen code.

If `@ionic/angular` and `@ui5/webcomponents-ngx` have zero remaining imports under `src/`, remove them from `package.json` and run `npm install`. Keep Capacitor packages.

Add a one-file manual script at `docs/superpowers/plans/2026-09-20-planning-stream-manual.md`:

```markdown
# Manual planning loop

1. Run API on http://localhost:1294 and web on http://localhost:4200.
2. Open /home at 375px and at 1280px.
3. Allow geolocation or pick a point on the map.
4. Send "one park day near me".
5. Confirm chat shows a spec or a clarification; if clarification, answer and resend.
6. Confirm the map plots points without wiping on the second places payload.
7. Confirm itinerary cards fill from plan_delta.
8. Confirm /detail shows the same stops.
9. With AZUREAI_API_KEY empty and XAI_API_KEY set, repeat once and confirm the loop still completes.
```

Cypress/Playwright is not a delivery gate.

- [ ] **Step 4: Run the verification suite**

```bash
dotnet test travelfinder-api/TravelfinderAPI.sln -v n
cd travelfinder-web && npx ng test --watch=false --browsers=ChromeHeadless
rg -n "ion-|ui5-" travelfinder-web/src/app/pages travelfinder-web/src/app/features travelfinder-web/src/app/app.component.html travelfinder-web/src/app/ui
rg -n "AzureAIApiClient|xAIApiClient|OpenAIApiClient|GmpGisApiClient|ArcGisApiClient" travelfinder-api/Agents travelfinder-api/Gis travelfinder-api/Host travelfinder-api/Controllers/PlanController.cs
rg -n "streamcommand|/chat/" travelfinder-web/src/app/core travelfinder-web/src/app/pages travelfinder-web/src/app/features
```

Expected:

- All backend tests PASS
- Shipped frontend specs PASS
- No `ion-` / `ui5-` in shipped page/feature templates
- No frozen `*ApiClient` types under Agents / Gis / Host / `PlanController`
- No `/chat/` calls from new UI

`ChatController` still builds: `dotnet build travelfinder-api/TravelfinderAPI.csproj`.

- [ ] **Step 5: Commit**

```bash
git add travelfinder-web travelfinder-api docs/superpowers/plans/2026-09-20-planning-stream-manual.md
git commit -m "feat: finish Tailwind pages and freeze leftover Ionic chat UI"
```

---

## Self-review

### Spec coverage

| Spec section | Task |
|---|---|
| `POST /plan/stream` + typed SSE events | 1, 8, 9, 15 |
| Request shape; no `systemId`; no client place JSON | 1, 8, 15 |
| Planner clarification vs spec; no nearby on clarification | 9, 11 |
| Host fan-out `get_merged_places` | 7, 9 |
| Renderer uses only provided `Place[]` | 12 |
| Azure → one xAI retry; `providerUsed` on `done` | 10, 13 |
| 45 s deadline; keep partial spec/places | 9 |
| Unified `Place`; Google + ArcGIS providers | 1, 5, 6 |
| Dedupe, score, cache keys, partial GIS failure | 2, 3, 4, 7 |
| `HttpClient` singleton + `ENABLE_PROXY` | 5, 13 |
| Angular shell + Tailwind; no Ionic/UI5 on shipped pages | 14, 17, 18 |
| `PlanClient` + session store + incremental map | 15, 16, 17 |
| Detail read-only; no `ApplyEdits` / saved itineraries | 18 |
| Tests listed in spec §8.3 | 2–12, 15–17 |
| `ChatController` still builds; new UI does not call it | 13, 18 |
| Frozen `*ApiClient` unused by new business code | 13, 18 |

### Placeholder scan

No TBD / TODO / "implement later" / "similar to Task N" without code. Test bodies and commands are spelled out.

### Type consistency

- `Place.Id` is `${source}:${sourceId}` with sources `google` \| `arcgis` on both sides.
- `PlanSpec` fields match the spec (`pointOfInterests`, `radiusMeters`, `dayCount`, `budgetLevel`).
- Event names: `clarification`, `plan_spec`, `places`, `plan_delta`, `error`, `done`.
- `plan_delta` merges by `dayIndex` + `stopIndex` on backend `ItineraryStop` and frontend `ItineraryStop`.
- `IPlanner.PlanAsync` / `IRenderer.RenderAsync` / `IPlaceService.GetMergedPlaces` are the only ports Host uses.
- `IModelFailover.LastProviderUsed` is `"azure"` or `"xai"`.
- Session phases: `idle → planning → clarifying | retrieving → rendering → done | error`.

