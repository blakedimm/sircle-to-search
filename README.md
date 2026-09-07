# 🔍 Circle to Search for Windows

<p align="center">
  <b>Полноценный нативный Circle to Search с Android прямо на вашем ПК с Windows 10/11.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat&logo=windows" alt="Platform" />
  <img src="https://img.shields.io/badge/UI-WPF%20%2B%20WebView2-2B2D42?style=flat" alt="UI" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=flat" alt="License" />
</p>

---

## ✨ Возможности

* **Быстрый захват по хоткею** — моментальный снимок экрана через Win32 API (`Ctrl + Shift + S` по умолчанию).
* **Плавный неоновый оверлей** — кастомизация цвета подсветки, толщины рамки и степени затемнения экрана.
* **Интеграция с Google Lens & Gemini** — автоматическая загрузка снимка и извлечение чистого текстового обзора без мусорных баннеров и кнопок.
* **Поддержка нескольких мониторов** — корректная работа с отрицательными координатами виртуального экрана и любым масштабированием DPI (100%–200%).
* **Экстремальная оптимизация памяти** — Chromium в фоне уходит в глубокую заморозку (`TrySuspendAsync`), а WPF сбрасывает неиспользуемые страницы через `EmptyWorkingSet`. Базовый процесс в трее держится в районе **15–25 МБ RAM**.
* **Постоянная сессия** — один раз входите в Google-аккаунт через защищенный WebView2-контейнер, сессия сохраняется навсегда.

---

## ⌨️ Горячие клавиши

| Сочетание | Действие |
| :--- | :--- |
| `Ctrl + Shift + S` | Активировать поиск по выделению |
| `Esc` / `ПКМ` | Отмена выделения / закрытие окна |
| `Ctrl + Shift + Q` | Полный выход из приложения |

---

## 🚀 Сборка из исходников

### Требования:
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
* Windows 10 (1809+) или Windows 11

### Сборка компактного Single-File EXE:
```bash
dotnet publish src/CircleSearch.App/CircleSearch.App.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

Исполняемый файл со всеми встроенными ресурсами появится в папке `./publish/CircleSearch.exe`.

---

## 🛠 Технологический стек

* **Ядро:** C# 13 / .NET 10
* **Графика и оверлей:** WPF, DirectX/GDI Interop, кастомный шейдер размытия
* **Анализ:** Microsoft WebView2, Chrome DevTools Protocol (CDP), Google Lens
* **Низкоуровневые хуки:** Win32 User32 Hooking, Per-Monitor DpiAwareness V2

---

## 📄 Лицензия

Проект распространяется под лицензией [MIT](LICENSE). Автор: **blakedimm** (2026).