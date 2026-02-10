namespace InstallerUI
{
    /// <summary>
    /// Installer flow steps. The WPF UI drives the flow; the MSI is only invoked
    /// when we reach Progress and run it silently (no MSI dialogs shown).
    /// </summary>
    public enum InstallerStep
    {
        Welcome,
        LicenseAgreement,
        InstallPath,
        Progress,
        Finish
    }
}
