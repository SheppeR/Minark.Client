using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Minark.Client.ViewModels;

namespace Minark.Client.Views.Shared;

public partial class ToastWindow
{
    private readonly DispatcherTimer _autoClose;

    public ToastWindow(string title, string body, int durationMs = 4000)
    {
        InitializeComponent();

        var vm = new ToastViewModel(title, body);
        DataContext = vm;
        vm.CloseRequested += AnimateOut;

        _autoClose = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _autoClose.Tick += (_, _) =>
        {
            _autoClose.Stop();
            AnimateOut();
        };

        Loaded += (_, _) =>
        {
            PositionBottomRight();
            AnimateIn();
            _autoClose.Start();
        };
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Bottom - ActualHeight - 16;
    }

    private void AnimateIn()
    {
        ((Storyboard)FindResource("SlideIn")).Begin(this);
    }

    private void AnimateOut()
    {
        var sb = (Storyboard)FindResource("SlideOut");
        sb.Completed += (_, _) => Close();
        sb.Begin(this);
    }
}