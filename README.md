# X-PHY WPF UI Suite

This solution contains **only** the WPF-based application and installer. It is a separate, focused codebase that does not include the older tray or desktop UI projects.

## Contents

| Project | Description |
|--------|-------------|
| **InstallerUI** | WPF installer UI (welcome, EULA, install path, progress, finish). Used as the bootstrapper UI when the user runs setup. |
| **x_phy_wpf_wrapper** | C++/CLI wrapper that bridges the native detection engine to the WPF app. Build this first (Visual Studio required). |
| **x_phy_wpf_ui** | Main WPF application (login, detection, settings). Depends on the wrapper DLL. |
| **X-PHY-Setup-WPF-UI-CPU** | MSI/setup project that produces the installable package (setup.exe + .msi). Packs InstallerUI, WPF UI output, wrapper DLLs, and dependencies. |

## Dependencies (same repo)

- **Solution Items**: `vcpkg-configuration.json`, `vcpkg.json`, `XPhyDualPropertySheet.props` (needed for the wrapper build).
- **src/** – Native headers and shared code used by the wrapper.
- **external-headers/** – Referenced by the wrapper.
- **X-PHY Resource Files/** – Icons and images used by InstallerUI and WPF UI.
- **config.toml** – Optional; can be copied to output for development.
- **dependencies/** – EULA and any other installer/runtime deps (if used by InstallerUI/setup).

The wrapper also depends on vcpkg libraries and native libs (e.g. detection_program_lib, OpenCV, TensorFlow) as defined in `XPhyDualPropertySheet.props` and the wrapper project.

## Build order

1. **x_phy_wpf_wrapper** (Visual Studio; requires C++ workload and vcpkg).
2. **x_phy_wpf_ui** (Visual Studio or `dotnet build`; expects wrapper DLL in `bin\x_phy_wpf_wrapper\x64\Prod_Release_CPU\` or as configured).
3. **InstallerUI** (any time).
4. **X-PHY-Setup-WPF-UI-CPU** – Build after WPF UI (and InstallerUI if the setup includes it) so it can package the correct outputs.

## This folder (self-contained)

The **XPhy-WPF-UI-Suite** folder contains the solution, all four projects, and their dependencies. You can build and run from this folder alone:

- **Solution**: `XPhy-WPF-UI-Suite.sln`
- **Projects**: `InstallerUI`, `x_phy_wpf_wrapper`, `x_phy_wpf_ui`, `X-PHY-Setup-WPF-UI-CPU`
- **Dependencies**: `src`, `external-headers`, `X-PHY Resource Files`, `dependencies`, `vcpkg.json`, `vcpkg-configuration.json`, `XPhyDualPropertySheet.props`, `config.toml`

All paths in the solution and projects are relative to this folder.

## Opening the solution

- Open **XPhy-WPF-UI-Suite.sln** from this folder (`XPhy-WPF-UI-Suite`).
- Do **not** move the solution file to a different folder; the wrapper and other projects rely on paths relative to the solution directory.

## What this solution does *not* include

- **x_phy_detection_program_tray** – Tray-based detection app.
- **x_phy_detection_program_ui** – Desktop UI detection app.
- **X-PHY-Setup-CPU** / **X-PHY-Setup-UI-CPU** – Other installer projects.
- **CustomActions** – Not in this solution; add back if the WPF setup project is configured to use them.

For the full repo (all apps and installers), use **XPhyDeepfakeDetectionExe.sln** instead.
