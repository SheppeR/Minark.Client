using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class RegisterView
{
    private const int NodeCount = 28;
    private const double MaxEdgeDist = 160;

    private readonly DispatcherTimer _mouseTimer;
    private readonly List<NeuralNode> _nodes = [];
    private readonly DispatcherTimer _pulseTimer;
    private readonly Random _rng = new();
    private Point _mousePos;
    private Point _smoothMouse;

    public RegisterView(RegisterViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        // PasswordBoxes bound via PasswordBoxBehavior in XAML

        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _pulseTimer.Tick += (_, _) => FireRandomPulse();

        _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _mouseTimer.Tick += OnMouseTick;

        Loaded += (_, _) =>
        {
            _mousePos = _smoothMouse = new Point(RBrandPanel.ActualWidth / 2, RBrandPanel.ActualHeight / 2);
            BuildNetwork();
            _pulseTimer.Start();
            _mouseTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _pulseTimer.Stop();
            _mouseTimer.Stop();
        };
        SizeChanged += (_, _) => BuildNetwork();
        RBrandPanel.MouseMove += (_, e) => _mousePos = e.GetPosition(RBrandPanel);
        RBrandPanel.MouseLeave += (_, _) => _mousePos = new Point(RBrandPanel.ActualWidth / 2, RBrandPanel.ActualHeight / 2);
    }

    private void OnMouseTick(object? sender, EventArgs e)
    {
        const double lerp = 0.08;
        _smoothMouse = new Point(
            _smoothMouse.X + (_mousePos.X - _smoothMouse.X) * lerp,
            _smoothMouse.Y + (_mousePos.Y - _smoothMouse.Y) * lerp);

        foreach (var node in _nodes)
        {
            if (node.Dot is null)
            {
                continue;
            }

            if (node.Dot.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                node.Dot.RenderTransform = tt;
            }

            var dx = node.X - _smoothMouse.X;
            var dy = node.Y - _smoothMouse.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist is < 130 and > 0.5)
            {
                var force = (1 - dist / 130.0) * 70;
                var ang = Math.Atan2(dy, dx);
                tt.X += (Math.Cos(ang) * force - tt.X) * 0.15;
                tt.Y += (Math.Sin(ang) * force - tt.Y) * 0.15;
            }
            else
            {
                tt.X *= 0.88;
                tt.Y *= 0.88;
            }
        }
    }

    private void BuildNetwork()
    {
        var w = RBrandPanel.ActualWidth;
        var h = RBrandPanel.ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        NeuralCanvas.Children.Clear();
        _nodes.Clear();

        var cx = w / 2;
        var cy = h / 2;
        const double centerExclude = 80;

        for (var i = 0; i < NodeCount; i++)
        {
            double x, y;
            do
            {
                x = _rng.NextDouble() * w;
                y = _rng.NextDouble() * h;
            } while (Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) < centerExclude);

            _nodes.Add(new NeuralNode { X = x, Y = y, Size = _rng.NextDouble() * 2.8 + 1.5 });
        }

        // Draw edges
        for (var i = 0; i < _nodes.Count; i++)
        for (var j = i + 1; j < _nodes.Count; j++)
        {
            var dx = _nodes[i].X - _nodes[j].X;
            var dy = _nodes[i].Y - _nodes[j].Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d > MaxEdgeDist)
            {
                continue;
            }

            var opacity = (1 - d / MaxEdgeDist) * 0.22;
            NeuralCanvas.Children.Add(new Line
            {
                X1 = _nodes[i].X, Y1 = _nodes[i].Y,
                X2 = _nodes[j].X, Y2 = _nodes[j].Y,
                StrokeThickness = 0.7,
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0xEC, 0x48, 0x99))
            });
            _nodes[i].Connections.Add(j);
            _nodes[j].Connections.Add(i);
        }

        // Draw nodes
        foreach (var node in _nodes)
        {
            var dot = new Ellipse
            {
                Width = node.Size * 2, Height = node.Size * 2,
                Fill = new SolidColorBrush(Color.FromArgb(200, 0xEC, 0x48, 0x99)),
                RenderTransform = new TranslateTransform()
            };
            Canvas.SetLeft(dot, node.X - node.Size);
            Canvas.SetTop(dot, node.Y - node.Size);
            NeuralCanvas.Children.Add(dot);
            node.Dot = dot;

            dot.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, _rng.NextDouble() * 0.9 + 0.2,
                    TimeSpan.FromSeconds(_rng.NextDouble() * 1.2 + 0.3)));

            AnimateNodeFloat(node);
        }

        FireRandomPulse();
    }

    private void AnimateNodeFloat(NeuralNode node)
    {
        var dur = _rng.NextDouble() * 7 + 5;
        var dx = (_rng.NextDouble() - 0.5) * 50;
        var dy = (_rng.NextDouble() - 0.5) * 50;
        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var beg = TimeSpan.FromSeconds(_rng.NextDouble() * 3);

        node.Dot?.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(
                node.X - node.Size, node.X - node.Size + dx, TimeSpan.FromSeconds(dur))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease, BeginTime = beg });
        node.Dot?.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(
                node.Y - node.Size, node.Y - node.Size + dy, TimeSpan.FromSeconds(dur * 1.15))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease, BeginTime = beg });
    }

    private void FireRandomPulse()
    {
        if (_nodes.Count == 0)
        {
            return;
        }

        PropagatePulse(_rng.Next(_nodes.Count), [], 0);
    }

    private void PropagatePulse(int nodeIdx, HashSet<int> visited, int depth)
    {
        if (depth > 4 || !visited.Add(nodeIdx))
        {
            return;
        }

        var node = _nodes[nodeIdx];
        var delay = TimeSpan.FromSeconds(depth * 0.18);
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (node.Dot != null)
        {
            node.Dot.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1.0, 0.15, TimeSpan.FromSeconds(0.45))
                    { BeginTime = delay, EasingFunction = easeOut });

            if (node.Dot.LayoutTransform is not ScaleTransform st)
            {
                st = new ScaleTransform(1, 1);
                node.Dot.LayoutTransform = st;
            }

            var burstAnim = new DoubleAnimation(2.5, 1.0, TimeSpan.FromSeconds(0.4))
                { BeginTime = delay, EasingFunction = easeOut };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, burstAnim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, burstAnim);
        }

        foreach (var neighbor in node.Connections)
        {
            var captured = neighbor;
            var dt = new DispatcherTimer { Interval = delay + TimeSpan.FromSeconds(0.12) };
            dt.Tick += (_, _) =>
            {
                dt.Stop();
                PropagatePulse(captured, visited, depth + 1);
            };
            dt.Start();
        }
    }

    private sealed class NeuralNode
    {
        public readonly List<int> Connections = [];
        public Ellipse? Dot;
        public double X, Y, Size;
    }
}