namespace Minark.Client.Services.Interfaces;

/// <summary>
///     Abstrait les boîtes de dialogue OS (OpenFileDialog, SaveFileDialog…)
///     pour garder les ViewModels testables et sans dépendances UI.
/// </summary>
public interface IDialogService
{
    /// <summary>
    ///     Ouvre une boîte de dialogue de sélection de fichier.
    ///     Retourne le chemin complet sélectionné, ou null si annulé.
    /// </summary>
    string? OpenFile(string title, string filter);
}