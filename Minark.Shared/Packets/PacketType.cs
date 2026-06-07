namespace Minark.Shared.Packets;

public enum PacketType
{
    // Auth
    ChallengeRequest,
    ChallengeResponse,
    LoginRequest,
    LoginResponse,
    RegisterRequest,
    RegisterResponse,
    LogoutRequest,
    LogoutResponse,

    // Friends
    FriendListRequest,
    FriendListResponse,
    FriendRequestSend,
    FriendRequestResponse,
    FriendRemove,
    FriendStatusUpdate,
    FriendListChanged,
    FriendInviteReceived,
    FriendInviteAccept,
    FriendInviteDecline,

    // News
    NewsListRequest,
    NewsListResponse,
    NewsChanged,
    NewsUpsertRequest,
    NewsUpsertResponse,
    NewsDeleteRequest, // push serveur → tous les clients connectés

    // Status
    StatusUpdateRequest, // client → serveur : changer son propre statut
    SelfStatusUpdate, // serveur → client : le serveur notifie le client de son propre statut (ex: InGame via GameServer)

    // Chat
    ChatSend,
    ChatReceive,
    ChatHistoryRequest,
    ChatHistoryResponse,
    UnreadCountsRequest,
    UnreadCountsResponse,
    MarkAsReadRequest,

    // News interactions
    NewsReactRequest,
    NewsReactResponse,
    NewsCommentsRequest,
    NewsCommentsResponse,
    NewsPostCommentRequest,
    NewsPostCommentResponse,
    NewsStatsUpdated,

    // Typing
    TypingStart,
    TypingStop,

    // Block
    BlockUser,
    UnblockUser,
    BlockResponse,
    BlockListRequest,
    BlockListResponse,

    // Profile
    ChangePasswordRequest,
    ChangePasswordResponse,
    UpdateAvatarRequest,
    UpdateAvatarResponse,

    // Chat actions
    ChatDeleteRequest, // client → serveur : supprimer un message
    ChatDeleteNotify, // serveur → les deux clients : message supprimé
    ChatEditRequest, // client → serveur : éditer un message
    ChatEditNotify, // serveur → les deux clients : message édité
    ChatSearchRequest, // client → serveur : rechercher dans l'historique
    ChatSearchResponse, // serveur → client : résultats de recherche

    // Réactions aux messages
    ChatReactRequest, // client → serveur : ajouter/retirer une réaction
    ChatReactNotify, // serveur → les deux clients : réaction mise à jour

    // General
    Ping,
    Pong,
    Error,

    // Session lifecycle
    SessionInvalidated // serveur → client : session invalidée (double login ailleurs)
}