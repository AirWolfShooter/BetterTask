using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BetterTask.Interface.Managers;
using BetterTask.Themes;

namespace BetterTask.Interface.UserControlPages;

public partial class ThemesSettings : UserControl
{
    private const int MaximumPresetImageSizeBytes = 50 * 1024 * 1024;

    private static readonly FilePickerFileType ImageFileType = new("Images and GIFs")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" }
    };

    private static readonly FilePickerFileType ThemePresetFileType = new("BetterTask theme presets")
    {
        Patterns = new[] { "*.btpreset", "*.json" }
    };

    private static readonly string[] SupportedImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    private static readonly (string Key, string Label)[] ColorEntries =
    {
        ("AppBackgroundBrush", "Window background"),
        ("SurfaceBrush", "Panels"),
        ("CardBrush", "Cards"),
        ("CardHoverBrush", "Card hover"),
        ("CardPressedBrush", "Card pressed"),
        ("OutlineBrush", "Borders"),
        ("OutlineHoverBrush", "Border hover"),
        ("TextPrimaryBrush", "Primary text"),
        ("TextSecondaryBrush", "Secondary text"),
        ("AccentBrush", "Accent"),
        ("AccentHoverBrush", "Accent hover"),
        ("AccentPressedBrush", "Accent pressed"),
        ("AccentSoftBrush", "Soft accent"),
        ("SuccessBrush", "Success"),
        ("DangerBrush", "Danger"),
        ("DangerSurfaceBrush", "Danger surface"),
        ("DangerHoverBrush", "Danger hover"),
        ("DangerTextBrush", "Danger button text")
    };

    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool isLoadingControls;
    private bool hasPendingSave;

    public ThemesSettings()
    {
        InitializeComponent();
        saveTimer.Tick += SaveTimerTick;
        Unloaded += (_, _) => FlushPendingSave();
        LoadControlsFromSettings();
    }

    private ThemeSettings ThemeData => SettingsManager.Instance.settings.Theme ??= new ThemeSettings();

    private void LoadControlsFromSettings()
    {
        isLoadingControls = true;
        ThemeData.Normalize();

        UseBackgroundGradientCheckBox.IsChecked = ThemeData.UseBackgroundGradient;
        AnimateBackgroundGradientCheckBox.IsChecked = ThemeData.AnimateBackgroundGradient;
        AnimateBackgroundGradientCheckBox.IsEnabled = ThemeData.UseBackgroundGradient;
        BackgroundGradientStartPicker.Color = Color.Parse(ThemeData.BackgroundGradientStart);
        BackgroundGradientEndPicker.Color = Color.Parse(ThemeData.BackgroundGradientEnd);
        UseAccentGradientCheckBox.IsChecked = ThemeData.UseAccentGradient;
        AnimateAccentGradientCheckBox.IsChecked = ThemeData.AnimateAccentGradient;
        AnimateAccentGradientCheckBox.IsEnabled = ThemeData.UseAccentGradient;
        AccentGradientStartPicker.Color = Color.Parse(ThemeData.AccentGradientStart);
        AccentGradientEndPicker.Color = Color.Parse(ThemeData.AccentGradientEnd);
        BackgroundOpacitySlider.Value = ThemeData.BackgroundImageOpacity;
        BackgroundPathText.Text = string.IsNullOrWhiteSpace(ThemeData.BackgroundImagePath)
            ? "No background image selected"
            : Path.GetFileName(ThemeData.BackgroundImagePath);
        ClearBackgroundButton.IsEnabled = !string.IsNullOrWhiteSpace(ThemeData.BackgroundImagePath);

        BuildColorEditors();
        RefreshPresetSelectors();
        UpdateBackgroundOpacityText(ThemeData.BackgroundImageOpacity);
        isLoadingControls = false;
    }

    private void RefreshPresetSelectors(string? selectedCustomPreset = null)
    {
        BuiltInPresetComboBox.ItemsSource = ThemePresetCatalog.BuiltInNames;
        if (BuiltInPresetComboBox.SelectedIndex < 0)
            BuiltInPresetComboBox.SelectedIndex = 0;

        List<string> presetNames = SettingsManager.Instance.settings.ThemePresets
            .Select(preset => preset.Name)
            .ToList();
        CustomPresetComboBox.ItemsSource = presetNames;
        CustomPresetComboBox.IsEnabled = presetNames.Count > 0;

        if (!string.IsNullOrWhiteSpace(selectedCustomPreset))
        {
            CustomPresetComboBox.SelectedItem = presetNames.FirstOrDefault(
                name => string.Equals(name, selectedCustomPreset, StringComparison.OrdinalIgnoreCase));
        }
        else if (presetNames.Count > 0 && CustomPresetComboBox.SelectedIndex < 0)
        {
            CustomPresetComboBox.SelectedIndex = 0;
        }
    }

    private void applyBuiltInPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (BuiltInPresetComboBox.SelectedItem is not string name)
        {
            PresetStatusText.Text = "Choose a built-in preset first.";
            return;
        }

        ApplyThemePreset(ThemePresetCatalog.CreateBuiltIn(name), $"Applied {name} theme.");
    }

    private void saveCustomPresetClicked(object? sender, RoutedEventArgs e)
    {
        string name = PresetNameTextBox.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            PresetStatusText.Text = "Enter a name for this preset.";
            return;
        }

        StoreCustomPreset(new ThemePreset { Name = name, Theme = ThemeData.Clone() });
        PresetStatusText.Text = $"Saved '{name}' to your custom presets.";
    }

    private void applyCustomPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (CustomPresetComboBox.SelectedItem is not string name)
        {
            PresetStatusText.Text = "Save or import a custom preset first.";
            return;
        }

        ThemePreset? preset = SettingsManager.Instance.settings.ThemePresets.FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
        {
            RefreshPresetSelectors();
            PresetStatusText.Text = "That custom preset is no longer available.";
            return;
        }

        ApplyThemePreset(preset, $"Applied '{preset.Name}'.");
    }

    private void deleteCustomPresetClicked(object? sender, RoutedEventArgs e)
    {
        if (CustomPresetComboBox.SelectedItem is not string name)
        {
            PresetStatusText.Text = "Choose a custom preset to delete.";
            return;
        }

        List<ThemePreset> presets = SettingsManager.Instance.settings.ThemePresets;
        ThemePreset? preset = presets.FirstOrDefault(
            item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null)
            return;

        presets.Remove(preset);
        SettingsManager.Instance.saveSettings();
        RefreshPresetSelectors();
        PresetStatusText.Text = $"Deleted '{name}'.";
    }

    private async void importThemePresetClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        try
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import a BetterTask theme preset",
                AllowMultiple = false,
                FileTypeFilter = new[] { ThemePresetFileType }
            });
            if (files.Count == 0)
                return;

            await using Stream stream = await files[0].OpenReadAsync();
            ThemePresetDocument? document = await JsonSerializer.DeserializeAsync<ThemePresetDocument>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (document is null || document.FormatVersion != 1 || document.Theme is null)
            {
                PresetStatusText.Text = "This preset file is invalid or uses an unsupported version.";
                return;
            }

            ThemeSettings importedTheme = document.Theme;
            importedTheme.BackgroundImagePath = null;
            if (!string.IsNullOrWhiteSpace(document.BackgroundImageData))
            {
                if (document.BackgroundImageData.Length > ((MaximumPresetImageSizeBytes + 2) / 3 * 4))
                    throw new InvalidDataException("The preset background image is larger than 50 MB.");

                byte[] imageData = Convert.FromBase64String(document.BackgroundImageData);
                if (imageData.Length > MaximumPresetImageSizeBytes)
                    throw new InvalidDataException("The preset background image is larger than 50 MB.");

                importedTheme.BackgroundImagePath = await SavePresetImageAsync(
                    document.BackgroundImageFileName,
                    imageData);
            }

            importedTheme.Normalize();
            string presetName = string.IsNullOrWhiteSpace(document.Name)
                ? Path.GetFileNameWithoutExtension(files[0].Name)
                : document.Name.Trim();
            presetName = GetUniquePresetName(presetName);
            var preset = new ThemePreset { Name = presetName, Theme = importedTheme.Clone() };
            StoreCustomPreset(preset);
            PresetNameTextBox.Text = presetName;
            ApplyThemePreset(preset, $"Imported and applied '{presetName}'.");
        }
        catch (Exception ex)
        {
            PresetStatusText.Text = $"Could not import preset: {ex.Message}";
        }
    }

    private async void exportCurrentThemePresetClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        string name = string.IsNullOrWhiteSpace(PresetNameTextBox.Text)
            ? "My BetterTask theme"
            : PresetNameTextBox.Text.Trim();

        try
        {
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export current theme preset",
                SuggestedFileName = $"{MakeSafeFileName(name)}.btpreset",
                FileTypeChoices = new[] { ThemePresetFileType }
            });
            if (file is null)
                return;

            ThemeSettings exportedTheme = ThemeData.Clone();
            string? backgroundPath = exportedTheme.BackgroundImagePath;
            exportedTheme.BackgroundImagePath = null;
            var document = new ThemePresetDocument
            {
                Name = name,
                Theme = exportedTheme
            };

            if (!string.IsNullOrWhiteSpace(backgroundPath) && File.Exists(backgroundPath))
            {
                var imageFile = new FileInfo(backgroundPath);
                if (imageFile.Length > MaximumPresetImageSizeBytes)
                    throw new InvalidDataException("The background image is larger than 50 MB and cannot be bundled.");

                document.BackgroundImageFileName = imageFile.Name;
                document.BackgroundImageData = Convert.ToBase64String(await File.ReadAllBytesAsync(backgroundPath));
            }

            await using Stream destination = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(destination, document, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            PresetStatusText.Text = $"Exported '{name}'.";
        }
        catch (Exception ex)
        {
            PresetStatusText.Text = $"Could not export preset: {ex.Message}";
        }
    }

    private async Task<string> SavePresetImageAsync(string? fileName, byte[] imageData)
    {
        string extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("The preset contains an unsupported background image type.");

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterTask",
            "Themes");
        Directory.CreateDirectory(directory);
        string safeName = MakeSafeFileName(Path.GetFileNameWithoutExtension(fileName ?? "background"));
        string destination = Path.Combine(directory, $"{safeName}-{Guid.NewGuid():N}{extension}");
        await File.WriteAllBytesAsync(destination, imageData);
        return destination;
    }

    private void StoreCustomPreset(ThemePreset preset)
    {
        List<ThemePreset> presets = SettingsManager.Instance.settings.ThemePresets;
        int existingIndex = presets.FindIndex(
            item => string.Equals(item.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            presets[existingIndex] = preset.Clone();
        else
            presets.Add(preset.Clone());

        SettingsManager.Instance.saveSettings();
        RefreshPresetSelectors(preset.Name);
    }

    private void ApplyThemePreset(ThemePreset preset, string status)
    {
        SettingsManager.Instance.settings.Theme = preset.Theme.Clone();
        LoadControlsFromSettings();
        ThemeManager.Instance.ApplyCurrentTheme();
        saveTimer.Stop();
        hasPendingSave = false;
        SettingsManager.Instance.saveSettings();
        PresetStatusText.Text = status;
    }

    private static string MakeSafeFileName(string name)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string safeName = new(name.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(safeName) ? "BetterTaskTheme" : safeName;
    }

    private static string GetUniquePresetName(string requestedName)
    {
        List<string> existingNames = SettingsManager.Instance.settings.ThemePresets
            .Select(preset => preset.Name)
            .ToList();
        if (!existingNames.Contains(requestedName, StringComparer.OrdinalIgnoreCase))
            return requestedName;

        int suffix = 2;
        string uniqueName;
        do
        {
            uniqueName = $"{requestedName} ({suffix++})";
        }
        while (existingNames.Contains(uniqueName, StringComparer.OrdinalIgnoreCase));

        return uniqueName;
    }

    private void BuildColorEditors()
    {
        ColorEditors.Children.Clear();

        foreach ((string key, string label) in ColorEntries)
        {
            Color color = Color.Parse(ThemeData.Colors[key]);
            var colorText = new TextBlock
            {
                Text = color.ToString(),
                FontSize = 11,
                Foreground = GetAppBrush("TextSecondaryBrush", "#FF9BA9B9"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 82
            };

            var picker = new ColorPicker
            {
                Color = color,
                Width = 132,
                Height = 38,
                IsAlphaEnabled = true,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            picker.ColorChanged += (_, args) =>
            {
                if (isLoadingControls)
                    return;

                ThemeData.Colors[key] = args.NewColor.ToString();
                colorText.Text = args.NewColor.ToString();
                ApplyThemeAndQueueSave();
            };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 12,
                MinHeight = 46
            };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = GetAppBrush("TextPrimaryBrush", "#FFF1F4F9"),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(picker, 1);
            row.Children.Add(picker);
            Grid.SetColumn(colorText, 2);
            row.Children.Add(colorText);
            ColorEditors.Children.Add(row);
        }
    }

    private async void importBackgroundClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        try
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a background image or GIF",
                AllowMultiple = false,
                FileTypeFilter = new[] { ImageFileType }
            });
            if (files.Count == 0)
                return;

            string extension = Path.GetExtension(files[0].Name);
            string importDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BetterTask",
                "Themes");
            Directory.CreateDirectory(importDirectory);

            string safeName = Path.GetFileNameWithoutExtension(files[0].Name);
            string destinationPath = Path.Combine(
                importDirectory,
                $"{safeName}-{Guid.NewGuid():N}{extension}");
            await using (Stream source = await files[0].OpenReadAsync())
            await using (var destination = File.Create(destinationPath))
            {
                await source.CopyToAsync(destination);
                await destination.FlushAsync();
            }

            ThemeData.BackgroundImagePath = destinationPath;
            BackgroundPathText.Text = Path.GetFileName(destinationPath);
            ClearBackgroundButton.IsEnabled = true;
            ApplyThemeAndQueueSave();
        }
        catch (Exception ex)
        {
            BackgroundPathText.Text = $"Could not import image: {ex.Message}";
        }
    }

    private void clearBackgroundClicked(object? sender, RoutedEventArgs e)
    {
        ThemeData.BackgroundImagePath = null;
        BackgroundPathText.Text = "No background image selected";
        ClearBackgroundButton.IsEnabled = false;
        ApplyThemeAndQueueSave();
    }

    private void backgroundOpacityChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.BackgroundImageOpacity = e.NewValue;
        UpdateBackgroundOpacityText(e.NewValue);
        ApplyThemeAndQueueSave();
    }

    private void useBackgroundGradientClicked(object? sender, RoutedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.UseBackgroundGradient = UseBackgroundGradientCheckBox.IsChecked ?? false;
        AnimateBackgroundGradientCheckBox.IsEnabled = ThemeData.UseBackgroundGradient;
        ApplyThemeAndQueueSave();
    }

    private void animateBackgroundGradientClicked(object? sender, RoutedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.AnimateBackgroundGradient = AnimateBackgroundGradientCheckBox.IsChecked ?? false;
        ApplyThemeAndQueueSave();
    }

    private void backgroundGradientStartChanged(object? sender, ColorChangedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.BackgroundGradientStart = e.NewColor.ToString();
        ApplyThemeAndQueueSave();
    }

    private void backgroundGradientEndChanged(object? sender, ColorChangedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.BackgroundGradientEnd = e.NewColor.ToString();
        ApplyThemeAndQueueSave();
    }

    private void useAccentGradientClicked(object? sender, RoutedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.UseAccentGradient = UseAccentGradientCheckBox.IsChecked ?? false;
        AnimateAccentGradientCheckBox.IsEnabled = ThemeData.UseAccentGradient;
        ApplyThemeAndQueueSave();
    }

    private void animateAccentGradientClicked(object? sender, RoutedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.AnimateAccentGradient = AnimateAccentGradientCheckBox.IsChecked ?? false;
        ApplyThemeAndQueueSave();
    }

    private void accentGradientStartChanged(object? sender, ColorChangedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.AccentGradientStart = e.NewColor.ToString();
        ApplyThemeAndQueueSave();
    }

    private void accentGradientEndChanged(object? sender, ColorChangedEventArgs e)
    {
        if (isLoadingControls)
            return;

        ThemeData.AccentGradientEnd = e.NewColor.ToString();
        ApplyThemeAndQueueSave();
    }

    private void resetThemeClicked(object? sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.settings.Theme = new ThemeSettings();
        LoadControlsFromSettings();
        ApplyThemeAndQueueSave();
    }

    private void ApplyThemeAndQueueSave()
    {
        ThemeManager.Instance.ApplyCurrentTheme();
        hasPendingSave = true;
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void SaveTimerTick(object? sender, EventArgs e)
    {
        FlushPendingSave();
    }

    private void FlushPendingSave()
    {
        saveTimer.Stop();
        if (!hasPendingSave)
            return;

        SettingsManager.Instance.saveSettings();
        hasPendingSave = false;
    }

    private void UpdateBackgroundOpacityText(double opacity)
    {
        BackgroundOpacityText.Text = $"{Math.Round(opacity * 100):0}%";
    }

    private static IBrush GetAppBrush(string key, string fallbackColor)
    {
        var app = Application.Current;
        return app is not null
            && app.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out object? value)
            && value is IBrush brush
                ? brush
                : new SolidColorBrush(Color.Parse(fallbackColor));
    }
}
