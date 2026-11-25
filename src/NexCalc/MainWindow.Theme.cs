using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NexCalc;

public partial class MainWindow
{
    private const string PreferencesKey = @"Software\NexCalc";
    private bool lightTheme;

    private readonly Dictionary<string, (Color Dark, Color Light)> themeColors = new();

    private void InitializeTheme()
    {
        foreach (var (name, light) in LightColors)
        {
            string key = name + "Brush";
            var dark = ((SolidColorBrush)Resources[key]).Color;
            themeColors.Add(name, (dark, (Color)ColorConverter.ConvertFromString(light)));
        }
    }

    private void InitializePreferences()
    {
        try
        {
            using var settings = Registry.CurrentUser.OpenSubKey(PreferencesKey);
            if (settings is not null)
            {
                RestoreSelection(settings, "Angle", angleCombo);
                RestoreSelection(settings, "Format", displayCombo);
                RestoreSelection(settings, "Digits", displayDigits);
                mathPrintToggle.IsChecked = settings.GetValue("MathPrint") is 1;
                SetTheme(settings.GetValue("LightTheme") is 1);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            // Preferences are optional when the current user cannot access the registry.
        }
    }

    private static void RestoreSelection(RegistryKey settings, string name, ComboBox choice)
    {
        if (settings.GetValue(name) is int index && index >= 0 && index < choice.Items.Count)
            choice.SelectedIndex = index;
    }

    private void SavePreferences()
    {
        try
        {
            using var settings = Registry.CurrentUser.CreateSubKey(PreferencesKey);
            settings.SetValue("Angle", angleCombo.SelectedIndex);
            settings.SetValue("Format", displayCombo.SelectedIndex);
            settings.SetValue("Digits", displayDigits.SelectedIndex);
            settings.SetValue("MathPrint", mathPrintToggle.IsChecked == true ? 1 : 0);
            settings.SetValue("LightTheme", lightTheme ? 1 : 0);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            // Preferences are optional when the current user cannot access the registry.
        }
    }

    private void Theme_Click(object sender, RoutedEventArgs e) => SetTheme(!lightTheme);

    private void SetTheme(bool light)
    {
        if (lightTheme == light) return;
        lightTheme = light;
        themeButton.Content = lightTheme ? "Dark theme" : "Light theme";
        foreach (var (name, (dark, lightColor)) in themeColors)
            Resources[name + "Brush"] = new SolidColorBrush(lightTheme ? lightColor : dark);
    }

    private static readonly (string Name, string Light)[] LightColors =
    [
        ("Window", "#F4F6FA"),
        ("Text", "#1C2637"),
        ("Button", "#E0E7F0"),
        ("ButtonBorder", "#A6B4C6"),
        ("Accent", "#00506F"),
        ("NumberKey", "#DAE4EF"),
        ("NumberText", "#172638"),
        ("TabText", "#283D58"),
        ("Surface", "#FFFFFF"),
        ("Selected", "#D1E3FF"),
        ("Toolbar", "#E0E8F3"),
        ("Border", "#B2C0D0"),
        ("Output", "#E8EEF5"),
        ("Muted", "#455C74"),
        ("Function", "#14603C"),
        ("Warm", "#7E4400"),
        ("Empty", "#EDF1F7"),
        ("Chart", "#FFFFFF"),
        ("CodeOutput", "#245A29"),
        ("Error", "#A32936"),
        ("ChartLabel", "#495B73"),
        ("ChartAxis", "#8293AA"),
        ("Curve", "#175FB6"),
        ("Tangent", "#7035A1"),
        ("Sketch", "#A32936"),
    ];
}
