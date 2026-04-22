# Version bump (developers)

This guide lives at **`VERSION-BUMP.md`** in the **XPhy-WPF-UI-Suite** folder (suite root).

The suite uses **one** shipping version for SDK-style projects and the **MSI** setup project. Follow the steps below whenever you release a new build.

## 1. Bump the version

Edit **`Directory.Build.props`** at the suite root (`XPhy-WPF-UI-Suite\Directory.Build.props`).

Set **`Version`** to the new semantic version (examples: `2.0.5`, `2.1.0`), and set **`ReleaseDate`** to the ship date in **ISO `yyyy-MM-dd`** (this value is embedded in the app assembly and shown on **Settings → Release Date**):

```xml
<PropertyGroup Label="X-PHY product version">
  <Version>2.0.5</Version>
  <InformationalVersion>$(Version)</InformationalVersion>
  <ReleaseDate>2026-04-22</ReleaseDate>
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
   - From `XPhy-WPF-UI-Suite`, build **`x_phy_wpf_wrapper`** (native), **`x_phy_wpf_ui`** and **`InstallerUI`** as you normally do (e.g. **Release** in Visual Studio, or `dotnet build` / MSBuild on the relevant `.csproj` / `.vcxproj`).
3. **Build the MSI** using **Visual Studio** with the **Microsoft Visual Studio Installer Projects** extension: build the **`X-PHY-Setup-WPF-UI-CPU`** project (or build the whole **`XPhy-WPF-UI-Suite.sln`** in **Release**). The `.vdproj` is not built with `dotnet` alone; use Visual Studio (or `devenv.com` if your pipeline supports it).

Ship the **MSI** from the setup project’s output folder together with **`InstallerUI.exe`** if your layout bundles them side by side.

## 4. DigitalOcean Spaces — installer ZIP and `version.json`

After the MSI sits **next to** `InstallerUI.exe`, publish the bootstrap folder and point the global manifest at the new build.

**1. Prepare the folder**

- Build **InstallerUI** in **Release**. Typical output: **`InstallerUI\bin\Release\net48\`**.
- Copy the built **`.msi`** into that **`net48`** folder beside **`InstallerUI.exe`** (same folder you ship to users).

**2. Zip**

- Zip the **entire contents** of that **`net48`** Release folder (everything the user needs to run the installer).
- Name the archive **`x-phy-dfd-release-v{version}.zip`**, where **`{version}`** is the same value as `<Version>` in `Directory.Build.props` (example: `2.0.2` → `x-phy-dfd-release-v2.0.2.zip`).

**3. Upload to Spaces**

- In your **DigitalOcean Space** (example: bucket **`xphy-dfd-releases`**, region **`sgp1`**), create or use a folder **`v{version}/`** (example: **`v2.0.2/`**).
- Upload **`x-phy-dfd-release-v{version}.zip`** into that folder.
- Ensure objects are **public** (or served via a public CDN URL) if clients download without auth.

**4. Update `version.json`**

- Edit the manifest at the URL configured as **`AppUpdate:VersionManifestUrl`** in **XPhy.Licensing.Api** (example: `https://xphy-dfd-releases.sgp1.digitaloceanspaces.com/version.json` at the **root** of the Space).
- Set **`version`** to the new semver and **`url`** to the **direct HTTPS URL of the zip** under `v{version}/` (not a folder listing URL), for example:

```json
{
  "version": "2.0.2",
  "url": "https://xphy-dfd-releases.sgp1.digitaloceanspaces.com/v2.0.2/x-phy-dfd-release-v2.0.2.zip",
  "mandatory": false,
  "releaseNotes": ""
}
```

- The API compares **`version`** to the client’s current version; an update is offered only if the manifest version is **greater** (see `AppUpdateService` in **XPhy.Licensing.Api**).
- **`url`** becomes the download link returned to the app. Optional fields **`mandatory`** and **`releaseNotes`** are supported by the same service.
- Re-upload **`version.json`** after editing so the new latest build is visible immediately.

## 5. What to commit

Commit at least:

- **`Directory.Build.props`** (new `Version`)
- **`X-PHY-Setup-WPF-UI-CPU\X-PHY-Setup-WPF-UI-CPU.vdproj`** (after sync: `ProductVersion` + `ProductCode`)

`version.json` on DigitalOcean Spaces is **not** in this repo; track changes there per your release process.

## Quick checklist

| Step | Action |
|------|--------|
| 1 | Set `<Version>` and `<ReleaseDate>` (`yyyy-MM-dd`) in `Directory.Build.props` |
| 2 | Run `SyncVdprojProductVersion` (correct path for your cwd) |
| 3 | Build app projects (Release as appropriate) |
| 4 | Build **`X-PHY-Setup-WPF-UI-CPU`** in Visual Studio |
| 5 | Commit props + vdproj (+ any project changes) |
| 6 | Zip **`InstallerUI\bin\Release\net48\`** → **`x-phy-dfd-release-v{version}.zip`**, MSI next to **`InstallerUI.exe`** |
| 7 | Upload zip to Space under **`v{version}/`** |
| 8 | Update root **`version.json`**: **`version`**, **`url`** (HTTPS link to that zip) — §4 |
