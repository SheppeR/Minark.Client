using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Minark.Shared.Packets.Chat;
using ReactiveUI;

namespace Minark.Client.ViewModels;

/// <summary>
///     ViewModel réactif wrappant ChatMessageDto.
///     Permet aux bindings XAML de se mettre à jour quand un message est
///     édité, supprimé ou reçoit une nouvelle réaction sans recréer la liste.
/// </summary>
public class ChatMessageViewModel : ReactiveObject
{
    private readonly ObservableAsPropertyHelper<string> _displayContent;

    public ChatMessageViewModel(ChatMessageDto dto)
    {
        Id = dto.Id;
        FromUsername = dto.FromUsername;
        Content = dto.Content;
        SentAt = dto.SentAt;
        IsOwn = dto.IsOwn;
        IsDeleted = dto.IsDeleted;
        IsEdited = dto.IsEdited;
        EditBuffer = string.Empty;
        SyncReactions(dto.Reactions);

        // DisplayContent se recalcule quand IsDeleted ou Content changent
        this.WhenAnyValue(x => x.IsDeleted, x => x.Content,
                (deleted, text) => deleted ? "🗑 Message supprimé" : text)
            .ToProperty(this, x => x.DisplayContent, out _displayContent);
    }

    public int Id
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    }

    [field: AllowNull]
    [field: MaybeNull]
    public string FromUsername
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    }

    [field: AllowNull]
    [field: MaybeNull]
    public string Content
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public DateTime SentAt
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsOwn
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsDeleted
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsEdited
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsEditMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    [field: AllowNull]
    [field: MaybeNull]
    public string EditBuffer
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ShowReactionPicker
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<ReactionViewModel> Reactions { get; } = [];

    public string BubbleAlign => IsOwn ? "Right" : "Left";
    public string TimeAlign => IsOwn ? "Right" : "Left";
    public string DisplayContent => _displayContent.Value;

    public void ApplyDelete()
    {
        Content = string.Empty;
        IsDeleted = true;
    }

    public void ApplyEdit(string newContent)
    {
        Content = newContent;
        IsEdited = true;
        IsEditMode = false;
    }

    public void SyncReactions(List<ReactionDto> reactions)
    {
        // Diff intelligent : on met à jour les existants, on ajoute les nouveaux,
        // on retire les disparus — évite le Clear() qui déclenche un CollectionChanged
        // intermédiaire et peut provoquer un état vide visible dans l'UI WPF.

        // 1. Mettre à jour ou ajouter
        foreach (var dto in reactions)
        {
            var existing = Reactions.FirstOrDefault(r => r.Emoji == dto.Emoji);
            if (existing is not null)
            {
                existing.Count = dto.Count;
                existing.HasMine = dto.HasMine;
            }
            else
            {
                Reactions.Add(new ReactionViewModel(dto));
            }
        }

        // 2. Retirer les réactions qui ne sont plus dans la liste du serveur
        var toRemove = Reactions
            .Where(r => reactions.All(d => d.Emoji != r.Emoji))
            .ToList();
        foreach (var r in toRemove)
        {
            Reactions.Remove(r);
        }
    }
}

public class ReactionViewModel : ReactiveObject
{
    public ReactionViewModel(ReactionDto dto)
    {
        Emoji = dto.Emoji;
        Count = dto.Count;
        HasMine = dto.HasMine;
    }

    public string Emoji
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int Count
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasMine
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}