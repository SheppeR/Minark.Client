namespace Minark.Game.Shared.Packets.Game;

// ── Présence ──────────────────────────────────────────────────────────────────

public class PlayerJoinedPacket
{
    public int PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class PlayerLeftPacket
{
    public int PlayerId { get; set; }
}

// ── Mouvement (Unreliable — haute fréquence) ─────────────────────────────────

/// <summary>
///     Envoyé par Unity → GameServer à chaque tick client (~20 Hz).
///     Coords float pour économiser la taille du paquet.
/// </summary>
public class PlayerMovePacket
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Rot { get; set; } // rotation Y en degrés
}

/// <summary>
///     Broadcast GameServer → tous les clients de la room, à chaque tick serveur.
///     Contient les positions de tous les joueurs visibles.
/// </summary>
public class PlayerMoveSyncPacket
{
    public PlayerSnapshot[] Players { get; set; } = [];
}

public class PlayerSnapshot
{
    public int PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Rot { get; set; }
    public long Tick { get; set; } // tick serveur pour interpolation
}