# X-PHY Setup – License Agreement, Welcome & Banner

**Note:** This project (X-PHY-Setup-WPF-UI-CPU) is the **MSI/setup installer only**. It does not perform license activation. License activation in the WPF suite works as follows:

- **Backend (XPhy.Licensing.Api):** When a trial or paid license is **created**, the backend calls the LMS **activate** API (e.g. `POST licenses/activate` with the license key) so that the license gets an **expiry** (e.g. one year from activation). Without this step, retrieving the license from LMS would return `expiry: null`.
- **WPF app (x_phy_wpf_ui):** When the user runs the installed app, **native** code (Keygen via `ApplicationController` + `KeygenLicenseManager` in the wrapper) validates the license and, if the machine is not yet registered, **registers the machine** with Keygen (“machine activation”). See `keygen_license_manager.h` and `ApplicationControllerWrapperNative.cpp` (e.g. `ActivationError` → “Machine activation unsuccessful.”). The app then calls **POST /api/License/validate** (with device fingerprint) to refresh license info from the backend, including the expiry set by LMS.

So: **create license → backend activates with LMS (sets expiry) → user runs app → native Keygen validates/registers machine → app calls validate to get expiry.**

---

## MSI project (X-PHY-Setup-WPF-UI-CPU) – standards

- **Output:** The MSI takes the main app (exe + .NET DLLs) from **x_phy_wpf_ui** build output: `..\bin\x_phy_wpf_ui\x64\Release\`. Native DLLs, `config.toml`, and the `models\` folder are taken from **x_phy_wpf_wrapper**: `..\bin\x_phy_wpf_wrapper\x64\Release\`. **Build order:** 1) Build **x_phy_wpf_wrapper** (Release|x64) so that `x_phy_wpf_wrapper\x64\Release` is populated. 2) Build **x_phy_wpf_ui** with **Configuration = Release** and **Platform = x64** so that `x_phy_wpf_ui\x64\Release` exists with the exe and all managed DLLs (including **MaterialDesignThemes.Wpf.dll** and **MaterialDesignColors.dll**). 3) Build the MSI (X-PHY-Setup-WPF-UI-CPU). If you build x_phy_wpf_ui with Any CPU or Debug, the MSI may not pick up the correct output and the installed app may fail to start.
- **Install location:** Default is `[ProgramFilesFolder]X-PHY\`. Overridden by **INSTALLDIR** when the installer is run by InstallerUI (e.g. `INSTALLDIR="C:\Program Files\X-PHY\X-PHY Deepfake Detector"`).
- **Single app folder (TARGETDIR):** All files (exe, DLLs, config, models, runtimes) go into one folder; no separate Program Menu or Desktop shortcuts from the MSI (InstallerUI creates the desktop shortcut).
- **.NET requirement:** .NET Framework 4.8 or later (LaunchCondition and bootstrapper). Debug and Release both use 4.8.
- **Product:** ProductName = "X-PHY Deepfake Detector", Manufacturer = "X-PHY", UpgradeCode and ProductCode for proper install/upgrade/uninstall.

## Custom WPF installer (InstallerUI) – MSI runs silently

When using the **InstallerUI** WPF application as the installer entry point:

- **The end user only sees the WPF UI.** The MSI is run silently in the background via:
  `msiexec /i "path" /quiet /norestart INSTALLDIR="<selected path>"`
- **MSI dialogs are not required.** With `/quiet`, the MSI shows no UI. The WPF app handles Welcome, License, Install Path, Progress, and Finish. The dialogs defined in the vdproj (Welcome, License Agreement, Installation Folder, Progress, Finished) are **never shown** when InstallerUI launches the MSI; they are only used if someone runs the MSI directly (e.g. double‑click the .msi) without using InstallerUI. You can leave them in the vdproj for that case or simplify/remove them if you only ever use InstallerUI.
- **INSTALLDIR:** The MSI must accept the `INSTALLDIR` property on the command line so the WPF installer can pass the user-selected install path.
- **Exit codes:** InstallerUI treats MSI exit code **0** (success) and **3010** (success, restart required) as success; any other code is shown as failure.

To deploy: place the built MSI (e.g. `X-PHY-Setup-WPF-UI-CPU.msi`) next to `InstallerUI.exe` so the WPF app can locate and run it.

**Desktop shortcut:** The **MSI** creates the desktop shortcut (in DesktopFolder) so that when the user uninstalls via Add/Remove Programs, the shortcut is removed. InstallerUI does not create a desktop shortcut (it no longer calls CreateDesktopShortcut).

The MSI project has **no custom dialogs** (all dialog entries in the vdproj User Interface have been removed). The only installer UI is the WPF **InstallerUI** app.

---

## If the app does not start after install

If the app starts from Visual Studio but fails to open after installation (e.g. nothing happens or it closes immediately):

1. **Build and MSI:** Ensure **x_phy_wpf_ui** is built with **Release | x64** (not Any CPU / not Debug). Then rebuild the MSI so it picks up files from `..\bin\x_phy_wpf_ui\x64\Release\`.
2. **New NuGet packages:** The MSI does **not** auto-include dependencies. When you add a new NuGet package to x_phy_wpf_ui, add the corresponding output DLL(s) to the setup project (vdproj **File** list) so they are installed with the app. For example, **MaterialDesignThemes.Wpf.dll**, **MaterialDesignColors.dll**, and **Microsoft.Xaml.Behaviors.dll** are explicitly listed in the vdproj. After adding new DLLs to the vdproj, **rebuild the setup project** and **reinstall** the MSI (the previously installed app will not get the new files until you do).

---

## Which .exe runs when the user clicks the icon

The application that runs when the user double‑clicks the desktop shortcut (or “Launch X-PHY Deepfake Detector now” on the Finish screen) is **x_phy_wpf_ui.exe** (the X-PHY Deepfake Detector WPF app).

### How it’s configured

1. **In the MSI (vdproj)**  
   The .exe is added as **Primary Output** from the **x_phy_wpf_ui** project:
   - In Visual Studio: open **X-PHY-Setup-WPF-UI-CPU** → **Application Folder** (or **Primary output from x_phy_wpf_ui**).
   - In the vdproj file: see the **ProjectOutput** section; it references:
     - **SourcePath** = `..\bin\x_phy_wpf_ui\x64\Release\x_phy_wpf_ui.exe` (the **x_phy_wpf_ui** project output path; ensure the WPF project is built first).
     - **OutputProjectGuid** = `{F07B28F5-A73F-E45D-141A-CDC56708B059}` (the x_phy_wpf_ui project).
     - **Folder** = the app folder (e.g. `[ProgramFilesFolder]X-PHY\`; overridden by INSTALLDIR when InstallerUI runs the MSI).
   - **TargetName** = empty, so the file is installed as **x_phy_wpf_ui.exe**.

2. **In InstallerUI**  
   The desktop shortcut and “Launch now” both target **x_phy_wpf_ui.exe** in the install folder. This is hardcoded in **InstallerUI** (`InstallerViewModel.cs`):
   - `CreateDesktopShortcut()` → `Path.Combine(installDir, "x_phy_wpf_ui.exe")`
   - `LaunchInstalledApp()` → same path
   - `ApplyLaunchOnStartup()` → same path

### Changing which .exe runs

- **To keep using x_phy_wpf_ui.exe but from a different build path**  
  In the vdproj, update **ProjectOutput** → **SourcePath** (and ensure the project reference **OutputProjectGuid** still points to the correct project), or in the IDE change “Primary output” to the correct project/configuration.

- **To use a different .exe (e.g. another project or a renamed exe)**  
  1. In the vdproj: either change the **ProjectOutput** to point to the other project’s primary output, or remove that and add a **File** that includes your .exe in the Application Folder.  
  2. In **InstallerUI** (`InstallerViewModel.cs`): replace the string **"x_phy_wpf_ui.exe"** with your exe name in `CreateDesktopShortcut()`, `LaunchInstalledApp()`, and `ApplyLaunchOnStartup()` (search for `x_phy_wpf_ui.exe`).

- **To install the exe under a different name**  
  In the vdproj, set **ProjectOutput** → **TargetName** to the desired file name (e.g. `MyApp.exe`). Then use that same name in InstallerUI as above.

---

## Stripe payment (Subscribe / Complete Your Payment)

The payment window uses **Microsoft Edge WebView2** to load Stripe card fields. On machines where the app is installed via the MSI:

1. **WebView2 Runtime** must be installed. If it is missing, the payment form may not load. Install from: [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/). Many machines already have it (e.g. via Windows Update or Edge).

2. **Writable user data folder**: The app uses a writable folder under the current user's **Local Application Data** (`%LocalAppData%\X-PHY\X-PHY Deepfake Detector\WebView2`) for WebView2 cache and payment HTML. This avoids "Access is denied (0x80070005)" when the app is installed under Program Files. No installer changes are required for this; the app creates the folder at runtime.

---

## Application icon (installed .exe)

The **application icon** (X-PHY logo) is set on the **.exe** itself. The WPF project (`x_phy_wpf_ui.csproj`) has `ApplicationIcon` set to **x-phy.ico** from `X-PHY Resource Files`, so the built `x_phy_wpf_ui.exe` shows the X-PHY icon in Explorer, on the taskbar, and when pinned. No desktop shortcut or icon file is created by the installer; only the exe in the install folder has the icon.
