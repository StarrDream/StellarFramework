namespace StellarFrameworkInstaller
{
    internal enum StellarFrameworkInstallPhase
    {
        Idle,
        CheckingEnvironment,
        InstallingDependencies,
        ImportingCore,
        RefreshingAssets,
        WaitingForCompile,
        CheckingPackages,
        WritingDefineSymbols,
        CreatingAddressablesSettings,
        CreatingGroups,
        CreatingGameHotUpdateLayout,
        WritingRuntimeSettings,
        Validating,
        Completed,
        Failed
    }

    internal sealed class StellarFrameworkInstallerState
    {
        public StellarFrameworkInstallPhase Phase = StellarFrameworkInstallPhase.Idle;
        public bool IsBusy;
        public bool PreferOfflinePackages;
        public string LastOfflinePackageDirectory = string.Empty;
        public readonly StellarFrameworkInstallerReport Report = new StellarFrameworkInstallerReport();

        public void Begin(StellarFrameworkInstallPhase phase)
        {
            IsBusy = true;
            Phase = phase;
            Report.Clear();
        }

        public void Complete()
        {
            IsBusy = false;
            Phase = StellarFrameworkInstallPhase.Completed;
        }

        public void Fail(string error)
        {
            IsBusy = false;
            Phase = StellarFrameworkInstallPhase.Failed;
            Report.AddError(error);
        }
    }
}
