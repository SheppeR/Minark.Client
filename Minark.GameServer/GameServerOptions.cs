namespace Minark.GameServer;

public class GameServerOptions
{
    public const string Section = "GameServer";

    public string Host { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 9002;
    public int TickRateHz { get; init; } = 20;

    /// <summary>Clé de connexion LiteNetLib (doit matcher côté Unity).</summary>
    public string ConnectionKey { get; init; } = "minark";

    /// <summary>Max joueurs simultanés.</summary>
    public int MaxPlayers { get; init; } = 200;
}