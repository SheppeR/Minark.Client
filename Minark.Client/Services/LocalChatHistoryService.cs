using System.IO;
using System.Text.Json;
using Minark.Shared.Packets.Chat;

namespace Minark.Client.Services;

/// <summary>
///     Sauvegarde l'historique des conversations en JSON local dans AppData.
///     Structure : %AppData%\Minark\Chat\{myUsername}\{friendUsername}.json
/// </summary>
public class LocalChatHistoryService
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string GetPath(string myUsername, string friendUsername)
    {
        // Utilise %LOCALAPPDATA%\Minark (cohérent avec ProfileService.RootDirectory)
        var dir = Path.Combine(ProfileService.RootDirectory, "Chat", myUsername);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{friendUsername}.json");
    }

    /// <summary>Charge l'historique local. Retourne une liste vide si absent.</summary>
    public List<ChatMessageDto> Load(string myUsername, string friendUsername)
    {
        try
        {
            var path = GetPath(myUsername, friendUsername);
            if (!File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<ChatMessageDto>>(json, _opts) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Sauvegarde la liste complète (merge + tri déduplication).</summary>
    public void Save(string myUsername, string friendUsername, IEnumerable<ChatMessageDto> messages)
    {
        try
        {
            var path = GetPath(myUsername, friendUsername);

            // Charger l'existant et fusionner
            var existing = Load(myUsername, friendUsername);
            // messages (serveur) d'abord : en cas de doublon, l'entrée serveur
            // est conservée car elle contient les réactions à jour.
            var merged = messages
                .Concat(existing)
                .GroupBy(m => m.Id > 0
                    ? $"id:{m.Id}"
                    : $"ts:{m.SentAt.Ticks}|{m.FromUsername}|{m.Content}")
                .Select(g =>
                {
                    // Version serveur en priorité, mais si elle n'a pas de réactions
                    // et que le cache local en a, on les préserve
                    var server = g.First();
                    if (server.Reactions.Count == 0)
                    {
                        var cached = g.Skip(1).FirstOrDefault();
                        if (cached?.Reactions.Count > 0)
                        {
                            server.Reactions = cached.Reactions;
                        }
                    }

                    return server;
                })
                .OrderBy(m => m.SentAt)
                .ToList();

            File.WriteAllText(path, JsonSerializer.Serialize(merged, _opts));
        }
        catch
        {
            /* non bloquant */
        }
    }

    /// <summary>Ajoute un seul message à l'historique local.</summary>
    public void Append(string myUsername, string friendUsername, ChatMessageDto message)
    {
        try
        {
            var existing = Load(myUsername, friendUsername);
            // Éviter les doublons (priorité sur l'Id DB, fallback sur contenu)
            var isDuplicate = message.Id > 0
                ? existing.Any(m => m.Id == message.Id)
                : existing.Any(m => m.SentAt == message.SentAt &&
                                    m.FromUsername == message.FromUsername &&
                                    m.Content == message.Content);
            if (!isDuplicate)
            {
                existing.Add(message);
                existing.Sort((a, b) => a.SentAt.CompareTo(b.SentAt));
                var path = GetPath(myUsername, friendUsername);
                File.WriteAllText(path, JsonSerializer.Serialize(existing, _opts));
            }
        }
        catch
        {
            /* non bloquant */
        }
    }
}