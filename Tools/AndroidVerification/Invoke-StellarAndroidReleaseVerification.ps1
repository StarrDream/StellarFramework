param(
    [string] $UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe',
    [string] $UnitySkillsUrl = 'http://localhost:8090',
    [string] $ApkPath = '',
    [int] $BuildTimeoutMinutes = 30,
    [int] $RuntimeSeconds = 15,
    [int] $RestartRuntimeSeconds = 5,
    [switch] $HotUpdate,
    [int] $CdnPort = 18743,
    [string] $PythonExe = 'python.exe',
    [switch] $SkipBuild,
    [switch] $ForceBatchModeBuild,
    [switch] $KeepEmulator
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$defaultApkPath = Join-Path $projectRoot 'Builds\AndroidVerification\StellarFramework-ArchitectureDemo-x86_64-release.apk'
$hotUpdateDefaultApkPath = Join-Path $projectRoot 'Builds\AndroidVerification\StellarFramework-HotUpdate-x86_64-release.apk'
$selectedDefaultApkPath = if ($HotUpdate) { $hotUpdateDefaultApkPath } else { $defaultApkPath }

if ([string]::IsNullOrWhiteSpace($ApkPath))
{
    $ApkPath = $selectedDefaultApkPath
}
elseif (-not [IO.Path]::IsPathRooted($ApkPath))
{
    $ApkPath = Join-Path $projectRoot $ApkPath
}

$ApkPath = [IO.Path]::GetFullPath($ApkPath)
$defaultApkPath = [IO.Path]::GetFullPath($defaultApkPath)
$hotUpdateDefaultApkPath = [IO.Path]::GetFullPath($hotUpdateDefaultApkPath)
$selectedDefaultApkPath = [IO.Path]::GetFullPath($selectedDefaultApkPath)
$buildStatePath = if ($HotUpdate) {
    Join-Path $projectRoot 'Library\StellarFramework\AndroidVerification\android-hotupdate-build-state.json'
} else {
    Join-Path $projectRoot 'Library\StellarFramework\AndroidVerification\android-build-state.json'
}
$prepareStatePath = Join-Path $projectRoot 'Temp\StellarHotUpdateVerification\android-release-preparation.json'
$runDirectory = Join-Path $PSScriptRoot ("Results\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$buildLog = Join-Path $runDirectory 'unity-build.log'
$pipelineResultPath = Join-Path $runDirectory 'pipeline-result.json'
$smokeResultPath = Join-Path $runDirectory 'result.json'
$startedAt = [DateTimeOffset]::Now

New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$pipelineResult = [ordered]@{
    status = 'FAIL'
    startedAt = $startedAt.ToString('o')
    completedAt = $null
    projectRoot = $projectRoot
    apk = $ApkPath
    profile = if ($HotUpdate) { 'HotUpdate' } else { 'ArchitectureDemoSmoke' }
    buildMode = if ($SkipBuild) { 'Skipped' } else { $null }
    buildResult = $null
    buildWarnings = $null
    buildRequestTransportStatus = $null
    buildRequestTransportWarning = $null
    emulatorMemoryMegabytes = $null
    emulatorMemTotalKilobytes = $null
    smokeResult = $null
    hotUpdatePreparation = $null
    hotUpdateCdn = $null
    hotUpdateRuntime = $null
    adbReversePort = $null
    productVerificationStatus = 'NOT_RUN'
    cleanupStatus = 'NotStarted'
    cleanupFailures = @()
    artifactsDirectory = $runDirectory
    failure = $null
}

function Get-StellarUnitySkillsHealth
{
    param([Parameter(Mandatory = $true)] [string] $BaseUrl)

    try
    {
        $uri = $BaseUrl.TrimEnd('/') + '/health'
        $health = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 3
        if ($health.status -ne 'ok' -or $health.projectName -ne 'StellarFramework')
        {
            return $null
        }

        return $health
    }
    catch
    {
        return $null
    }
}

function Read-StellarBuildState
{
    param([Parameter(Mandatory = $true)] [string] $Path)

    if (-not (Test-Path -LiteralPath $Path))
    {
        return $null
    }

    try
    {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch
    {
        return $null
    }
}

function Wait-StellarBuildState
{
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [int] $TimeoutMinutes
    )

    $deadline = (Get-Date).AddMinutes([Math]::Max(1, $TimeoutMinutes))
    $nextHeartbeat = Get-Date

    while ((Get-Date) -lt $deadline)
    {
        $state = Read-StellarBuildState -Path $Path
        if ($null -ne $state)
        {
            if ($state.status -eq 'PASS' -or $state.status -eq 'FAIL')
            {
                return $state
            }

            if ((Get-Date) -ge $nextHeartbeat)
            {
                Write-Host "Unity Android build state: $($state.status)"
                $nextHeartbeat = (Get-Date).AddSeconds(30)
            }
        }
        elseif ((Get-Date) -ge $nextHeartbeat)
        {
            Write-Host 'Waiting for Unity Android build state...'
            $nextHeartbeat = (Get-Date).AddSeconds(30)
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out after $TimeoutMinutes minute(s) waiting for Unity Android build state: $Path"
}

function Invoke-StellarUnitySkillsBuild
{
    param(
        [Parameter(Mandatory = $true)] [string] $BaseUrl,
        [Parameter(Mandatory = $true)] [string] $StatePath,
        [Parameter(Mandatory = $true)] [int] $TimeoutMinutes,
        [Parameter(Mandatory = $true)] [string] $MenuPath
    )

    Remove-Item -LiteralPath $StatePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath ($StatePath + '.tmp') -Force -ErrorAction SilentlyContinue

    $uri = $BaseUrl.TrimEnd('/') + '/skill/editor_execute_menu'
    $body = @{
        menuPath = $MenuPath
    } | ConvertTo-Json -Compress

    Write-Host "Scheduling Android Release build through UnitySkills: $BaseUrl"
    $request = @{
        Uri = $uri
        Method = 'Post'
        ContentType = 'application/json'
        Body = $body
        TimeoutSec = [Math]::Max(60, $TimeoutMinutes * 60)
    }
    $response = $null
    $transportWarning = $null
    try {
        $response = Invoke-RestMethod @request
    }
    catch {
        $statusCode = 0
        if ($null -ne $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        if ($statusCode -ne 504) {
            throw
        }

        $transportWarning = $_.Exception.Message
        Write-Warning 'UnitySkills returned HTTP 504 while the Editor build may still be completing; awaiting the authoritative build state file.'
    }

    if ($null -ne $response -and $response.status -ne 'success')
    {
        throw 'UnitySkills did not accept the Android Release build request.'
    }

    $state = Wait-StellarBuildState -Path $StatePath -TimeoutMinutes $TimeoutMinutes
    if ($null -ne $transportWarning) {
        $outputPath = [string]$state.outputPath
        if ($state.status -ne 'PASS' -or $state.buildResult -ne 'Succeeded' -or
            [string]::IsNullOrWhiteSpace($outputPath) -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
            throw "UnitySkills returned HTTP 504 and the completed Android Release build state/artifact is not a proven PASS: status=$($state.status), buildResult=$($state.buildResult), outputPath=$outputPath. $transportWarning"
        }

        $state | Add-Member -NotePropertyName requestTransportStatus `
            -NotePropertyValue 'GatewayTimeoutRecoveredFromBuildState' -Force
        $state | Add-Member -NotePropertyName requestTransportWarning `
            -NotePropertyValue $transportWarning -Force
    }
    else {
        $state | Add-Member -NotePropertyName requestTransportStatus -NotePropertyValue 'Completed' -Force
        $state | Add-Member -NotePropertyName requestTransportWarning -NotePropertyValue $null -Force
    }

    return $state
}

function Invoke-StellarBatchModeBuild
{
    param(
        [Parameter(Mandatory = $true)] [string] $Executable,
        [Parameter(Mandatory = $true)] [string] $ProjectPath,
        [Parameter(Mandatory = $true)] [string] $OutputApk,
        [Parameter(Mandatory = $true)] [string] $StatePath,
        [Parameter(Mandatory = $true)] [string] $LogPath,
        [switch] $HotUpdate
    )

    if (-not (Test-Path -LiteralPath $Executable))
    {
        throw "Unity executable not found: $Executable"
    }

    Remove-Item -LiteralPath $StatePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath ($StatePath + '.tmp') -Force -ErrorAction SilentlyContinue

    $previousVerificationApk = $env:STELLAR_ANDROID_VERIFICATION_APK
    $previousHotUpdateApk = $env:STELLAR_ANDROID_HOTUPDATE_APK
    try {
        if ($HotUpdate) {
            $env:STELLAR_ANDROID_HOTUPDATE_APK = $OutputApk
            $executeMethod = 'StellarFrameworkAndroidReleaseVerificationBuild.BuildHotUpdateRelease'
        } else {
            $env:STELLAR_ANDROID_VERIFICATION_APK = $OutputApk
            $executeMethod = 'StellarFrameworkAndroidReleaseVerificationBuild.BuildRelease'
        }
        $unityArgs = @(
            '-batchmode',
            '-nographics',
            '-quit',
            '-projectPath', $ProjectPath,
            '-buildTarget', 'Android',
            '-executeMethod', $executeMethod,
            '-logFile', $LogPath
        )

        Write-Host "Building Android Release APK with Unity batchmode: $Executable"
        & $Executable @unityArgs
    }
    finally {
        if ($null -eq $previousVerificationApk) {
            Remove-Item Env:\STELLAR_ANDROID_VERIFICATION_APK -ErrorAction SilentlyContinue
        } else {
            $env:STELLAR_ANDROID_VERIFICATION_APK = $previousVerificationApk
        }
        if ($null -eq $previousHotUpdateApk) {
            Remove-Item Env:\STELLAR_ANDROID_HOTUPDATE_APK -ErrorAction SilentlyContinue
        } else {
            $env:STELLAR_ANDROID_HOTUPDATE_APK = $previousHotUpdateApk
        }
    }
    if ($LASTEXITCODE -ne 0)
    {
        throw "Unity Android Release build failed with exit code $LASTEXITCODE. See: $LogPath"
    }

    $state = Read-StellarBuildState -Path $StatePath
    if ($null -eq $state)
    {
        throw "Unity exited without producing build state: $StatePath"
    }

    return $state
}

function Invoke-StellarBatchModeHotUpdatePreparation
{
    param(
        [Parameter(Mandatory = $true)] [string] $Executable,
        [Parameter(Mandatory = $true)] [string] $ProjectPath,
        [Parameter(Mandatory = $true)] [string] $StatePath,
        [Parameter(Mandatory = $true)] [string] $LogPath,
        [Parameter(Mandatory = $true)] [int] $TimeoutMinutes
    )

    if (-not (Test-Path -LiteralPath $Executable)) {
        throw "Unity executable not found: $Executable"
    }

    Remove-Item -LiteralPath $StatePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath ($StatePath + '.tmp') -Force -ErrorAction SilentlyContinue
    $unityArgs = @(
        '-batchmode',
        '-nographics',
        '-quit',
        '-projectPath', $ProjectPath,
        '-buildTarget', 'Android',
        '-executeMethod', 'StellarFrameworkVerification.Editor.YooAssetHotUpdateVerificationBuilder.PrepareAndroidReleaseHotUpdate',
        '-logFile', $LogPath
    )

    Write-Host "Preparing Android HybridCLR artifacts and YooAsset package with Unity batchmode: $Executable"
    & $Executable @unityArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Unity Android HotUpdate preparation failed with exit code $LASTEXITCODE. See: $LogPath"
    }

    $state = Read-StellarBuildState -Path $StatePath
    if ($null -eq $state) {
        throw "Unity exited without producing Android HotUpdate preparation state: $StatePath"
    }
    return $state
}

function Invoke-StellarUnitySkillsPreparation
{
    param(
        [Parameter(Mandatory = $true)] [string] $BaseUrl,
        [Parameter(Mandatory = $true)] [string] $StatePath,
        [Parameter(Mandatory = $true)] [int] $TimeoutMinutes
    )

    Remove-Item -LiteralPath $StatePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath ($StatePath + '.tmp') -Force -ErrorAction SilentlyContinue
    $uri = $BaseUrl.TrimEnd('/') + '/skill/editor_execute_menu'
    $body = @{
        menuPath = 'Tools/StellarFramework/Verification/Prepare Android HotUpdate Release Gate'
    } | ConvertTo-Json -Compress

    Write-Host "Preparing Android HybridCLR artifacts through UnitySkills: $BaseUrl"
    $request = @{
        Uri = $uri
        Method = 'Post'
        ContentType = 'application/json'
        Body = $body
        TimeoutSec = [Math]::Max(60, $TimeoutMinutes * 60)
    }
    $response = Invoke-RestMethod @request
    if ($response.status -ne 'success') {
        throw 'UnitySkills did not accept Android HotUpdate preparation.'
    }

    return Wait-StellarBuildState -Path $StatePath -TimeoutMinutes $TimeoutMinutes
}

function Start-StellarVerificationCdn
{
    param(
        [Parameter(Mandatory = $true)] [string] $PackageDirectory,
        [Parameter(Mandatory = $true)] [int] $Port,
        [Parameter(Mandatory = $true)] [string] $RunDirectory
    )

    $pythonCommand = Get-Command -Name $PythonExe -ErrorAction Stop
    $cdnScript = Join-Path $PSScriptRoot 'StellarVerificationCdn.py'
    $accessLog = Join-Path $RunDirectory 'cdn-requests.jsonl'
    $stdout = Join-Path $RunDirectory 'cdn-stdout.log'
    $stderr = Join-Path $RunDirectory 'cdn-stderr.log'
    $argumentLine = '"{0}" --root "{1}" --host 127.0.0.1 --port {2} --access-log "{3}"' -f `
        $cdnScript, $PackageDirectory, $Port, $accessLog
    $process = Start-Process -FilePath $pythonCommand.Source `
        -ArgumentList $argumentLine `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr

    try {
        $deadline = (Get-Date).AddSeconds(30)
        $health = $null
        while ((Get-Date) -lt $deadline) {
            $process.Refresh()
            if ($process.HasExited) {
                throw "Verification CDN exited during startup (code $($process.ExitCode)). See $stderr"
            }
            try {
                $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 2
                if ($health.status -eq 'ok') { break }
            }
            catch {
                $health = $null
            }
            Start-Sleep -Milliseconds 300
        }

        if ($null -eq $health) {
            throw "Verification CDN did not become healthy on 127.0.0.1:$Port. See $stderr"
        }

        $expectedRoot = [IO.Path]::GetFullPath($PackageDirectory)
        $reportedRoot = [IO.Path]::GetFullPath([string]$health.root)
        if (-not [string]::Equals($expectedRoot, $reportedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Verification CDN serves '$reportedRoot'; expected package directory '$expectedRoot'."
        }

        return [pscustomobject]@{
            Process = $process
            Port = $Port
            PackageDirectory = $PackageDirectory
            AccessLog = $accessLog
            Pid = $process.Id
            Url = "http://127.0.0.1:$Port"
        }
    }
    catch {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
        throw
    }
}

$startedEmulator = $false
$verificationCdn = $null
$verificationSerial = $null
$adbReverseActive = $false
$exitCode = 0

try
{
    Assert-StellarAndroidEnvironment

    if ($HotUpdate -and $SkipBuild) {
        throw '-HotUpdate always regenerates Android artifacts and builds a fresh non-Development IL2CPP APK; -SkipBuild is not valid for this profile.'
    }
    if ($HotUpdate -and ($CdnPort -lt 1 -or $CdnPort -gt 65535)) {
        throw '-CdnPort must be in 1..65535.'
    }

    if (-not $SkipBuild)
    {
        $health = $null
        if (-not $ForceBatchModeBuild -and $ApkPath -eq $selectedDefaultApkPath)
        {
            $health = Get-StellarUnitySkillsHealth -BaseUrl $UnitySkillsUrl
        }

        if ($HotUpdate) {
            if ($null -ne $health) {
                $pipelineResult.buildMode = 'UnitySkills'
                $preparation = Invoke-StellarUnitySkillsPreparation `
                    -BaseUrl $UnitySkillsUrl `
                    -StatePath $prepareStatePath `
                    -TimeoutMinutes $BuildTimeoutMinutes
            }
            else {
                $pipelineResult.buildMode = 'BatchMode'
                $preparation = Invoke-StellarBatchModeHotUpdatePreparation `
                    -Executable $UnityExe `
                    -ProjectPath $projectRoot `
                    -StatePath $prepareStatePath `
                    -LogPath (Join-Path $runDirectory 'unity-hotupdate-prepare.log') `
                    -TimeoutMinutes $BuildTimeoutMinutes
            }

            $pipelineResult.hotUpdatePreparation = $preparation
            if ($preparation.status -ne 'PASS') {
                throw "Android HotUpdate preparation state is '$($preparation.status)': $($preparation.error)"
            }
            if ($preparation.buildTarget -ne 'Android' -or
                $preparation.manifestBuildTarget -ne 'Android' -or
                $preparation.scriptingBackend -ne 'IL2CPP' -or
                @($preparation.aotMetadataKeys).Count -ne 4 -or
                [int]$preparation.packageBundleCount -le 0) {
                throw 'Android preparation did not prove Android IL2CPP generation, four AOT metadata keys, and a built YooAsset package.'
            }
            if (-not (Test-Path -LiteralPath $preparation.packageDirectory -PathType Container)) {
                throw "Prepared Android YooAsset package directory is missing: $($preparation.packageDirectory)"
            }

            $verificationCdn = Start-StellarVerificationCdn `
                -PackageDirectory $preparation.packageDirectory `
                -Port $CdnPort `
                -RunDirectory $runDirectory
            $pipelineResult.hotUpdateCdn = [ordered]@{
                url = $verificationCdn.Url
                port = $verificationCdn.Port
                processId = $verificationCdn.Pid
                packageDirectory = $verificationCdn.PackageDirectory
                accessLog = $verificationCdn.AccessLog
            }
        }

        if ($null -ne $health)
        {
            if (-not $HotUpdate) { $pipelineResult.buildMode = 'UnitySkills' }
            Write-Host "Using open Unity $($health.unityVersion) instance '$($health.instanceId)'."
            $menuPath = if ($HotUpdate) {
                'Tools/StellarFramework/Verification/Build Android Release HotUpdate Verification APK'
            } else {
                'Tools/StellarFramework/Verification/Build Android Release Verification APK'
            }
            $buildState = Invoke-StellarUnitySkillsBuild `
                -BaseUrl $UnitySkillsUrl `
                -StatePath $buildStatePath `
                -TimeoutMinutes $BuildTimeoutMinutes `
                -MenuPath $menuPath
        }
        else
        {
            $pipelineResult.buildMode = 'BatchMode'
            $buildState = Invoke-StellarBatchModeBuild `
                -Executable $UnityExe `
                -ProjectPath $projectRoot `
                -OutputApk $ApkPath `
                -StatePath $buildStatePath `
                -LogPath $buildLog `
                -HotUpdate:$HotUpdate
        }

        $pipelineResult.buildResult = $buildState.buildResult
        $pipelineResult.buildWarnings = $buildState.totalWarnings
        $pipelineResult.buildRequestTransportStatus = $buildState.requestTransportStatus
        $pipelineResult.buildRequestTransportWarning = $buildState.requestTransportWarning
        if ($buildState.status -ne 'PASS')
        {
            throw "Unity Android Release build state is '$($buildState.status)': $($buildState.error)"
        }
        if ($HotUpdate -and
            ($buildState.profile -ne 'HotUpdate' -or
             $buildState.scriptingBackend -ne 'IL2CPP' -or
             $buildState.architectures -notmatch 'X86_64' -or
             $buildState.developmentBuild -or
             $buildState.buildTarget -ne 'Android')) {
            throw 'Android HotUpdate APK state does not prove a non-Development Android IL2CPP x86_64 build.'
        }
    }

    if (-not (Test-Path -LiteralPath $ApkPath))
    {
        throw "Verification APK not found: $ApkPath"
    }

    $existingSerial = Get-StellarEmulatorSerial
    $startedEmulator = [string]::IsNullOrWhiteSpace($existingSerial)
    $emulatorMemoryMegabytes = if ($HotUpdate) { 4096 } else { 2048 }

    if ($HotUpdate -and -not $startedEmulator) {
        $existingMemoryKilobytes = Get-StellarAndroidMemoryKilobytes -Serial $existingSerial
        if ($existingMemoryKilobytes -lt (3584L * 1024L)) {
            throw "The running StellarFramework_API35 emulator has only $([Math]::Floor($existingMemoryKilobytes / 1024)) MB RAM. Stop that AVD and rerun -HotUpdate so the existing launcher can start it with 4096 MB."
        }
    }

    & "$PSScriptRoot\Start-StellarAndroid.ps1" `
        -LogDirectory $runDirectory `
        -MemoryMegabytes $emulatorMemoryMegabytes | Out-Host

    if ($HotUpdate) {
        $verificationSerial = Get-StellarEmulatorSerial
        if ([string]::IsNullOrWhiteSpace($verificationSerial)) {
            throw "Unable to resolve the dedicated '$script:StellarAvdName' AVD after startup."
        }
        $pipelineResult.emulatorMemoryMegabytes = $emulatorMemoryMegabytes
        $pipelineResult.emulatorMemTotalKilobytes =
            Get-StellarAndroidMemoryKilobytes -Serial $verificationSerial
        if ($pipelineResult.emulatorMemTotalKilobytes -lt (3584L * 1024L)) {
            throw "The HotUpdate gate requires at least 3584 MB visible Android RAM; device reports $([Math]::Floor($pipelineResult.emulatorMemTotalKilobytes / 1024)) MB."
        }
    }

    if ($HotUpdate) {
        & $script:StellarAdb -s $verificationSerial reverse "tcp:$CdnPort" "tcp:$CdnPort" | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "adb reverse failed for verification port $CdnPort on $verificationSerial."
        }
        $adbReverseActive = $true
        $pipelineResult.adbReversePort = $CdnPort
    }

    $effectiveRuntimeSeconds = if ($HotUpdate) { [Math]::Max(90, $RuntimeSeconds) } else { $RuntimeSeconds }
    $effectiveRestartSeconds = if ($HotUpdate) { [Math]::Max(30, $RestartRuntimeSeconds) } else { $RestartRuntimeSeconds }

    $smokeArgs = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $PSScriptRoot 'Invoke-StellarApkSmoke.ps1'),
        '-ApkPath', $ApkPath,
        '-RuntimeSeconds', $effectiveRuntimeSeconds,
        '-RestartRuntimeSeconds', $effectiveRestartSeconds,
        '-OutputDirectory', $runDirectory
    )
    if ($HotUpdate) {
        $smokeArgs += @(
            '-RequireHotUpdatePass',
            '-HotUpdateHost', '127.0.0.1',
            '-HotUpdatePort', [string]$CdnPort,
            '-HotUpdatePackageName', [string]$preparation.packageName,
            '-HotUpdatePackageVersion', [string]$preparation.packageVersion,
            '-EmulatorMemoryMegabytes', [string]$emulatorMemoryMegabytes
        )
    }

    & powershell.exe @smokeArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "APK smoke verification failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $smokeResultPath))
    {
        throw "Smoke verification did not produce result.json: $smokeResultPath"
    }

    $smokeResult = Get-Content -LiteralPath $smokeResultPath -Raw | ConvertFrom-Json
    $pipelineResult.smokeResult = $smokeResult.status
    if ($smokeResult.status -ne 'PASS')
    {
        throw "Smoke verification result is '$($smokeResult.status)'."
    }

    if ($HotUpdate) {
        if ($null -eq $smokeResult.hotUpdateColdStart -or $null -eq $smokeResult.hotUpdateRestart) {
            throw 'Android smoke result did not contain both cold-start and restart HotUpdate PASS records.'
        }
        $pipelineResult.hotUpdateRuntime = [ordered]@{
            coldStart = $smokeResult.hotUpdateColdStart
            restart = $smokeResult.hotUpdateRestart
            resultFile = $smokeResultPath
        }
    }

    $pipelineResult.status = 'PASS'
    $pipelineResult.productVerificationStatus = 'PASS'
    Write-Host "PASS: Android Release Verification Pipeline profile '$($pipelineResult.profile)' completed."
    Write-Host "Artifacts: $runDirectory"
}
catch
{
    $exitCode = 2
    $pipelineResult.failure = $_.Exception.Message
    Write-Error "FAIL: $($_.Exception.Message)"
}
finally
{
    $cleanupFailures = @()

    if ($adbReverseActive -and -not [string]::IsNullOrWhiteSpace($verificationSerial)) {
        try {
            & $script:StellarAdb -s $verificationSerial reverse --remove "tcp:$CdnPort" | Out-Host
            if ($LASTEXITCODE -ne 0) {
                throw "adb reverse cleanup returned exit code $LASTEXITCODE."
            }
            $adbReverseActive = $false
        }
        catch {
            $cleanupFailures += $_.Exception.Message
        }
    }

    if ($null -ne $verificationCdn -and $null -ne $verificationCdn.Process) {
        try {
            $verificationCdn.Process.Refresh()
            if (-not $verificationCdn.Process.HasExited) {
                Stop-Process -Id $verificationCdn.Process.Id -Force
                $verificationCdn.Process.WaitForExit(5000) | Out-Null
            }
            $verificationCdn.Process.Refresh()
            if (-not $verificationCdn.Process.HasExited) {
                throw "Verification CDN process $($verificationCdn.Process.Id) is still running."
            }
        }
        catch {
            $cleanupFailures += $_.Exception.Message
        }
    }

    if (-not $KeepEmulator -and $startedEmulator)
    {
        try
        {
            & "$PSScriptRoot\Stop-StellarAndroid.ps1" | Out-Host
        }
        catch
        {
            Write-Warning "Failed to stop emulator cleanly: $($_.Exception.Message)"
        }
    }

    $pipelineResult.cleanupFailures = @($cleanupFailures)
    $pipelineResult.cleanupStatus = if ($cleanupFailures.Count -eq 0) { 'PASS' } else { 'FAIL' }
    if ($cleanupFailures.Count -gt 0 -and $pipelineResult.status -eq 'PASS') {
        $pipelineResult.productVerificationStatus = 'PASS'
        $pipelineResult.status = 'FAIL'
        $pipelineResult.failure = 'Android product gate passed, but verification resource cleanup failed: ' +
            ($cleanupFailures -join ' | ')
        $exitCode = 2
    }

    $pipelineResult.completedAt = [DateTimeOffset]::Now.ToString('o')
    $pipelineResult | ConvertTo-Json -Depth 6 | Out-File -FilePath $pipelineResultPath -Encoding utf8

    if ($pipelineResult.status -ne 'PASS')
    {
        Write-Host "FAIL artifacts: $runDirectory"
    }
}

exit $exitCode
