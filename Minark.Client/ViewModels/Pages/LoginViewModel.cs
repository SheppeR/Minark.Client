using System.Reactive;
using System.Reactive.Linq;
using System.Windows;
using Minark.Client.Networking;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.Views.Pages;
using ReactiveUI;

namespace Minark.Client.ViewModels.Pages;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthClientService _auth;
    private readonly ICredentialsService _credentials;
    private readonly INavigationService _nav;
    private readonly ProfileService _profile;
    private readonly ReconnectionService _reconnection;

    public LoginViewModel(
        IAuthClientService auth,
        TcpClientService tcp,
        INavigationService nav,
        ICredentialsService credentials,
        ProfileService profile,
        ReconnectionService reconnection)
    {
        _auth = auth;
        _nav = nav;
        _credentials = credentials;
        _profile = profile;
        _reconnection = reconnection;

        tcp.OnConnected += () => { Application.Current.Dispatcher.Invoke(() => IsConnected = true); };
        tcp.OnDisconnected += () =>
            Application.Current.Dispatcher.Invoke(() => IsConnected = false);
        tcp.OnConnectionFailed += () =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = false;
                IsLoading = false;
                ErrorMessage = "Impossible de joindre le serveur.";
            });
        IsConnected = tcp.IsConnected;


        var saved = credentials.Load();
        if (saved.RememberMe)
        {
            Username = saved.Username;
            Password = saved.Password;
            RememberMe = true;
        }
        else if (!string.IsNullOrEmpty(saved.Username))
        {
            Username = saved.Username;
        }

        this.WhenAnyValue(x => x.Username)
            .Skip(1)
            .Subscribe(typed =>
            {
                if (string.IsNullOrWhiteSpace(typed))
                {
                    return;
                }

                var match = credentials.Load(typed);
                if (match.RememberMe && !string.IsNullOrEmpty(match.Password))
                {
                    Password = match.Password;
                    RememberMe = true;
                }
            });

        this.WhenAnyValue(x => x.ErrorMessage)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(HasError)));

        var canLogin = this.WhenAnyValue(
            x => x.Username,
            x => x.Password,
            x => x.IsLoading,
            x => x.IsConnected,
            (u, p, loading, connected) => !string.IsNullOrWhiteSpace(u) &&
                                          !string.IsNullOrWhiteSpace(p) &&
                                          !loading &&
                                          connected);

        LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync, canLogin);
        GoToRegisterCommand = ReactiveCommand.Create(GoToRegister);
    }

    public string Username
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool RememberMe
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsConnected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ErrorMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public IReadOnlyList<string> SavedUsernames => _credentials.GetSavedUsernames();


    public ReactiveCommand<Unit, Unit> LoginCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToRegisterCommand { get; }

    private async Task LoginAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            if (!IsConnected)
            {
                ErrorMessage = "Non connecté au serveur.";
                return;
            }

            var result = await _auth.LoginAsync(Username, Password);
            if (result.Success)
            {
                _profile.SwitchToUserProfile(Username);
                _reconnection.NotifyUserLoggedIn();

                if (RememberMe)
                {
                    _credentials.Save(Username, Password, true);
                }
                else
                {
                    _credentials.Clear();
                }

                _nav.NavigateTo<ShellView>();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Identifiants incorrects.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GoToRegister()
    {
        _nav.NavigateTo<RegisterView>();
    }
}