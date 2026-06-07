using System.Reactive;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.Views.Pages;
using Minark.Shared;
using ReactiveUI;
using Serilog;

namespace Minark.Client.ViewModels.Pages;

public class RegisterViewModel : ViewModelBase
{
    private readonly IAuthClientService _auth;
    private readonly INavigationService _nav;

    public RegisterViewModel(IAuthClientService auth, INavigationService nav)
    {
        _auth = auth;
        _nav = nav;

        this.WhenAnyValue(x => x.ErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasError)));
        this.WhenAnyValue(x => x.SuccessMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasSuccess)));

        var canRegister = this.WhenAnyValue(
            x => x.Username, x => x.Email, x => x.Password, x => x.ConfirmPassword, x => x.IsLoading,
            (u, e, p, c, loading) =>
                !string.IsNullOrWhiteSpace(u) &&
                !string.IsNullOrWhiteSpace(e) &&
                !string.IsNullOrWhiteSpace(p) &&
                !string.IsNullOrWhiteSpace(c) &&
                !loading);

        RegisterCommand = ReactiveCommand.CreateFromTask(RegisterAsync, canRegister);
        GoToLoginCommand = ReactiveCommand.Create(GoToLogin);
    }

    public string Username
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Email
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Password
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string ConfirmPassword
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ErrorMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string SuccessMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToLoginCommand { get; }

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        var usernameCheck = InputValidator.ValidateUsername(Username);
        if (!usernameCheck.IsValid)
        {
            ErrorMessage = usernameCheck.ErrorMessage!;
            return;
        }

        var emailCheck = InputValidator.ValidateEmail(Email);
        if (!emailCheck.IsValid)
        {
            ErrorMessage = emailCheck.ErrorMessage!;
            return;
        }

        var passwordCheck = InputValidator.ValidatePasswordConfirm(Password, ConfirmPassword);
        if (!passwordCheck.IsValid)
        {
            ErrorMessage = passwordCheck.ErrorMessage!;
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _auth.RegisterAsync(Username, Email, Password);
            if (result.Success)
            {
                SuccessMessage = "Compte créé ! Vous pouvez maintenant vous connecter.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Erreur inconnue.";
            }
        }
        catch (TimeoutException)
        {
            ErrorMessage = "Le serveur ne répond pas.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erreur inattendue.";
            Log.Error(ex, "RegisterViewModel error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GoToLogin()
    {
        _nav.NavigateTo<LoginView>();
    }
}