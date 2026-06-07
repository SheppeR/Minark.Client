namespace Minark.Shared.Packets.Chat;

public class ChatMessageDto
{
    public int Id { get; init; } // identifiant DB — requis pour delete/edit/react
    public string FromUsername { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public bool IsOwn { get; init; }
    public bool IsDeleted { get; init; } // message supprimé (contenu masqué côté client)
    public bool IsEdited { get; init; } // affiche "(modifié)" sous le message
    public List<ReactionDto> Reactions { get; set; } = [];

    public string BubbleAlign => IsOwn ? "Right" : "Left";
    public string TimeAlign => IsOwn ? "Right" : "Left";
}