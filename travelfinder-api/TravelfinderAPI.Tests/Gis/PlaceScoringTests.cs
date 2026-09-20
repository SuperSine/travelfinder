using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPITests.Gis;

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
        var spec = new PlanSpec { Categories = ["museum"], RadiusMeters = 1000 };
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
