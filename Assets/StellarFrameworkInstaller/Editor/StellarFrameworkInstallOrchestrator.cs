using UnityEditor;

namespace StellarFrameworkInstaller
{
    internal sealed class StellarFrameworkInstallOrchestrator
    {
        private enum Flow
        {
            None,
            Basic,
            HotUpdate
        }

        private readonly StellarFrameworkDependencyInstaller _dependencyInstaller = new StellarFrameworkDependencyInstaller();
        private Flow _flow;
        private int _step;

        public void StartBasicInstall(StellarFrameworkInstallerState state)
        {
            if (state == null || state.IsBusy)
            {
                return;
            }

            _flow = Flow.Basic;
            _step = 0;
            state.Begin(StellarFrameworkInstallPhase.CheckingEnvironment);
            state.Report.AddMessage("开始安装基础框架。");
        }

        public void StartHotUpdateInstall(StellarFrameworkInstallerState state)
        {
            if (state == null || state.IsBusy)
            {
                return;
            }

            _flow = Flow.HotUpdate;
            _step = 0;
            state.Begin(StellarFrameworkInstallPhase.CheckingPackages);
            state.Report.AddMessage("开始安装 AA + HybridCLR 热更新能力。");
        }

        public void Tick(StellarFrameworkInstallerState state)
        {
            if (_flow == Flow.None || state == null || !state.IsBusy)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                state.Phase = StellarFrameworkInstallPhase.WaitingForCompile;
                return;
            }

            if (!_dependencyInstaller.Poll(state.Report))
            {
                return;
            }

            if (!state.Report.IsValid)
            {
                _flow = Flow.None;
                state.Fail(state.Report.Summary);
                return;
            }

            if (_flow == Flow.Basic)
            {
                TickBasicInstall(state);
            }
            else if (_flow == Flow.HotUpdate)
            {
                TickHotUpdateInstall(state);
            }
        }

        private void TickBasicInstall(StellarFrameworkInstallerState state)
        {
            switch (_step++)
            {
                case 0:
                    state.Phase = StellarFrameworkInstallPhase.InstallingDependencies;
                    _dependencyInstaller.InstallPackage(
                        StellarFrameworkInstallerConstants.NewtonsoftPackageId,
                        StellarFrameworkInstallerConstants.NewtonsoftVersion,
                        string.Empty,
                        state.Report);
                    return;
                case 1:
                    _dependencyInstaller.InstallPackage(
                        StellarFrameworkInstallerConstants.UniTaskPackageId,
                        string.Empty,
                        StellarFrameworkInstallerConstants.UniTaskGitUrl,
                        state.Report);
                    return;
                case 2:
                    state.Phase = StellarFrameworkInstallPhase.ImportingCore;
                    if (!StellarFrameworkCoreImporter.ImportCorePayloadOrSkipIfAlreadyPresent(state.Report))
                    {
                        _flow = Flow.None;
                        state.Fail(state.Report.Summary);
                    }

                    return;
                case 3:
                    state.Phase = StellarFrameworkInstallPhase.RefreshingAssets;
                    AssetDatabase.Refresh();
                    return;
                case 4:
                    _flow = Flow.None;
                    state.Complete();
                    EditorApplication.ExecuteMenuItem(StellarFrameworkInstallerConstants.ToolsHubMenuPath);
                    return;
            }
        }

        private void TickHotUpdateInstall(StellarFrameworkInstallerState state)
        {
            switch (_step++)
            {
                case 0:
                    state.Phase = StellarFrameworkInstallPhase.InstallingDependencies;
                    _dependencyInstaller.InstallPackage(
                        StellarFrameworkInstallerConstants.AddressablesPackageId,
                        StellarFrameworkInstallerConstants.AddressablesVersion,
                        string.Empty,
                        state.Report);
                    return;
                case 1:
                    _dependencyInstaller.InstallPackage(
                        StellarFrameworkInstallerConstants.HybridClrPackageId,
                        string.Empty,
                        StellarFrameworkInstallerConstants.HybridClrGitUrl,
                        state.Report);
                    return;
                case 2:
                    state.Phase = StellarFrameworkInstallPhase.WritingDefineSymbols;
                    StellarFrameworkDefineSymbolsUtility.AddDefinesForSelectedBuildTarget(
                        StellarFrameworkInstallerConstants.UnityAddressablesDefine,
                        StellarFrameworkInstallerConstants.HybridClrDefine);
                    state.Report.AddMessage("已写入当前 BuildTarget 的热更新宏。");
                    return;
                case 3:
                    state.Phase = StellarFrameworkInstallPhase.ImportingCore;
                    if (!StellarFrameworkCoreImporter.ImportHotUpdatePayloadOrSkipIfAlreadyPresent(state.Report))
                    {
                        _flow = Flow.None;
                        state.Fail(state.Report.Summary);
                    }

                    return;
                case 4:
                    state.Phase = StellarFrameworkInstallPhase.CreatingGameHotUpdateLayout;
                    StellarFrameworkHotUpdateLayoutInitializer.CreateDefaultLayout(state.Report);
                    return;
                case 5:
                    state.Phase = StellarFrameworkInstallPhase.CreatingAddressablesSettings;
                    StellarFrameworkAddressablesReflectionBridge.EnsureDefaultAddressablesSettings(state.Report);
                    return;
                case 6:
                    state.Phase = StellarFrameworkInstallPhase.WritingRuntimeSettings;
                    StellarFrameworkPostCoreReflectionBridge.EnsureResKitRuntimeSettings(state.Report);
                    StellarFrameworkPostCoreReflectionBridge.EnsureAAWorkflowConfig(state.Report);
                    return;
                case 7:
                    state.Phase = StellarFrameworkInstallPhase.CreatingGroups;
                    StellarFrameworkPostCoreReflectionBridge.TryApplyAAWorkflowDefaults(state.Report);
                    return;
                case 8:
                    state.Phase = StellarFrameworkInstallPhase.RefreshingAssets;
                    AssetDatabase.Refresh();
                    return;
                case 9:
                    _flow = Flow.None;
                    state.Complete();
                    EditorApplication.ExecuteMenuItem(StellarFrameworkInstallerConstants.ToolsHubMenuPath);
                    return;
            }
        }
    }
}
