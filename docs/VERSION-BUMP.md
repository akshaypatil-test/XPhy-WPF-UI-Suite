# Version bump (developers)

This suite uses **one** shipping version for SDK-style projects and the **MSI** setup project. Follow the steps below whenever you release a new build.

## 1. Bump the version

Edit **`Directory.Build.props`** at the repo root of this suite (`XPhy-WPF-UI-Suite\Directory.Build.props`).

Set **`Version`** to the new semantic version (examples: `2.0.5`, `2.1.0`):

```xml
<PropertyGroup Label="X-PHY product version">
  <Version>2.0.5</Version>
  <InformationalVersion>$(Version)</InformationalVersion>
</PropertyGroup>
```

- **`InformationalVersion`** should stay **`$(Version)`** so assemblies and UI show the same label.
- Projects under this folder (`InstallerUI`, `x_phy_wpf_ui`, etc.) pick this up via MSBuild.

## 2. Sync the setup project (vdproj)

The sync step copies **`Version`** into the Visual Studio Installer project and assigns a **new `ProductCode`** (GUID) so Windows Installer can treat the new MSI as a proper upgrade. **`UpgradeCode`** stays the same; **`PackageCode`** is **not** changed by sync (Visual Studio may still update it when you build the MSI).

Run **one** of these from a terminal (PowerShell or Command Prompt), depending on your current directory.

**If your shell is already in `XPhy-WPF-UI-Suite`:**

```bat
dotnet msbuild "InstallerUI\InstallerUI.csproj" -t:SyncVdprojProductVersion
```

**If your shell is in the backend root `X-PHY_WPF_Plus_Backend`:**

```bat
dotnet msbuild "XPhy-WPF-UI-Suite\InstallerUI\InstallerUI.csproj" -t:SyncVdprojProductVersion
```

Expected console output includes updated **`ProductVersion`** and **`ProductCode`**. If **`ProductVersion`** in the vdproj **already** matches **`Version`**, sync does nothing (no new `ProductCode`).

### Optional: sync on every build

To run the same sync automatically before SDK builds, set **`SyncVdprojOnBuild=true`** in `Directory.Build.props` or pass:

```bat
dotnet build -p:SyncVdprojOnBuild=true
```

Use this only if your team wants the vdproj always aligned without running the sync target manually.

## 3. Build order

1. **Sync** (step 2) so `X-PHY-Setup-WPF-UI-CPU\X-PHY-Setup-WPF-UI-CPU.vdproj` matches **`Version`** before you build the installer.
2. **Build application binaries** so the setup project packages current outputs:
   - From `XPhy-WPF-UI-Suite`, build **`x_phy_wpf_ui`**, **`x_phy_wpf_wrapper`** (native), and **`InstallerUI`** as you normally do (e.g. **Release** in Visual Studio, or `dotnet build` / MSBuild on the relevant `.csproj` / `.vcxproj`).
3. **Build the MSI** using **Visual Studio** with the **Microsoft Visual Studio Installer Projects** extension: build the **`X-PHY-Setup-WPF-UI-CPU`** project (or build the whole **`XPhy-WPF-UI-Suite.sln`** in **Release**). The `.vdproj` is not built with `dotnet` alone; use Visual Studio (or `devenv.com` if your pipeline supports it).

Ship the **MSI** from the setup project’s output folder together with **`InstallerUI.exe`** if your layout bundles them side by side.

## 4. What to commit

Commit at least:

- **`Directory.Build.props`** (new `Version`)
- **`X-PHY-Setup-WPF-UI-CPU\X-PHY-Setup-WPF-UI-CPU.vdproj`** (after sync: `ProductVersion` + `ProductCode`)

## Quick checklist

| Step | Action |
|------|--------|
| 1 | Set `<Version>` in `Directory.Build.props` |
| 2 | Run `SyncVdprojProductVersion` (correct path for your cwd) |
| 3 | Build app projects (Release as appropriate) |
| 4 | Build **`X-PHY-Setup-WPF-UI-CPU`** in Visual Studio |
| 5 | Commit props + vdproj (+ any project changes) |
