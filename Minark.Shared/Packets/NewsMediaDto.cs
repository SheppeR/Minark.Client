namespace Minark.Shared.Packets;

public class NewsMediaDto
{
    public string Url { get; init; } = string.Empty;
    public string MediaType { get; init; } = "image"; // "image" | "video"
}