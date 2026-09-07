using CircleSearch.Core.Models;

namespace CircleSearch.App.Config;

public sealed class AppSettings
{
    public uint Modifiers { get; set; } = 0x0002 | 0x0004; // Ctrl + Shift
    public uint VirtualKey { get; set; } = 0x53;          // 'S'
    public WindowDisplayMode DefaultDisplayMode { get; set; } = WindowDisplayMode.Compact;
    public bool StartWithWindows { get; set; } = false;
    public double ContourTolerance { get; set; } = 38.0;

    // Стилизация и поведение
    public string AccentColorHex { get; set; } = "#00D2FF";
    public double DimmingOpacity { get; set; } = 0.40;
    public double SelectionThickness { get; set; } = 2.5;

    // Режим только ответа ИИ
    public bool AiOnlyMode { get; set; } = true;
}