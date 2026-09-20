using TravelfinderAPI.Contracts;
using TravelfinderAPI.Gis;
using Xunit;

namespace TravelfinderAPITests.Gis;

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
