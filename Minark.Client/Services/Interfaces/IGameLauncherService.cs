// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable EventNeverSubscribedTo.Global
// ReSharper disable NotAccessedPositionalProperty.Global

#pragma warning disable IDE0051

namespace Minark.Client.Services.Interfaces;

public interface IGameLauncherService
{
    bool IsRunning { get; }

    /// <summary>Lance le jeu compilé en passant le token via --minark-token.</summary>
    Task<GameLaunchResult> LaunchAsync();

    /// <summary>Déclenché quand le process jeu se ferme.</summary>
    event Action? GameExited;
}

public record GameLaunchResult(bool Success, string? ErrorMessage = null);