# InstallerUI – Custom WPF Installer

This WPF application is the **only** UI the end user sees during setup. The MSI runs **silently** in the background.

## Why MSI dialogs are not used

- The built-in MSI wizard (Welcome, License, Install Dir, Progress, etc.) is replaced entirely by this WPF UI.
- The user experience is controlled here: flow, text, branding, and layout match the Figma design.
- The MSI is invoked with `msiexec /i "path" /quiet /norestart INSTALLDIR="..."` so it performs the actual installation (files, registry, shortcuts) without showing any windows.

## How the WPF installer controls the flow

1. **Welcome** → user clicks Next.
2. **License Agreement** → user must check "I accept" to enable Next.
3. **Install Path** → user can change the folder; Next runs the MSI silently.
4. **Progress** → simulated stages (Preparing, Installing, Finalizing) while the real MSI runs in the background.
5. **Finish** → success or failure message; Finish closes the installer.

MSI execution is in `InstallerViewModel.StartMsiInstall()`: it locates the MSI next to `InstallerUI.exe`, runs `msiexec`, and treats **ExitCode 0** and **3010** (success, restart required) as success. Any other code shows the failure screen.

## Requirements

- **Administrator**: The app requires admin (see `app.manifest`) so the MSI can install to Program Files.
- **MSI placement**: Place `X-PHY-Setup-WPF-UI-CPU.msi` (or `X-PHY-Setup.msi`) next to `InstallerUI.exe` when deploying the bootstrapper.
- **MSI configuration**: The MSI project (e.g. X-PHY-Setup-WPF-UI-CPU) should be set up for **silent install** (no UI dialogs) and must accept **INSTALLDIR** on the command line so the WPF installer can pass the chosen path.

## Screens

1. **Welcome** – Welcome text and “Click Next to continue”.
2. **License & Agreement** – EULA content (from `EULA.txt`, `EULA.rtf`, or `dependencies\eula\EULA.rtf`). **Next is enabled only after the user scrolls to the bottom** of the EULA. The primary button on this step is “I Accept”.
3. **Installation Preferences** – **Quick Install** (default path) or **Custom Install** (path + Browse). Checkbox: **Launch X-PHY Deepfake Detector automatically on system startup** (stored in `HKCU\...\Run` after a successful install).
4. **Installation Progress** – Simulated stages and **percentage progress** (0–100%).
5. **Finish** – Success or failure message. Checkbox: **Launch X-PHY Deepfake Detector now**; if checked, the app is started when the user clicks Finish.

## EULA content (X-PHY_DFD_EULA_20260107.docx)

- The installer loads EULA text from (in order): `EULA.txt`, `EULA.rtf` next to the exe, or `dependencies\eula\EULA.rtf`.
- To use the exact text from **X-PHY_DFD_EULA_20260107.docx**, export that document as plain text and save as **EULA.txt** in the InstallerUI project folder (or next to InstallerUI.exe when deploying). The project ships a default EULA.txt that you can replace.

## Output

- **InstallerUI.exe** – main installer entry point. Run this; it will show the WPF UI and run the MSI silently when the user clicks Next on the Installation Preferences screen.
