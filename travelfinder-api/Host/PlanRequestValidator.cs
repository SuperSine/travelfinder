using TravelfinderAPI.Contracts;

namespace TravelfinderAPI.Host;

public static class PlanRequestValidator
{
    public static ErrorPayload? TryValidate(PlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return new ErrorPayload
            {
                Code = PlanErrorCode.Validation,
                Message = "requestId is required.",
                RequestId = request.RequestId,
                Retryable = false
            };
        }

        if (request.Messages.Count == 0 ||
            request.Messages.All(message => string.IsNullOrWhiteSpace(message.Content)))
        {
            return new ErrorPayload
            {
                Code = PlanErrorCode.Validation,
                Message = "messages must not be empty.",
                RequestId = request.RequestId,
                Retryable = false
            };
        }

        if (!IsFiniteCoordinate(request.Latitude, -90, 90) ||
            !IsFiniteCoordinate(request.Longitude, -180, 180))
        {
            return new ErrorPayload
            {
                Code = PlanErrorCode.Validation,
                Message = "coordinates are required.",
                RequestId = request.RequestId,
                Retryable = false
            };
        }

        return null;
    }

    private static bool IsFiniteCoordinate(double value, double min, double max) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= min && value <= max;
}
