using System.Windows;
using System.Windows.Media;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Minark.Client.Services;

/// <summary>
///     Centralizes the application of an accent color across the whole app.
///     Updates a coherent set of dynamic resources (C_Accent*, AccentPrimary,
///     AccentSecondary, AccentGradient, iNKORE accent fill brushes, ...)
///     from a single dominant color choice. Every accent-driven brush in XAML
///     pulls from these dynamic resources, so changing the color here updates
///     every UI surface live.
/// </summary>
public static class AccentService
{
    public static Color CurrentAccent { get; private set; } = Color.FromRgb(0x7C, 0x3A, 0xED);

    /// <summary>
    ///     Apply a new accent color to all dynamic resources.
    ///     Called whenever the user picks a swatch, and at app startup with the persisted choice.
    /// </summary>
    public static void Apply(Color primary)
    {
        CurrentAccent = primary;

        // ── Derived colors ───────────────────────────────────────────────────
        var secondary = ShiftHue(primary, 35.0, 1.05, 1.10); // gradient end stop ("the variant")
        var soft = ShiftHue(primary, 0.0, 0.85, 1.35); // pastel for typing dots, etc.
        var glow = Color.FromArgb(0x2A, primary.R, primary.G, primary.B);
        var dark = ShiftHue(primary, -10.0, 1.0, 0.78); // 4F46E5-style darker companion
        var light1 = ShiftHue(primary, 0.0, 1.0, 1.18);
        var light2 = ShiftHue(primary, 0.0, 1.0, 1.36);
        var light3 = ShiftHue(primary, 0.0, 1.0, 1.55);
        var dark1 = ShiftHue(primary, 0.0, 1.0, 0.82);
        var dark2 = ShiftHue(primary, 0.0, 1.0, 0.65);
        var dark3 = ShiftHue(primary, 0.0, 1.0, 0.50);

        var rd = Application.Current?.Resources;
        if (rd is null)
        {
            return;
        }

        // ── Bare Color resources (consumed from XAML via {DynamicResource C_Accent*}) ──
        rd["C_Accent1"] = primary;
        rd["C_Accent2"] = secondary;
        rd["C_AccentSoft"] = soft;
        rd["C_AccentDark"] = dark;
        rd["C_AccentLight"] = light1;
        rd["C_AccentMenuHi"] = WithAlpha(primary, 0x26);

        // Translucent variants of the primary (hover/highlight backgrounds, soft borders)
        rd["C_Accent1_14"] = WithAlpha(primary, 0x14);
        rd["C_Accent1_1A"] = WithAlpha(primary, 0x1A);
        rd["C_Accent1_22"] = WithAlpha(primary, 0x22);
        rd["C_Accent1_26"] = WithAlpha(primary, 0x26);
        rd["C_Accent1_2A"] = WithAlpha(primary, 0x2A);
        rd["C_Accent1_33"] = WithAlpha(primary, 0x33);
        rd["C_Accent1_40"] = WithAlpha(primary, 0x40);
        rd["C_Accent1_50"] = WithAlpha(primary, 0x50);
        rd["C_Accent1_66"] = WithAlpha(primary, 0x66);
        rd["C_Accent1_80"] = WithAlpha(primary, 0x80);
        rd["C_Accent1_CC"] = WithAlpha(primary, 0xCC);
        rd["C_Accent2_40"] = WithAlpha(secondary, 0x40);
        rd["C_Accent2_50"] = WithAlpha(secondary, 0x50);
        rd["C_AccentDark_2A"] = WithAlpha(dark, 0x2A);
        rd["C_AccentDark_CC"] = WithAlpha(dark, 0xCC);
        rd["C_AccentSoft_33"] = WithAlpha(soft, 0x33);

        // ── Custom palette brushes ──────────────────────────────────────────
        rd["AccentPrimary"] = new SolidColorBrush(primary);
        rd["AccentSecondary"] = new SolidColorBrush(secondary);
        rd["AccentSoft"] = new SolidColorBrush(soft);
        rd["AccentGlow"] = new SolidColorBrush(glow);

        // Hover/selected/border brushes used by sidebar nav, buttons, etc.
        // Replacing them as fresh brush instances forces every DataTrigger / Style.Setter
        // that references them to re-resolve, even when WPF wouldn't have re-evaluated
        // the underlying Color binding.
        rd["AccentHoverBg"] = new SolidColorBrush(WithAlpha(primary, 0x1A));
        rd["AccentSelectedBg"] = new SolidColorBrush(WithAlpha(primary, 0x26));
        rd["AccentPressedBg"] = new SolidColorBrush(WithAlpha(primary, 0x2A));
        rd["AccentBorderSoft"] = new SolidColorBrush(WithAlpha(primary, 0x22));
        rd["AccentBorderHover"] = new SolidColorBrush(WithAlpha(primary, 0x66));
        rd["AccentWatermark"] = new SolidColorBrush(WithAlpha(soft, 0x33));

        // ── iNKORE / WinUI accent brushes (pivot indicator, accent buttons, ...) ──
        rd["AccentFillColorDefaultBrush"] = new SolidColorBrush(primary);
        rd["AccentFillColorSecondaryBrush"] = new SolidColorBrush(light1);
        rd["AccentFillColorTertiaryBrush"] = new SolidColorBrush(light2);
        rd["AccentFillColorDisabledBrush"] = new SolidColorBrush(dark2);
        rd["AccentTextFillColorPrimaryBrush"] = new SolidColorBrush(light2);
        rd["AccentTextFillColorSecondaryBrush"] = new SolidColorBrush(light1);
        rd["AccentTextFillColorTertiaryBrush"] = new SolidColorBrush(primary);
        rd["AccentTextFillColorDisabledBrush"] = new SolidColorBrush(dark2);

        rd["SystemAccentColor"] = primary;
        rd["SystemAccentColorLight1"] = light1;
        rd["SystemAccentColorLight2"] = light2;
        rd["SystemAccentColorLight3"] = light3;
        rd["SystemAccentColorDark1"] = dark1;
        rd["SystemAccentColorDark2"] = dark2;
        rd["SystemAccentColorDark3"] = dark3;

        // ── iNKORE control-state brushes (TextBox focus, CheckBox tick, ToggleSwitch on, ...) ──
        // Re-instantiate them so every Trigger/Style.Setter that references them re-resolves
        // when the user picks a new accent color, even on already-realized control templates.
        var primaryBrush = new Func<SolidColorBrush>(() => new SolidColorBrush(primary));
        var lightBrush = new Func<SolidColorBrush>(() => new SolidColorBrush(light1));
        var darkBrush = new Func<SolidColorBrush>(() => new SolidColorBrush(dark));

        rd["TextControlBorderBrushFocused"] = primaryBrush();
        rd["TextControlBorderBrushPointerOver"] = lightBrush();
        rd["TextControlSelectionHighlightColor"] = primaryBrush();
        rd["TextControlSelectionHighlightColorBrush"] = primaryBrush();
        rd["TextControlButtonForegroundPressed"] = primaryBrush();
        rd["TextControlButtonBackgroundPressed"] = primaryBrush();

        rd["CheckBoxCheckBackgroundFillChecked"] = primaryBrush();
        rd["CheckBoxCheckBackgroundFillCheckedPointerOver"] = lightBrush();
        rd["CheckBoxCheckBackgroundFillCheckedPressed"] = darkBrush();
        rd["CheckBoxCheckBackgroundStrokeChecked"] = primaryBrush();

        rd["ToggleSwitchFillOn"] = primaryBrush();
        rd["ToggleSwitchFillOnPointerOver"] = lightBrush();
        rd["ToggleSwitchFillOnPressed"] = darkBrush();
        rd["ToggleSwitchStrokeOn"] = primaryBrush();
        rd["ToggleSwitchStrokeOnPointerOver"] = lightBrush();
        rd["ToggleSwitchStrokeOnPressed"] = darkBrush();

        rd["SliderTrackValueFill"] = primaryBrush();
        rd["SliderTrackValueFillPointerOver"] = lightBrush();
        rd["SliderTrackValueFillPressed"] = darkBrush();
        rd["SliderThumbBackground"] = primaryBrush();
        rd["SliderThumbBackgroundPointerOver"] = lightBrush();
        rd["SliderThumbBackgroundPressed"] = darkBrush();

        rd["ButtonBackgroundPointerOver"] = new SolidColorBrush(WithAlpha(primary, 0x1A));
        rd["ButtonBorderBrushPointerOver"] = new SolidColorBrush(WithAlpha(primary, 0x66));

        rd["HyperlinkButtonForeground"] = primaryBrush();
        rd["HyperlinkButtonForegroundPointerOver"] = lightBrush();
        rd["HyperlinkButtonForegroundPressed"] = darkBrush();

        rd["RadioButtonOuterEllipseCheckedFill"] = primaryBrush();
        rd["RadioButtonOuterEllipseCheckedStroke"] = primaryBrush();
        rd["RadioButtonOuterEllipseCheckedFillPointerOver"] = lightBrush();
        rd["RadioButtonOuterEllipseCheckedStrokePointerOver"] = lightBrush();

        rd["ProgressBarForeground"] = primaryBrush();
        rd["ProgressRingForeground"] = primaryBrush();

        rd["ComboBoxBorderBrushFocused"] = primaryBrush();
        rd["ComboBoxItemBackgroundSelected"] = new SolidColorBrush(WithAlpha(primary, 0x26));
        rd["ComboBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(WithAlpha(primary, 0x33));

        rd["ListViewItemBackgroundSelected"] = new SolidColorBrush(WithAlpha(primary, 0x26));
        rd["ListViewItemBackgroundSelectedPointerOver"] = new SolidColorBrush(WithAlpha(primary, 0x33));

        rd["TabViewItemHeaderBackgroundSelected"] = new SolidColorBrush(WithAlpha(primary, 0x1A));
        rd["TabViewItemHeaderForegroundSelected"] = primaryBrush();

        // ── Two-tone gradient brushes ───────────────────────────────────────
        var grad = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        grad.GradientStops.Add(new GradientStop(primary, 0));
        grad.GradientStops.Add(new GradientStop(secondary, 1));
        rd["AccentGradient"] = grad;

        var gradH = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0)
        };
        gradH.GradientStops.Add(new GradientStop(primary, 0));
        gradH.GradientStops.Add(new GradientStop(dark, 1));
        rd["AccentGradientH"] = gradH;
    }

    // ── HSL helpers ────────────────────────────────────────────────────────

    private static Color WithAlpha(Color c, byte alpha)
    {
        return Color.FromArgb(alpha, c.R, c.G, c.B);
    }

    private static Color ShiftHue(Color c, double degrees, double satFactor, double lightFactor)
    {
        RgbToHsl(c.R, c.G, c.B, out var h, out var s, out var l);
        h = (h + degrees / 360.0) % 1.0;
        if (h < 0)
        {
            h += 1.0;
        }

        s = Math.Clamp(s * satFactor, 0, 1);
        l = Math.Clamp(l * lightFactor, 0, 1);
        HslToRgb(h, s, l, out var r, out var g, out var b);
        return Color.FromArgb(c.A, r, g, b);
    }

    private static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
    {
        double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));
        l = (max + min) / 2.0;
        if (Math.Abs(max - min) < 1e-9)
        {
            h = 0;
            s = 0;
            return;
        }

        var d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        if (max == rd)
        {
            h = ((gd - bd) / d + (gd < bd ? 6 : 0)) / 6.0;
        }
        else if (max == gd)
        {
            h = ((bd - rd) / d + 2) / 6.0;
        }
        else
        {
            h = ((rd - gd) / d + 4) / 6.0;
        }
    }

    private static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
    {
        double rd, gd, bd;
        if (s < 1e-9)
        {
            rd = gd = bd = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            rd = HueToRgb(p, q, h + 1.0 / 3.0);
            gd = HueToRgb(p, q, h);
            bd = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        r = (byte)Math.Round(rd * 255);
        g = (byte)Math.Round(gd * 255);
        b = (byte)Math.Round(bd * 255);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6.0)
        {
            return p + (q - p) * 6 * t;
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + (q - p) * (2.0 / 3.0 - t) * 6;
        }

        return p;
    }
}