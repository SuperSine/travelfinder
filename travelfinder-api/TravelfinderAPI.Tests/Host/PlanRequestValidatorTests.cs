using TravelfinderAPI.Contracts;
using TravelfinderAPI.Host;
using Xunit;

namespace TravelfinderAPITests.Host;

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
