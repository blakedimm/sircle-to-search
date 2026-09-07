import os
import subprocess
from pathlib import Path

STRUCTURE = {
    "CircleSearch.Core": {
        "Math": [
            "Point2D.cs",
            "SplineInterpolator.cs",
            "PathSimplifier.cs"
        ],
        "Segmentation": [
            "IContourDetector.cs",
            "FastLocalContourDetector.cs",
            "MarchingSquares.cs",
            "ColorDistance.cs"
        ],
        "Imaging": [
            "ImageMasker.cs",
            "ImageEncoder.cs"
        ],
        "Models": [
            "ContourResult.cs",
            "SelectionBounds.cs"
        ]
    },
    "CircleSearch.Native": {
        "Capture": [
            "IScreenCapture.cs",
            "DxgiScreenCapture.cs",
            "GdiScreenCapture.cs",
            "CapturedFrame.cs"
        ],
        "Hooks": [
            "GlobalInputHook.cs",
            "HotkeyManager.cs"
        ],
        "Win32": [
            "User32.cs",
            "Dxgi.cs",
            "DpiHelper.cs"
        ]
    },
    "CircleSearch.Network": {
        "Services": [
            "ILensClient.cs",
            "GoogleLensClient.cs"
        ],
        "Models": [
            "SearchPayload.cs",
            "SearchResult.cs"
        ]
    },
    "CircleSearch.UI": {
        "Overlay": [
            "OverlayWindow.xaml",
            "OverlayWindow.xaml.cs",
            "SelectionCanvas.cs"
        ],
        "Shaders": [
            "NeonGlowEffect.cs"
        ],
        "Sidebar": [
            "LensSidebarWindow.xaml",
            "LensSidebarWindow.xaml.cs"
        ],
        "ViewModels": [
            "OverlayViewModel.cs",
            "SidebarViewModel.cs"
        ]
    },
    "CircleSearch.App": {
        "Services": [
            "AppCoordinator.cs",
            "SettingsManager.cs"
        ],
        "Tray": [
            "TrayIconManager.cs"
        ],
        "Config": [
            "AppSettings.cs"
        ]
    }
}

def create_file_with_namespace(file_path: Path, project_name: str, subfolder: str):
    file_name = file_path.name
    class_name = file_path.stem
    ns = f"{project_name}.{subfolder.replace('/', '.')}".strip('.')

    if file_name.endswith(".xaml"):
        content = f"""<Window x:Class="{ns}.{class_name}"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{class_name}">
    <Grid>
    </Grid>
</Window>
"""
    elif file_name.endswith(".xaml.cs"):
        window_name = class_name
        content = f"""using System.Windows;

namespace {ns};

public partial class {window_name} : Window
{{
    public {window_name}()
    {{
        InitializeComponent();
    }}
}}
"""
    elif class_name.startswith("I") and not class_name.startswith("Image"):
        content = f"""namespace {ns};

public interface {class_name}
{{
}}
"""
    else:
        content = f"""namespace {ns};

public class {class_name}
{{
}}
"""

    file_path.write_text(content, encoding="utf-8")

def run():
    root = Path.cwd()
    src_dir = root / "src"
    src_dir.mkdir(exist_ok=True)
    sln_path = root / "CircleSearch.sln"

    print("🚀 Создание решения .NET...")
    if not sln_path.exists():
        subprocess.run(["dotnet", "new", "sln", "-n", "CircleSearch"], cwd=root, check=False)

    projects = {
        "CircleSearch.Core": "classlib",
        "CircleSearch.Native": "classlib",
        "CircleSearch.Network": "classlib",
        "CircleSearch.UI": "wpflib",
        "CircleSearch.App": "wpf"
    }

    for proj_name, template in projects.items():
        proj_path = src_dir / proj_name
        csproj_file = proj_path / f"{proj_name}.csproj"

        if not csproj_file.exists():
            print(f"📦 Создание проекта {proj_name} ({template})...")
            subprocess.run(["dotnet", "new", template, "-n", proj_name, "-o", str(proj_path)], cwd=src_dir, check=False)
            default_class = proj_path / "Class1.cs"
            if default_class.exists():
                default_class.unlink()

        subprocess.run(["dotnet", "sln", "CircleSearch.sln", "add", str(csproj_file)], cwd=root, check=False)

    print("🔗 Настройка связей проектов...")
    app_proj = src_dir / "CircleSearch.App" / "CircleSearch.App.csproj"
    ui_proj = src_dir / "CircleSearch.UI" / "CircleSearch.UI.csproj"
    native_proj = src_dir / "CircleSearch.Native" / "CircleSearch.Native.csproj"
    net_proj = src_dir / "CircleSearch.Network" / "CircleSearch.Network.csproj"
    core_proj = src_dir / "CircleSearch.Core" / "CircleSearch.Core.csproj"

    subprocess.run(["dotnet", "add", str(native_proj), "reference", str(core_proj)], check=False)
    subprocess.run(["dotnet", "add", str(net_proj), "reference", str(core_proj)], check=False)
    subprocess.run(["dotnet", "add", str(ui_proj), "reference", str(core_proj)], check=False)
    subprocess.run(["dotnet", "add", str(ui_proj), "reference", str(native_proj)], check=False)
    subprocess.run(["dotnet", "add", str(app_proj), "reference", str(core_proj)], check=False)
    subprocess.run(["dotnet", "add", str(app_proj), "reference", str(native_proj)], check=False)
    subprocess.run(["dotnet", "add", str(app_proj), "reference", str(net_proj)], check=False)
    subprocess.run(["dotnet", "add", str(app_proj), "reference", str(ui_proj)], check=False)

    print("📥 Добавление WebView2...")
    subprocess.run(["dotnet", "add", str(ui_proj), "package", "Microsoft.Web.WebView2"], check=False)

    print("📁 Генерация архитектурных файлов...")
    for proj_name, folders in STRUCTURE.items():
        proj_base = src_dir / proj_name
        for folder, files in folders.items():
            target_folder = proj_base / folder
            target_folder.mkdir(parents=True, exist_ok=True)
            for file_name in files:
                target_file = target_folder / file_name
                if not target_file.exists():
                    create_file_with_namespace(target_file, proj_name, folder)

    print("\n✅ Структура успешно создана!")
    print("👉 Можно открывать CircleSearch.sln")

if __name__ == "__main__":
    run()