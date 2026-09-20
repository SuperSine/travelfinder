namespace TravelfinderAPI.Contracts;

public sealed class ChatMessageDto
{
    public required string Role { get; init; }
    public required string Content { get; init; }
}
