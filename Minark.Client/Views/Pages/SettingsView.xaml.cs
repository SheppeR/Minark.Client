using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class SettingsView
{
    public SettingsView(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // Les PasswordBox ne peuvent pas être vidées depuis le VM (sécurité WPF).
        // On écoute l'event du VM pour les vider après succès.
        vm.PasswordChangedSuccessfully += () =>
        {
            OldPwdBox.Clear();
            NewPwdBox.Clear();
            ConfirmPwdBox.Clear();
        };
    }
}