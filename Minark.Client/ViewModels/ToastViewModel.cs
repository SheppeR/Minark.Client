using System.Reactive;
using ReactiveUI;

namespace Minark.Client.ViewModels;

public class ToastViewModel : ReactiveObject
{
    public ToastViewModel(string title, string body)
    {
        Title = title;
        Body = body;
        Initial = title.Length > 0 ? title[0].ToString().ToUpper() : "?";

        CloseCommand = ReactiveCommand.Create(() => CloseRequested?.Invoke());
    }

    public string Title { get; }
    public string Body { get; }
    public string Initial { get; }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    public event Action? CloseRequested;
}