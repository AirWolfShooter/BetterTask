using System.Diagnostics;
using AnimatedImage.Avalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using BetterTask.Infrastructure;
using BetterTask.Interface;
using BetterTask.Interface.Managers;
using BetterTask.Settings;

namespace BetterTask.Themes;

public sealed class ThemeManager
{
    private static readonly ThemeManager instance = new();
    private readonly List<WeakReference<Image>> backgroundImages = new();
    private readonly DispatcherTimer gradientAnimationTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private LinearGradientBrush? animatedBackgroundGradient;
    private LinearGradientBrush? animatedAccentGradient;
    private long lastGradientTick;
    private double gradientPhase;

    public static ThemeManager Instance => instance;

    private ThemeManager()
    {
        gradientAnimationTimer.Tick += AnimateGradients;
    }

    public void ApplyCurrentTheme()
    {
        var app = Application.Current;
        if (app is null)
            return;

        ThemeSettings theme = GetThemeSettings();

        foreach (string key in ThemeSettings.DefaultColors.Keys)
        {
            Color color = Color.Parse(theme.Colors[key]);
            if (app.Resources[key] is SolidColorBrush existingBrush)
            {
                existingBrush.Color = color;
            }
            else
            {
                app.Resources[key] = new SolidColorBrush(color);
            }
        }

        if (theme.UseBackgroundGradient)
        {
            LinearGradientBrush brush = CreateGradient(theme.BackgroundGradientStart, theme.BackgroundGradientEnd);
            app.Resources["AppBackgroundBrush"] = brush;
            animatedBackgroundGradient = theme.AnimateBackgroundGradient ? brush : null;
        }
        else
        {
            animatedBackgroundGradient = null;
            if (app.Resources["AppBackgroundBrush"] is SolidColorBrush backgroundBrush)
            {
                backgroundBrush.Color = Color.Parse(theme.Colors["AppBackgroundBrush"]);
            }
            else
            {
                app.Resources["AppBackgroundBrush"] =
                    new SolidColorBrush(Color.Parse(theme.Colors["AppBackgroundBrush"]));
            }
        }

        if (theme.UseAccentGradient)
        {
            LinearGradientBrush brush = CreateGradient(theme.AccentGradientStart, theme.AccentGradientEnd);
            app.Resources["PrimaryButtonBackgroundBrush"] = brush;
            animatedAccentGradient = theme.AnimateAccentGradient ? brush : null;
        }
        else
        {
            animatedAccentGradient = null;
            if (app.Resources["PrimaryButtonBackgroundBrush"] is SolidColorBrush buttonBrush)
            {
                buttonBrush.Color = Color.Parse(theme.Colors["AccentBrush"]);
            }
            else
            {
                app.Resources["PrimaryButtonBackgroundBrush"] =
                    new SolidColorBrush(Color.Parse(theme.Colors["AccentBrush"]));
            }
        }

        UpdateGradientAnimationTimer();
        RefreshBackgroundImages(theme);
        MainWindow.Current?.RefreshThemedControls();
    }

    public void RegisterBackgroundImage(Image image)
    {
        backgroundImages.RemoveAll(reference => !reference.TryGetTarget(out _));
        backgroundImages.Add(new WeakReference<Image>(image));
        UpdateBackgroundImage(image, GetThemeSettings());
    }

    public IBrush GetBrush(string key, string fallbackColor)
    {
        var app = Application.Current;
        return app is not null
            && app.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out object? value)
            && value is IBrush brush
                ? brush
                : new SolidColorBrush(Color.Parse(fallbackColor));
    }

    private static ThemeSettings GetThemeSettings()
    {
        AppSettings settings = SettingsManager.Instance.settings;
        settings.Theme ??= new ThemeSettings();
        settings.Theme.Normalize();
        return settings.Theme;
    }

    private void RefreshBackgroundImages(ThemeSettings theme)
    {
        for (int index = backgroundImages.Count - 1; index >= 0; index--)
        {
            if (backgroundImages[index].TryGetTarget(out Image? image))
            {
                UpdateBackgroundImage(image, theme);
            }
            else
            {
                backgroundImages.RemoveAt(index);
            }
        }
    }

    private static void UpdateBackgroundImage(Image image, ThemeSettings theme)
    {
        image.Opacity = theme.BackgroundImageOpacity;
        string? path = theme.BackgroundImagePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ImageBehavior.SetAnimatedSource(image, null!);
            image.IsVisible = false;
            return;
        }

        try
        {
            ImageBehavior.SetAnimatedSource(image, new Uri(Path.GetFullPath(path), UriKind.Absolute));
            image.IsVisible = true;
        }
        catch (Exception ex)
        {
            ImageBehavior.SetAnimatedSource(image, null!);
            image.IsVisible = false;
            AppLogger.Warning("Theme", "Could not load the selected theme background.", ex);
        }
    }

    private static LinearGradientBrush CreateGradient(string start, string end)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(start), 0),
                new GradientStop(Color.Parse(end), 1)
            }
        };
    }

    private void UpdateGradientAnimationTimer()
    {
        bool shouldAnimate = animatedBackgroundGradient is not null || animatedAccentGradient is not null;
        if (shouldAnimate && !gradientAnimationTimer.IsEnabled)
        {
            lastGradientTick = Stopwatch.GetTimestamp();
            gradientAnimationTimer.Start();
        }
        else if (!shouldAnimate && gradientAnimationTimer.IsEnabled)
        {
            gradientAnimationTimer.Stop();
        }
    }

    private void AnimateGradients(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = (double)(now - lastGradientTick) / Stopwatch.Frequency;
        lastGradientTick = now;
        gradientPhase = (gradientPhase + elapsedSeconds * (Math.Tau / 12)) % Math.Tau;

        if (animatedBackgroundGradient is not null)
            SetGradientDirection(animatedBackgroundGradient, gradientPhase);
        if (animatedAccentGradient is not null)
            SetGradientDirection(animatedAccentGradient, -gradientPhase);
    }

    private static void SetGradientDirection(LinearGradientBrush brush, double phase)
    {
        double x = Math.Cos(phase) * 0.5;
        double y = Math.Sin(phase) * 0.5;
        brush.StartPoint = new RelativePoint(0.5 - x, 0.5 - y, RelativeUnit.Relative);
        brush.EndPoint = new RelativePoint(0.5 + x, 0.5 + y, RelativeUnit.Relative);
    }
}
