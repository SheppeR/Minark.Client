namespace Minark.Shared.Packets.Chat;

public class ChatSearchRequest
{
    public string Token { get; init; } = string.Empty;
    public string WithUsername { get; init; } = string.Empty;
    public string Query { get; init; } = string.Empty;
}