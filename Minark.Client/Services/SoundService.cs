using System.IO;
using System.Media;
using System.Windows.Media;
using Minark.Client.Helpers;

namespace Minark.Client.Services;

/// <summary>
///     Joue les sons de notification de manière non-bloquante.
///     Utilise un son .wav embarqué ou, à défaut, le son système Windows.
/// </summary>
public class SoundService
{
    private readonly MediaPlayer _player = new();

    public bool Enabled { get; set; } = true;

    public void PlayMessageReceived()
    {
        if (!Enabled)
        {
            return;
        }

        PlayAsync("message.wav", SystemSounds.Asterisk);
    }

    public void PlayNotification()
    {
        if (!Enabled)
        {
            return;
        }

        PlayAsync("notification.wav", SystemSounds.Asterisk);
    }

    private void PlayAsync(string fileName, SystemSound fallback)
    {
        Task.Run(() =>
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Sounds", fileName);
                if (File.Exists(path))
                {
                    // MediaPlayer doit être sur le thread STA
                    UiThread.Invoke(() =>
                    {
                        _player.Open(new Uri(path, UriKind.Absolute));
                        _player.Volume = 0.6;
                        _player.Play();
                    });
                }
                else
                {
                    fallback.Play();
                }
            }
            catch
            {
                // Ne jamais crasher à cause du son
            }
        });
    }
}