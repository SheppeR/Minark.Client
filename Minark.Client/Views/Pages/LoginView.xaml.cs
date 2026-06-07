using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class LoginView
{
    private const int NodeCount = 32;
    private const double ConnectDistance = 140;
    private const double MouseInfluenceRadius = 160;
    private const double MouseRepelStrength = 110;

    private readonly DispatcherTimer _mouseTimer;
    private readonly List<ConstellationNode> _nodes = [];
    private readonly Random _rng = new();
    private Point _mousePos;
    private ScaleTransform? _orbitSystemScale;
    private TranslateTransform? _orbitSystemTranslate;
    private Point _smoothMouse;

    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        // PasswordBox is bound via PasswordBoxBehavior in XAML
        PasswordBox.PasswordChanged += (_, _) => vm.Password = PasswordBox.Password;

        _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _mouseTimer.Tick += OnMouseTick;

        Loaded += (_, _) =>
        {
            _mousePos = _smoothMouse = new Point(BrandPanel.ActualWidth / 2, BrandPanel.ActualHeight / 2);
            SyncOrbitCanvas();
            BuildConstellation();
            SetupOrbitMouseTransform();
            _mouseTimer.Start();
        };
        Unloaded += (_, _) => _mouseTimer.Stop();
        SizeChanged += (_, _) => BuildConstellation();
        BrandPanel.MouseMove += (_, e) => _mousePos = e.GetPosition(BrandPanel);
        BrandPanel.MouseLeave += (_, _) => _mousePos = new Point(BrandPanel.ActualWidth / 2, BrandPanel.ActualHeight / 2);
    }

    private void SetupOrbitMouseTransform()
    {
        if (FindName("OrbitSystem") is not Grid orbitGrid)
        {
            return;
        }

        _orbitSystemTranslate = new TranslateTransform();
        _orbitSystemScale = new ScaleTransform(1, 1, 0.5, 0.5);
        orbitGrid.RenderTransform = new TransformGroup
        {
            Children = [_orbitSystemScale, _orbitSystemTranslate]
        };
    }

    private void OnMouseTick(object? sender, EventArgs e)
    {
        var w = BrandPanel.ActualWidth;
        var h = BrandPanel.ActualHeight;
        if (w < 1 || h < 1)
        {
            return;
        }

        const double lerp = 0.10;
        _smoothMouse = new Point(
            _smoothMouse.X + (_mousePos.X - _smoothMouse.X) * lerp,
            _smoothMouse.Y + (_mousePos.Y - _smoothMouse.Y) * lerp);

        var nx = (_smoothMouse.X - w / 2) / (w / 2);
        var ny = (_smoothMouse.Y - h / 2) / (h / 2);

        if (_orbitSystemTranslate != null)
        {
            _orbitSystemTranslate.X = nx * 55;
            _orbitSystemTranslate.Y = ny * 35;
        }

        if (_orbitSystemScale != null)
        {
            var dist = Math.Sqrt(nx * nx + ny * ny);
            _orbitSystemScale.ScaleX = _orbitSystemScale.ScaleY = 1.0 + dist * 0.07;
        }

        ApplyMouseRepulsion(w, h);
    }

    private void ApplyMouseRepulsion(double w, double h)
    {
        foreach (var node in _nodes)
        {
            if (node.Shape is null)
            {
                continue;
            }

            var cx = node.X + (Canvas.GetLeft(node.Shape) - (node.X - node.Size));
            var cy = node.Y + (Canvas.GetTop(node.Shape) - (node.Y - node.Size));
            var dx = cx - _smoothMouse.X;
            var dy = cy - _smoothMouse.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (node.Shape.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                node.Shape.RenderTransform = tt;
            }

            if (dist is < MouseInfluenceRadius and > 0.1)
            {
                var force = (1.0 - dist / MouseInfluenceRadius) * MouseRepelStrength;
                var angle = Math.Atan2(dy, dx);
                tt.X += (node.X - node.Size + Math.Cos(angle) * force - (node.X - node.Size) - tt.X) * 0.18;
                tt.Y += (node.Y - node.Size + Math.Sin(angle) * force - (node.Y - node.Size) - tt.Y) * 0.18;
            }
            else
            {
                tt.X *= 0.90;
                tt.Y *= 0.90;
            }
        }
    }

    private void SyncOrbitCanvas()
    {
        StartRingAnimation("Ring1Rotate2", 0, 360, 18);
        StartRingAnimation("Ring2Rotate2", 360, 0, 12);
    }

    private void StartRingAnimation(string targetName, double from, double to, double seconds)
    {
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var a = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromSeconds(seconds) };
        Storyboard.SetTargetName(a, targetName);
        Storyboard.SetTargetProperty(a, new PropertyPath(RotateTransform.AngleProperty));
        sb.Children.Add(a);
        sb.Begin(this);
    }

    private void BuildConstellation()
    {
        var w = BrandPanel.ActualWidth;
        var h = BrandPanel.ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        _nodes.Clear();
        ConstellationCanvas.Children.Clear();

        for (var i = 0; i < NodeCount; i++)
        {
            _nodes.Add(new ConstellationNode
            {
                X = _rng.NextDouble() * w,
                Y = _rng.NextDouble() * h,
                Size = _rng.NextDouble() * 2.5 + 1.0
            });
        }

        // Draw edges
        for (var i = 0; i < _nodes.Count; i++)
        for (var j = i + 1; j < _nodes.Count; j++)
        {
            var dx = _nodes[i].X - _nodes[j].X;
            var dy = _nodes[i].Y - _nodes[j].Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > ConnectDistance)
            {
                continue;
            }

            var opacity = (1.0 - dist / ConnectDistance) * 0.3;
            ConstellationCanvas.Children.Add(new Line
            {
                X1 = _nodes[i].X, Y1 = _nodes[i].Y,
                X2 = _nodes[j].X, Y2 = _nodes[j].Y,
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0x7C, 0x3A, 0xED)),
                StrokeThickness = 0.6
            });
        }

        // Draw nodes + animate
        foreach (var node in _nodes)
        {
            var dot = new Ellipse
            {
                Width = node.Size * 2, Height = node.Size * 2,
                Fill = new SolidColorBrush(Color.FromArgb(180, 0xA7, 0x8B, 0xFA)),
                RenderTransform = new TranslateTransform()
            };
            Canvas.SetLeft(dot, node.X - node.Size);
            Canvas.SetTop(dot, node.Y - node.Size);
            ConstellationCanvas.Children.Add(dot);
            node.Shape = dot;
            dot.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, _rng.NextDouble() * 0.8 + 0.2,
                    TimeSpan.FromSeconds(_rng.NextDouble() * 1.5 + 0.3)));
            AnimateNodeFloat(node, 60, 8, 6);
        }
    }

    private void AnimateNodeFloat(ConstellationNode node, double maxDelta, double maxDuration, double minDuration)
    {
        var duration = _rng.NextDouble() * (maxDuration - minDuration) + minDuration;
        var deltaX = (_rng.NextDouble() - 0.5) * maxDelta;
        var deltaY = (_rng.NextDouble() - 0.5) * maxDelta;
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var begin = TimeSpan.FromSeconds(_rng.NextDouble() * 4);

        node.Shape?.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(
                node.X - node.Size, node.X - node.Size + deltaX, TimeSpan.FromSeconds(duration))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease, BeginTime = begin });
        node.Shape?.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(
                node.Y - node.Size, node.Y - node.Size + deltaY, TimeSpan.FromSeconds(duration * 1.1))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease, BeginTime = begin });
    }

    private sealed class ConstellationNode
    {
        public Ellipse? Shape;
        public double X, Y, Size;
    }
}