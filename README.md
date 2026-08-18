# N Screen Recorder 🎥

**N Screen Recorder** is a lightweight, high-performance, and feature-rich screen recording and screenshotting utility built for Windows and macOS using **Avalonia UI (.NET 10)**.

Featuring a sleek, modern geometric "N" brand identity and native system integration, it runs silently in your system tray and lets you capture your screen, system audio, and microphone with extreme ease and precision.

---

## ✨ Features

### 🚀 Core Recording Capabilities
* **Full Screen & Custom Region Recording:** Toggle between recording your entire display or selecting a specific click-and-drag target area.
* **Ultra-Customizable Framerates (5 to 60 FPS):** Choose any integer framerate between 5 FPS and 60 FPS to record everything from low-framerate presentations to smooth 60 FPS gaming/motion capture.
* **H264 Quality-Based Video Compression:** Adjust video quality between 10% and 100% using a hardware-accelerated quality encoder, directly controlling both video visual fidelity and file size.
* **Simultaneous Dual-Audio Capture:** Toggle checkboxes to record system sounds (speakers/headphones) and microphone input simultaneously. Perfect for voiceovers, gaming commentary, or tutorials.
* **macOS & Windows Support:** Fully cross-platform architecture with native screenshotting (via GDI on Windows and `screencapture` on macOS) and optimized video capturing pipelines.

### 🎨 Premium User Experience & Branding
* **Modern Geometric "N" Branding:** Tailor-made, high-end visual logo used across the window title bars, installers, and system tray icon.
* **Dynamic Recording State Indicator:** The system tray and window icons dynamically append an active red recording dot to the "N" logo when recording is in progress, returning to clean white once recording is stopped.
* **Instant Auto-Saving Settings:** No manual "Save Settings" button is required! Every slider adjustment, checkbox toggle, or hotkey change is instantly written to your configuration in the background.
* **Clickable Highlight Notification:** As soon as a recording or screenshot finishes, a custom-designed notification window slides up in the bottom-right corner of your screen. Clicking it opens the native file explorer (Windows Explorer or macOS Finder) with your newly created file highlighted and focused.
* **Virtual Desktop Migration:** The option window follows you. If the options window is open on one Virtual Desktop, opening it from the system tray on another Virtual Desktop automatically migrates and focuses the window on your active desktop.
* **Start on Startup:** Toggle a setting to register the application in the system startup registry (on Windows, `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`), enabling the application to start minimized in the tray area on boot.
* **Global Hotkey Support:** Minimize the app to the tray and trigger recording globally at any time using a customizable hotkey (default: `Alt+S` on Windows).
* **Sleek Scrollable Layout:** Controls are wrapped in a smooth `ScrollViewer` layout, ensuring accessibility and comfort even on smaller monitors or higher DPI scaling.

---

## 🛠️ Technology Stack
* **UI Framework:** [Avalonia UI 12](https://github.com/AvaloniaUI/Avalonia) (.NET 10.0 multi-targeted)
* **Recording Engine**:
  * **Windows:** [ScreenRecorderLib](https://github.com/yCross/ScreenRecorderLib) (Native Windows Media Foundation wrappers)
  * **macOS:** System-native screen/audio capturing and mock recording workflows.
* **Screenshot Engine**:
  * **Windows:** Native GDI-based high-performance display context capturing.
  * **macOS:** Built-in high-resolution `screencapture` utility.
* **Design Pattern:** MVVM (Model-View-ViewModel) using the Community Toolkit MVVM
* **Installer & Packaging**:
  * **Windows:** Inno Setup (Packages self-contained bundles)
  * **macOS:** Native `.app` bundles packed inside standard ZIP containers.

---

## 📦 Automated Release Pipeline (CI/CD)

The project includes a robust, matrix-based **GitHub Actions** workflow (`.github/workflows/build-and-release.yml`):
* **Windows Build Runner:** Compiles a self-contained Windows `win-x64` release and packages it as an easy-to-use installer (`ScreenRecorderSetup.exe`) using Inno Setup.
* **macOS Build Runner:** Compiles self-contained macOS `osx-arm64` and `osx-x64` applications, bundles them as native `.app` application packages, and zips them.
* **Automatic Publishing:** Combines the installer and packaged zip bundles dynamically, uploading them to a rolling **"Latest Build"** pre-release on branch pushes, or stable release assets on tag releases (`v*`).

---

## ⚙️ How to Build Locally

To build and run **N Screen Recorder** locally, ensure you have the .NET 10 SDK installed.

1. **Clone the Repository:**
   ```bash
   git clone <repository-url>
   cd NScreenRecorder
   ```

2. **Restore and Build:**
   ```bash
   dotnet build
   ```

3. **Run the Application:**
   ```bash
   dotnet run --project ScreenRecorder.csproj
   ```

---

## 📜 License
Refer to the `LICENSE` file for details. Built utilizing open-source libraries including Avalonia UI (MIT) and ScreenRecorderLib (MIT).
