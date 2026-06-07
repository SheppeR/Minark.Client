namespace Minark.Game.Shared.Packets;

public enum GamePacketType : byte
{
    // ── Auth (Reliable) ───────────────────────────────────────────────────────
    AuthRequest = 1, // Unity → GameServer : token launcher
    AuthResponse = 2, // GameServer → Unity : user + ok/ko

    // ── Friends (Reliable) ───────────────────────────────────────────────────
    FriendListRequest = 10, // Unity → GameServer : demande liste amis
    FriendListResponse = 11, // GameServer → Unity : liste amis + statuts
    FriendStatusUpdate = 12, // GameServer → Unity : un ami change de statut

    // ── Presence (Reliable) ──────────────────────────────────────────────────
    PlayerJoined = 20, // GameServer → room : un joueur rejoint
    PlayerLeft = 21, // GameServer → room : un joueur quitte

    // ── World / Gameplay (Unreliable) ────────────────────────────────────────
    // Ces paquets sont envoyés à haute fréquence, perte acceptable
    PlayerMove = 30, // Unity → GameServer : position locale
    PlayerMoveSync = 31, // GameServer → Unity : positions de tous

    // ── System (Reliable) ────────────────────────────────────────────────────
    Ping = 200,
    Pong = 201,
    Disconnect = 202,
    Error = 203
}