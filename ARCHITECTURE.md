# XPhy WPF UI Suite — Architecture

Windows desktop **X-PHY Deepfake Detector**: WPF app + native wrapper + WPF bootstrap installer + Visual Studio **MSI** (`XPhy-WPF-UI-Suite.sln`). Shipping **version**: `Directory.Build.props` (sync vdproj per **`VERSION-BUMP.md`**). Public distribution artifact is a single installer EXE.

## Projects

| Project | Role |
|---------|------|
| **x_phy_wpf_ui** | .NET 4.8 WPF, x64. UI, licensing HTTP → **XPhy.Licensing.Api**, Stripe in **WebView2**, MaterialDesign, Newtonsoft.Json, SQLite. |
| **x_phy_wpf_wrapper** | C++/CLI x64 DLL: .NET ↔ native **detection** (`detection_program_lib`), OpenCV/TensorFlow via **vcpkg**, models → `bin\x_phy_wpf_wrapper\x64\...\`. |
| **InstallerUI** | WPF setup wizard; runs **`msiexec /i … /quiet /norestart INSTALLDIR=…`** (`InstallerViewModel`). Success: exit **0** or **3010**. MSI is embedded in installer EXE for shipping. Admin manifest. |
| **X-PHY-Setup-WPF-UI-CPU** | **.vdproj** MSI: one **INSTALLDIR**, files from wrapper + WPF outputs. New NuGet DLLs → add to vdproj manually. |

## Flow

- **User** → InstallerUI → silent MSI → **`x_phy_wpf_ui.exe`** (+ DLLs, `models\`, `config.toml` when present).
- **App** → `x_phy_wpf_wrapper.dll` for detection / native license work; **HTTP** `Services/*` for JWT auth, plans, payments, updates.
- **Licensing:** backend LMS activation sets expiry; app uses native Keygen + API validate (device fingerprint). **Callbacks:** native threads → **Dispatcher** before UI.

## Ship checklist

1. Wrapper **Release \| x64**, then **x_phy_wpf_ui** **Release \| x64** (not Any CPU/Debug for production MSI paths).
2. Build MSI in **Visual Studio** (Installer Projects extension). Shortcut / launch target: **`x_phy_wpf_ui.exe`** — keep vdproj and **`InstallerViewModel`** in sync if the exe name changes.
3. End users: **.NET 4.8** + **WebView2** for Stripe.
4. Publish single-file installer **`x-phy-dfd-v{version}.exe`** (example: **`x-phy-dfd-v2.3.exe`**) to **DigitalOcean Spaces** and update root **`version.json`** to point directly to that EXE (see **`VERSION-BUMP.md`** §4).

## Further reading

- **`BUILD_INSTRUCTIONS.md`** — machine setup, build order, troubleshooting.  
- **`VERSION-BUMP.md`** — version bump, vdproj sync, Spaces + **`version.json`**.
