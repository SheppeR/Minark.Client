using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using Minark.Client.Helpers;
using Minark.Shared.Packets;
using ReactiveUI;

namespace Minark.Client.Services;

/// <summary>
///     Service singleton qui détient le statut courant de l'utilisateur connecté.
///     Le fichier de persistance vit dans le dossier du profil courant.
/// </summary>
public class UserStatusService : ReactiveObject
{
    private const string FileName = "userstatus.json";
    private readonly ProfileService _profile;
    private readonly ObservableAsPropertyHelper<string> _statusText;

    public UserStatusService(ProfileService profile)
    {
        _profile = profile;

        Status = LoadPersistedStatus(_profile.GetFilePath(FileName));

        // StatusText dérivé de Status — plus de RaisePropertyChanged manuel
        this.WhenAnyValue(x => x.Status)
            .Select(s => s.ToText())
            .ToProperty(this, x => x.StatusText, out _statusText);

        // Persistance + event externe sur changement de statut
        this.WhenAnyValue(x => x.Status)
            .Subscribe(s => { PersistStatus(_profile.GetFilePath(FileName), s); });

        _profile.ProfileSwitched += () =>
            Status = LoadPersistedStatus(_profile.GetFilePath(FileName));
    }

    public UserStatus Status
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string StatusText => _statusText.Value;

    private static UserStatus LoadPersistedStatus(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return UserStatus.Online;
            }

            var val = JsonSerializer.Deserialize<int>(File.ReadAllText(path));
            return Enum.IsDefined(typeof(UserStatus), val) ? (UserStatus)val : UserStatus.Online;
        }
        catch
        {
            return UserStatus.Online;
        }
    }

    private static void PersistStatus(string path, UserStatus status)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize((int)status));
        }
        catch
        {
            /* non bloquant */
        }
    }
}