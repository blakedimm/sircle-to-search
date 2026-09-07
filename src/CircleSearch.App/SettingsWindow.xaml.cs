using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CircleSearch.App.Services;
using CircleSearch.Core.Models;
using Microsoft.Win32;

namespace CircleSearch.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private string _selectedColor = "#00D2FF";

    public event Action? SettingsSaved;

    public SettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        LoadSettingsToUi();
    }

    private void LoadSettingsToUi()
    {
        var s = _settingsManager.CurrentSettings;

        // Модификаторы хоткея
        CmbModifiers.SelectedIndex = s.Modifiers switch
        {
            0x0001 | 0x0004 => 1, // Alt + Shift
            0x0002 | 0x0001 => 2, // Ctrl + Alt
            0x0008 | 0x0004 => 3, // Win + Shift
            _ => 0                // Ctrl + Shift
        };

        // Клавиша
        CmbKey.SelectedIndex = s.VirtualKey switch
        {
            0x58 => 1, // X
            0x43 => 2, // C
            0x5A => 3, // Z
            0x44 => 4, // D
            0x46 => 5, // F
            _ => 0     // S
        };

        // Режим окна по умолчанию
        CmbDefaultMode.SelectedIndex = s.DefaultDisplayMode switch
        {
            WindowDisplayMode.Sidebar => 1,
            WindowDisplayMode.Expanded => 2,
            _ => 0
        };

        // Цвет неона
        _selectedColor = s.AccentColorHex;
        if (_selectedColor.Equals("#4285F4", StringComparison.OrdinalIgnoreCase)) RbBlue.IsChecked = true;
        else if (_selectedColor.Equals("#CBA6F7", StringComparison.OrdinalIgnoreCase)) RbPurple.IsChecked = true;
        else if (_selectedColor.Equals("#50FA7B", StringComparison.OrdinalIgnoreCase)) RbEmerald.IsChecked = true;
        else RbCyan.IsChecked = true;

        SldDimming.Value = s.DimmingOpacity * 100;
        SldThickness.Value = s.SelectionThickness;
        ChkAiOnly.IsChecked = s.AiOnlyMode;
        ChkStartup.IsChecked = s.StartWithWindows;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var s = _settingsManager.CurrentSettings;

        // Сохраняем модификаторы
        s.Modifiers = CmbModifiers.SelectedIndex switch
        {
            1 => 0x0001u | 0x0004u, // Alt + Shift
            2 => 0x0002u | 0x0001u, // Ctrl + Alt
            3 => 0x0008u | 0x0004u, // Win + Shift
            _ => 0x0002u | 0x0004u  // Ctrl + Shift
        };

        // Сохраняем клавишу
        s.VirtualKey = CmbKey.SelectedIndex switch
        {
            1 => 0x58u, // X
            2 => 0x43u, // C
            3 => 0x5Au, // Z
            4 => 0x44u, // D
            5 => 0x46u, // F
            _ => 0x53u  // S
        };

        s.DefaultDisplayMode = CmbDefaultMode.SelectedIndex switch
        {
            1 => WindowDisplayMode.Sidebar,
            2 => WindowDisplayMode.Expanded,
            _ => WindowDisplayMode.Compact
        };

        s.AccentColorHex = _selectedColor;
        s.DimmingOpacity = SldDimming.Value / 100.0;
        s.SelectionThickness = SldThickness.Value;
        s.AiOnlyMode = ChkAiOnly.IsChecked == true;
        s.StartWithWindows = ChkStartup.IsChecked == true;

        _settingsManager.Save();
        ApplyStartupRegistry(s.StartWithWindows);

        SettingsSaved?.Invoke();
        Close();
    }

    private static void ApplyStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            string exePath = Environment.ProcessPath ?? string.Empty;
            if (enable && !string.IsNullOrEmpty(exePath))
            {
                key.SetValue("CircleSearch", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("CircleSearch", false);
            }
        }
        catch { }
    }

    private void Color_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string hex)
        {
            _selectedColor = hex;
        }
    }

    private void SldDimming_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtDimVal != null) TxtDimVal.Text = $"{(int)e.NewValue}%";
    }

    private void SldThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtThickVal != null) TxtThickVal.Text = $"{e.NewValue:0.0} px";
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}