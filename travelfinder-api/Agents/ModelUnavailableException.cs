namespace TravelfinderAPI.Agents;

public sealed class ModelUnavailableException : Exception
{
    public ModelUnavailableException() : base("The planning model is unavailable.") { }

    public ModelUnavailableException(string message) : base(message) { }

    public ModelUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}
