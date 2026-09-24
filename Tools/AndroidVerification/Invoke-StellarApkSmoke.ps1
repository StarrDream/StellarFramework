param(
    [Parameter(Mandatory = $true)] [string] $ApkPath,
    [string] $PackageName = '',
    [string] $LaunchActivity = '',
    [int] $RuntimeSeconds = 15,
    [int] $RestartRuntimeSeconds = 5,
    [string] $OutputDirectory = '',
    [switch] $SkipClearAppData,
    [switch] $RequireHotUpdatePass,
    [string] $HotUpdateHost = '127.0.0.1',
    [int] $HotUpdatePort = 18743,
    [string] $HotUpdatePackageName = 'StellarHotUpdateVerification',
    [string] $HotUpdatePackageVersion = 'verification-v1',
    [ValidateRange(1024, 8192)] [int] $EmulatorMemoryMegabytes = 2048
)

. "$PSScriptRoot\Common.ps1"

$ErrorActionPreference = 'Stop'
$startedAt = Get-Date
$apk = (Resolve-Path $ApkPath).Path
$metadata = Get-StellarApkMetadata -ApkPath $apk

if ([string]::IsNullOrWhiteSpace($PackageName)) {
    $PackageName = $metadata.PackageName
}
if ([string]::IsNullOrWhiteSpace($LaunchActivity)) {
    $LaunchActivity = $metadata.ActivityName
}
if ($metadata.NativeCodes.Count -gt 0 -and $metadata.NativeCodes -notcontains 'x86_64') {
    throw "APK does not contain x86_64 native code. Found: $($metadata.NativeCodes -join ', ')"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot ("Results\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

$logPath = Join-Path $OutputDirectory 'logcat.txt'
$appLogPath = Join-Path $OutputDirectory 'app-logcat.txt'
$restartLogPath = Join-Path $OutputDirectory 'restart-logcat.txt'
$restartAppLogPath = Join-Path $OutputDirectory 'restart-app-logcat.txt'
$screenPath = Join-Path $OutputDirectory 'screen.png'
$packagePath = Join-Path $OutputDirectory 'package.txt'
$activityPath = Join-Path $OutputDirectory 'activity.txt'
$resultPath = Join-Path $OutputDirectory 'result.json'
$result = [ordered]@{
    status = 'FAIL'
    startedAt = $startedAt.ToString('o')
    apk = $apk
    packageName = $PackageName
    launchActivity = $LaunchActivity
    serial = $null
    firstPid = $null
    restartPid = $null
    coldStartSystemPromptsHandled = 0
    restartSystemPromptsHandled = 0
    hotUpdateColdStart = $null
    hotUpdateRestart = $null
    runtimeSeconds = $RuntimeSeconds
    restartRuntimeSeconds = $RestartRuntimeSeconds
    failures = @()
}

function Start-StellarApkActivity {
    param(
        [Parameter(Mandatory = $true)] [string] $Serial,
        [Parameter(Mandatory = $true)] [string] $Component,
        [Parameter(Mandatory = $true)] [bool] $ExpectCache
    )

    $arguments = @('-s', $Serial, 'shell', 'am', 'start', '-W', '-n', $Component)
    if ($RequireHotUpdatePass) {
        $arguments += @(
            '--ez', 'stellar.hotupdate.verify', 'true',
            '--es', 'stellar.hotupdate.host', $HotUpdateHost,
            '--ei', 'stellar.hotupdate.port', [string]$HotUpdatePort,
            '--es', 'stellar.hotupdate.package', $HotUpdatePackageName,
            '--es', 'stellar.hotupdate.version', $HotUpdatePackageVersion,
            '--ez', 'stellar.hotupdate.expectCache', ([string]$ExpectCache).ToLowerInvariant()
        )
    }

    $output = @(& $script:StellarAdb @arguments)
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

function Read-StellarHotUpdatePassRecord {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [bool] $ExpectCache
    )

    $pattern = '\[StellarHotUpdateVerificationChunk\]\s+(\d+)/(\d+)\s+([A-Za-z0-9+/=]+)'
    $matches = @(Select-String -Path $Path -Pattern $pattern -AllMatches)
    if ($matches.Count -eq 0) {
        throw "Expected structured HotUpdate result chunks in '$Path'; found none."
    }

    $parts = @{}
    $expectedPartCount = 0
    foreach ($match in $matches) {
        foreach ($capture in $match.Matches) {
            $partIndex = [int]$capture.Groups[1].Value
            $partCount = [int]$capture.Groups[2].Value
            if ($partCount -le 0 -or $partIndex -lt 1 -or $partIndex -gt $partCount) {
                throw "HotUpdate result '$Path' has an invalid chunk index $partIndex/$partCount."
            }
            if ($expectedPartCount -ne 0 -and $expectedPartCount -ne $partCount) {
                throw "HotUpdate result '$Path' contains inconsistent chunk totals."
            }
            if ($parts.ContainsKey($partIndex)) {
                throw "HotUpdate result '$Path' contains duplicate chunk $partIndex/$partCount."
            }

            $expectedPartCount = $partCount
            $parts[$partIndex] = $capture.Groups[3].Value
        }
    }

    if ($parts.Count -ne $expectedPartCount) {
        throw "HotUpdate result '$Path' is incomplete; expected $expectedPartCount chunks and found $($parts.Count)."
    }

    $orderedParts = @(
        for ($index = 1; $index -le $expectedPartCount; $index++) {
            if (-not $parts.ContainsKey($index)) {
                throw "HotUpdate result '$Path' is missing chunk $index/$expectedPartCount."
            }
            $parts[$index]
        }
    )
    $encodedJson = [string]::Join('', $orderedParts)
    try {
        $jsonBytes = [Convert]::FromBase64String($encodedJson)
        $json = [Text.Encoding]::UTF8.GetString($jsonBytes)
        $payload = ConvertFrom-Json -InputObject $json -ErrorAction Stop
    }
    catch {
        throw "HotUpdate result '$Path' contains invalid Base64 or JSON: $($_.Exception.Message)"
    }

    $requiredTrue = @(
        'success', 'contentUpdateSucceeded', 'resKitManifestLoaded', 'resKitAssemblyLoaded',
        'assemblySha256Verified', 'aotMetadataLoadSucceeded', 'assemblyLoadSucceeded',
        'entryPointInvoked', 'entryMarkerObserved'
    )
    foreach ($field in $requiredTrue) {
        if (-not [bool]$payload.$field) {
            throw "HotUpdate result '$Path' did not prove '$field'."
        }
    }

    if ($payload.status -ne 'PASS' -or $payload.platform -ne 'Android' -or
        $payload.manifestBuildTarget -ne 'Android' -or
        $payload.packageVersion -ne $HotUpdatePackageVersion -or
        $payload.manifestSource -notlike 'ResKit:YooAsset:*' -or
        [string]::IsNullOrWhiteSpace($payload.loadedAssemblyFullName) -or
        $payload.loadedAssemblyFullName -notmatch 'HotUpdate') {
        throw "HotUpdate result '$Path' has a status, target, package, manifest source, or loaded assembly mismatch."
    }

    if ([string]$payload.expectedAssemblySha256 -ne [string]$payload.actualAssemblySha256) {
        throw "HotUpdate result '$Path' has a SHA256 mismatch."
    }

    if (@($payload.aotMetadataKeys).Count -lt 1 -or
        @($payload.aotMetadataKeysLoaded).Count -ne @($payload.aotMetadataKeys).Count) {
        throw "HotUpdate result '$Path' did not load every declared AOT metadata key."
    }

    if ($ExpectCache) {
        if ([int]$payload.cacheFileCountBeforeUpdate -le 0 -or
            [int]$payload.downloadedFileCount -ne 0) {
            throw "Restart result '$Path' did not demonstrate a warm YooAsset cache with zero redownloads."
        }
    }
    elseif ([int]$payload.cacheFileCountBeforeUpdate -ne 0 -or
            [int]$payload.cacheFileCountAfterUpdate -le 0 -or
            [int]$payload.downloadedFileCount -le 0) {
        throw "Cold-start result '$Path' did not demonstrate a remote download into an empty cache."
    }

    return $payload
}

function Get-StellarAndroidStartupPromptButton {
    param([Parameter(Mandatory = $true)] [string] $Serial)

    $remoteHierarchyPath = '/sdcard/stellarframework-smoke-window.xml'
    $dumpOutput = @(& $script:StellarAdb -s $Serial shell uiautomator dump $remoteHierarchyPath 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect Android startup dialogs on '$Serial': $($dumpOutput -join ' ')"
    }

    $hierarchyText = (@(& $script:StellarAdb -s $Serial exec-out cat $remoteHierarchyPath) -join '')
    if ([string]::IsNullOrWhiteSpace($hierarchyText)) {
        throw "Android UI hierarchy was empty on '$Serial'."
    }

    try {
        [xml]$hierarchy = $hierarchyText
    }
    catch {
        throw "Android UI hierarchy was invalid on '$Serial': $($_.Exception.Message)"
    }

    $hasNotRespondingDialog = $false
    foreach ($title in $hierarchy.SelectNodes("//node[@resource-id='android:id/alertTitle']")) {
        if ($title.GetAttribute('text') -match "isn't responding|not responding") {
            $hasNotRespondingDialog = $true
            break
        }
    }

    if ($hasNotRespondingDialog) {
        $waitButton = $hierarchy.SelectSingleNode("//node[@resource-id='android:id/aerr_wait' and @clickable='true']")
        if ($null -eq $waitButton) {
            throw "Android reported an unresponsive system app on '$Serial' without an accessible Wait button."
        }
        $action = 'Wait'
    }
    else {
        $waitButton = $null
        foreach ($button in $hierarchy.SelectNodes("//node[@class='android.widget.Button' and @clickable='true']")) {
            if ($button.GetAttribute('text') -eq 'Got it' -and
                $button.GetAttribute('package') -in @('android', 'com.android.systemui')) {
                $waitButton = $button
                break
            }
        }
        if ($null -eq $waitButton) {
            return $null
        }
        $action = 'Got it'
    }

    $bounds = $waitButton.GetAttribute('bounds')
    if ($bounds -notmatch '^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$') {
        throw "Android Wait button has invalid bounds on '$Serial': $bounds"
    }

    [pscustomobject]@{
        Action = $action
        X = [int](([int]$Matches[1] + [int]$Matches[3]) / 2)
        Y = [int](([int]$Matches[2] + [int]$Matches[4]) / 2)
    }
}

function Wait-StellarHotUpdateBootstrap {
    param(
        [Parameter(Mandatory = $true)] [string] $Serial,
        [Parameter(Mandatory = $true)] [string] $Package,
        [ValidateRange(30, 300)] [int] $TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $handledPromptCount = 0
    $lastPromptKey = $null
    while ((Get-Date) -lt $deadline) {
        $promptButton = Get-StellarAndroidStartupPromptButton -Serial $Serial
        if ($null -eq $promptButton) {
            $lastPromptKey = $null
        }
        else {
            $promptKey = "$($promptButton.Action)@$($promptButton.X),$($promptButton.Y)"
            if ($promptKey -ne $lastPromptKey) {
                & $script:StellarAdb -s $Serial shell input tap $promptButton.X $promptButton.Y | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to select '$($promptButton.Action)' on an Android startup dialog on '$Serial'."
                }
                $handledPromptCount++
                $lastPromptKey = $promptKey
                Write-Host "Handled Android startup prompt '$($promptButton.Action)' on $Serial."
            }
        }

        $appProcessId = ((& $script:StellarAdb -s $Serial shell pidof $Package 2>$null) -join '').Trim()
        if (-not [string]::IsNullOrWhiteSpace($appProcessId)) {
            $appLog = @(& $script:StellarAdb -s $Serial logcat "--pid=$appProcessId" -d -v threadtime 2>$null)
            if (($appLog -join "`n") -match '\[StellarHotUpdateVerificationStage\] BootstrapEntered') {
                return $handledPromptCount
            }
        }

        Start-Sleep -Seconds 2
    }

    throw "HotUpdate Player did not emit BootstrapEntered within $TimeoutSeconds seconds on '$Serial'."
}

try {
    $serial = Get-StellarEmulatorSerial
    if ([string]::IsNullOrWhiteSpace($serial)) {
        $serial = (& "$PSScriptRoot\Start-StellarAndroid.ps1" `
            -MemoryMegabytes $EmulatorMemoryMegabytes | Select-Object -Last 1).Trim()
    }
    $result.serial = $serial

    Wait-StellarAndroidBoot -Serial $serial
    Configure-StellarAndroidForAutomation -Serial $serial

    & $script:StellarAdb -s $serial install -r $apk | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "APK installation failed on $serial."
    }

    if (-not $SkipClearAppData) {
        $clearResult = ((& $script:StellarAdb -s $serial shell pm clear $PackageName) -join '').Trim()
        if ($clearResult -ne 'Success') {
            throw "Failed to clear app data for '$PackageName': $clearResult"
        }
    }

    & $script:StellarAdb -s $serial logcat -c
    & $script:StellarAdb -s $serial shell am force-stop $PackageName | Out-Null

    if ($RequireHotUpdatePass -and
        ($HotUpdateHost -ne '127.0.0.1' -or $HotUpdatePort -lt 1 -or $HotUpdatePort -gt 65535)) {
        throw 'HotUpdate verification requires a valid loopback CDN host and port.'
    }

    $component = "$PackageName/$LaunchActivity"
    $start = Start-StellarApkActivity -Serial $serial -Component $component -ExpectCache $false
    $startOutput = @($start.Output)
    $startOutput | Out-Host
    if ($start.ExitCode -ne 0 -or ($startOutput -join "`n") -notmatch 'Status:\s+ok') {
        throw "Failed to cold-start '$component'."
    }

    if ($RequireHotUpdatePass) {
        $result.coldStartSystemPromptsHandled = Wait-StellarHotUpdateBootstrap `
            -Serial $serial `
            -Package $PackageName
    }

    Start-Sleep -Seconds ([Math]::Max(1, $RuntimeSeconds))

    $firstPid = ((& $script:StellarAdb -s $serial shell pidof $PackageName 2>$null) -join '').Trim()
    $result.firstPid = $firstPid
    if ([string]::IsNullOrWhiteSpace($firstPid)) {
        throw "App process '$PackageName' is not alive after the smoke window."
    }

    & $script:StellarAdb -s $serial logcat -d -v threadtime | Out-File -FilePath $logPath -Encoding utf8
    & $script:StellarAdb -s $serial logcat --pid=$firstPid -d -v threadtime | Out-File -FilePath $appLogPath -Encoding utf8
    & $script:StellarAdb -s $serial shell dumpsys package $PackageName | Out-File -FilePath $packagePath -Encoding utf8
    & $script:StellarAdb -s $serial shell dumpsys activity activities | Out-File -FilePath $activityPath -Encoding utf8

    $escapedPackage = [regex]::Escape($PackageName)
    $activityText = Get-Content $activityPath -Raw
    if ($activityText -notmatch $escapedPackage) {
        throw "Package '$PackageName' is not present in the activity state dump."
    }

    $remoteScreen = "/sdcard/stellarframework-smoke-$([Guid]::NewGuid().ToString('N')).png"
    & $script:StellarAdb -s $serial shell screencap -p $remoteScreen | Out-Null
    & $script:StellarAdb -s $serial pull $remoteScreen $screenPath | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $screenPath)) {
        throw "Failed to capture screenshot from $serial."
    }
    & $script:StellarAdb -s $serial shell rm $remoteScreen | Out-Null

    $appPatterns = @(
        'FATAL EXCEPTION',
        'AndroidRuntime.*FATAL',
        'UnityException',
        'NullReferenceException',
        'MissingReferenceException',
        '\sE\s+Unity\s*:'
    )
    $systemPatterns = @(
        "ANR in\s+$escapedPackage",
        "Process:\s+$escapedPackage",
        "Force finishing activity.*$escapedPackage"
    )

    $failures = @()
    foreach ($pattern in $appPatterns) {
        $failures += @(Select-String -Path $appLogPath -Pattern $pattern -ErrorAction SilentlyContinue)
    }
    foreach ($pattern in $systemPatterns) {
        $failures += @(Select-String -Path $logPath -Pattern $pattern -ErrorAction SilentlyContinue)
    }
    if ($failures.Count -gt 0) {
        $result.failures = @($failures | Select-Object -First 40 | ForEach-Object { $_.Line })
        throw "Detected $($failures.Count) configured Unity/FATAL/ANR failure log entries."
    }

    if ($RequireHotUpdatePass) {
        $result.hotUpdateColdStart = Read-StellarHotUpdatePassRecord `
            -Path $appLogPath `
            -ExpectCache $false
    }

    & $script:StellarAdb -s $serial shell am force-stop $PackageName | Out-Null
    & $script:StellarAdb -s $serial logcat -c
    Start-Sleep -Milliseconds 500
    $restart = Start-StellarApkActivity -Serial $serial -Component $component -ExpectCache $true
    $restartOutput = @($restart.Output)
    $restartOutput | Out-Host
    if ($restart.ExitCode -ne 0 -or ($restartOutput -join "`n") -notmatch 'Status:\s+ok') {
        throw "Failed to restart '$component'."
    }
    if ($RequireHotUpdatePass) {
        $result.restartSystemPromptsHandled = Wait-StellarHotUpdateBootstrap `
            -Serial $serial `
            -Package $PackageName
    }
    Start-Sleep -Seconds ([Math]::Max(1, $RestartRuntimeSeconds))
    $restartPid = ((& $script:StellarAdb -s $serial shell pidof $PackageName 2>$null) -join '').Trim()
    $result.restartPid = $restartPid
    if ([string]::IsNullOrWhiteSpace($restartPid)) {
        throw "App process '$PackageName' is not alive after restart."
    }

    & $script:StellarAdb -s $serial logcat -d -v threadtime | Out-File -FilePath $restartLogPath -Encoding utf8
    & $script:StellarAdb -s $serial logcat --pid=$restartPid -d -v threadtime | Out-File -FilePath $restartAppLogPath -Encoding utf8

    $restartFailures = @()
    foreach ($pattern in $appPatterns) {
        $restartFailures += @(Select-String -Path $restartAppLogPath -Pattern $pattern -ErrorAction SilentlyContinue)
    }
    foreach ($pattern in $systemPatterns) {
        $restartFailures += @(Select-String -Path $restartLogPath -Pattern $pattern -ErrorAction SilentlyContinue)
    }
    if ($restartFailures.Count -gt 0) {
        $result.failures = @($restartFailures | Select-Object -First 40 | ForEach-Object { $_.Line })
        throw "Detected $($restartFailures.Count) configured Unity/FATAL/ANR failure log entries after restart."
    }

    if ($RequireHotUpdatePass) {
        $result.hotUpdateRestart = Read-StellarHotUpdatePassRecord `
            -Path $restartAppLogPath `
            -ExpectCache $true
    }

    $result.status = 'PASS'
    $result.completedAt = (Get-Date).ToString('o')
    $result | ConvertTo-Json -Depth 5 | Out-File -FilePath $resultPath -Encoding utf8
    Write-Host "PASS: $PackageName cold-started, survived $RuntimeSeconds seconds, and restarted successfully."
    Write-Host "Artifacts: $OutputDirectory"
}
catch {
    $result.failures = @($result.failures) + @($_.Exception.Message)
    $result.completedAt = (Get-Date).ToString('o')
    $result | ConvertTo-Json -Depth 5 | Out-File -FilePath $resultPath -Encoding utf8
    Write-Error "FAIL: $($_.Exception.Message) Artifacts: $OutputDirectory"
    exit 2
}
