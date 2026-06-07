namespace Minark.Shared.Packets.Chat;

public class ChatHistoryRequest
{
    public string Token { get; init; } = string.Empty;
    public string WithUsername { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}