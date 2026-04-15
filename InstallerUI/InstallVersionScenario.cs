namespace InstallerUI
{
    /// <summary>Result of comparing this installer package to an existing installation.</summary>
    public enum InstallVersionScenario
    {
        /// <summary>No existing product files detected.</summary>
        FreshInstall,
        /// <summary>Installed version equals this package (block reinstall).</summary>
        SameVersionInstalled,
        /// <summary>Installed version is older than this package (allow MSI upgrade).</summary>
        OlderVersionInstalled,
        /// <summary>Installed version is newer than this package (block downgrade).</summary>
        NewerVersionInstalled,
        /// <summary>Product files exist but version could not be read (allow MSI; upgrade/repair path).</summary>
        InstalledVersionUnknown
    }
}
