# Build Instructions — XPhy WPF UI Suite (New Machine)

Use this guide when setting up and building the solution on a **new machine** or in a **new location**. This folder is self-contained; you can copy it anywhere and build.

---

## 1. Prerequisites

Install the following **before** opening the solution.

### Visual Studio 2022 (recommended) or 2019

- **Workloads**
  - **.NET desktop development** — for InstallerUI and x_phy_wpf_ui (C#/WPF)
  - **Desktop development with C++** — for x_phy_wpf_wrapper (C++/CLI)
- **Individual components** (usually installed with the workloads above)
  - MSVC v143 (or v142) build tools
  - Windows 10/11 SDK
  - C++ ATL for latest build tools (if prompted)

### vcpkg (for the C++ wrapper)

The wrapper project uses **vcpkg manifest mode**: dependencies (OpenCV, TensorFlow, etc.) are listed in `vcpkg.json` and restored when you build.

- **Option A — Visual Studio integration**  
  Install the **vcpkg** component from the Visual Studio Installer (Individual components → search “vcpkg”). No extra path setup needed.

- **Option B — Standalone vcpkg**  
  1. Clone vcpkg:  
     `git clone https://github.com/microsoft/vcpkg.git`  
  2. Run: `.\vcpkg\bootstrap-vcpkg.bat`  
  3. Set the system (or user) environment variable:  
     `VCPKG_ROOT` = path to the vcpkg folder  
  Visual Studio will then use it for manifest mode.

On first build, vcpkg will download and build packages; **internet access is required** for that step.

### This folder’s dependencies

The folder already contains:

- `dependencies\` — native libs, models, EULA (do not remove)
- `src\`, `external-headers\`, `X-PHY Resource Files\` — used by the solution

You do **not** need to copy anything from another repo; just keep this folder intact.

---

## 2. Open the solution

1. Copy or move the **XPhy-WPF-UI-Suite** folder to the desired location (e.g. `D:\Projects\XPhy-WPF-UI-Suite`).
2. Open **XPhy-WPF-UI-Suite.sln** from **inside that folder** in Visual Studio.  
   Do not move the `.sln` file; all paths are relative to this folder.

---

## 3. Build order

Build in this order. Later projects depend on outputs from earlier ones.

| Step | Project                  | Configuration / Platform   | Notes |
|------|--------------------------|----------------------------|--------|
| 1    | **x_phy_wpf_wrapper**    | `Prod_Release_CPU` \| `x64`| C++/CLI DLL. First build may run vcpkg (downloads). |
| 2    | **x_phy_wpf_ui**         | `Release` \| `x64` (or Debug) | Main WPF app. Copies wrapper DLL and deps to its output. |
| 3    | **InstallerUI**          | Any (e.g. Release \| Any CPU) | Optional; needed only if you build the installer. |
| 4    | **X-PHY-Setup-WPF-UI-CPU** | Release                   | Build **after** x_phy_wpf_ui so it can package the correct outputs. |

### In Visual Studio

1. Set solution configuration/platform (e.g. **Release** and **x64**).
2. Right‑click **x_phy_wpf_wrapper** → **Build**.
3. Then **x_phy_wpf_ui** → **Build**.
4. To run the app: set **x_phy_wpf_ui** as the startup project (right‑click → **Set as Startup Project**), then **F5** or **Ctrl+F5**.

### Build outputs

- Wrapper DLL and native deps:  
  `bin\x_phy_wpf_wrapper\x64\Prod_Release_CPU\`
- WPF app:  
  `bin\x_phy_wpf_ui\x64\Release\` (or Debug)
- Installer (after building the setup project):  
  `X-PHY-Setup-WPF-UI-CPU\Release\` (setup.exe and .msi)

---

## 4. Command-line build (optional)

If you prefer command line:

```bat
REM Open Developer Command Prompt for VS, then:
cd /d "D:\Path\To\XPhy-WPF-UI-Suite"

msbuild XPhy-WPF-UI-Suite.sln /p:Configuration=Release /p:Platform=x64 /t:x_phy_wpf_wrapper
msbuild XPhy-WPF-UI-Suite.sln /p:Configuration=Release /p:Platform=x64 /t:x_phy_wpf_ui
```

For the C# projects only (after the wrapper is already built):

```bat
dotnet build x_phy_wpf_ui\x_phy_wpf_ui.csproj -c Release
```

---

## 5. Common issues on a new machine

### “vcpkg not found” or package restore fails

- Ensure vcpkg is installed (Visual Studio component or standalone with `VCPKG_ROOT` set).
- Restart Visual Studio after installing vcpkg.
- First build of **x_phy_wpf_wrapper** needs network access so vcpkg can download and build packages.

### x_phy_wpf_ui cannot find x_phy_wpf_wrapper.dll

- Build **x_phy_wpf_wrapper** first (Prod_Release_CPU | x64).
- Confirm the DLL exists at:  
  `bin\x_phy_wpf_wrapper\x64\Prod_Release_CPU\x_phy_wpf_wrapper.dll`
- Rebuild **x_phy_wpf_ui** so it copies the DLL to its output.

### C++/CLI or linker errors in the wrapper

- Install the **Desktop development with C++** workload.
- Ensure **x64** is selected (not Win32).
- Check that `dependencies\lib` exists and contains the expected `.lib` files (e.g. detection_program_lib).

### Setup project (X-PHY-Setup-WPF-UI-CPU) fails or missing files

- Build **x_phy_wpf_ui** first so that `bin\x_phy_wpf_ui\x64\Release\` is populated.
- Then build the setup project.

---

## 6. Summary checklist (new machine)

- [ ] Visual Studio installed with **.NET desktop** and **C++ desktop** workloads
- [ ] vcpkg available (VS component or standalone + `VCPKG_ROOT`)
- [ ] **XPhy-WPF-UI-Suite** folder copied to desired location, not modified
- [ ] Open **XPhy-WPF-UI-Suite.sln** from inside that folder
- [ ] Build **x_phy_wpf_wrapper** (Prod_Release_CPU | x64), then **x_phy_wpf_ui** (Release | x64)
- [ ] Set **x_phy_wpf_ui** as startup project and run (F5) to test

For more context on the projects and this folder, see **README.md** in the same directory.
