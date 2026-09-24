$ErrorActionPreference = 'Stop'

$script:StellarAndroidSdkRoot = if (-not [string]::IsNullOrWhiteSpace($env:STELLAR_ANDROID_SDK_ROOT)) {
    $env:STELLAR_ANDROID_SDK_ROOT
} elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_SDK_ROOT)) {
    $env:ANDROID_SDK_ROOT
} else {
    'C:\Android\Sdk'
}

$script:StellarAvdName = 'StellarFramework_API35'
$script:StellarAdb = Join-Path $script:StellarAndroidSdkRoot 'platform-tools\adb.exe'
$script:StellarEmulator = Join-Path $script:StellarAndroidSdkRoot 'emulator\emulator.exe'
$script:StellarAapt = Get-ChildItem (Join-Path $script:StellarAndroidSdkRoot 'build-tools\*\aapt.exe') -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Name } -Descending |
    Select-Object -First 1 -ExpandProperty FullName

function Assert-StellarAndroidEnvironment {
    if (-not (Test-Path $script:StellarAdb)) {
        throw "ADB not found: $script:StellarAdb"
    }

    if (-not (Test-Path $script:StellarEmulator)) {
        throw "Android Emulator not found: $script:StellarEmulator"
    }

    if ([string]::IsNullOrWhiteSpace($script:StellarAapt) -or -not (Test-Path $script:StellarAapt)) {
        throw "Android aapt not found below: $(Join-Path $script:StellarAndroidSdkRoot 'build-tools')"
    }

    $installedAvds = @(& $script:StellarEmulator -list-avds)
    if ($installedAvds -notcontains $script:StellarAvdName) {
        throw "Required AVD '$script:StellarAvdName' is not installed."
    }
}

function Get-StellarEmulatorSerial {
    Assert-StellarAndroidEnvironment
    $lines = & $script:StellarAdb devices
    foreach ($line in $lines) {
        if ($line -notmatch '^(emulator-\d+)\s+device$') {
            continue
        }

        $serial = $Matches[1]
        $avdOutput = @(& $script:StellarAdb -s $serial emu avd name 2>$null)
        if ($avdOutput.Count -eq 0) {
            continue
        }

        $name = ($avdOutput[0] -replace "`r", '').Trim()
        if ($name -eq $script:StellarAvdName) {
            return $serial
        }
    }

    return $null
}

function Get-StellarAndroidMemoryKilobytes {
    param([Parameter(Mandatory = $true)] [string] $Serial)

    $memoryLine = @(& $script:StellarAdb -s $Serial shell cat /proc/meminfo) |
        Where-Object { $_ -match '^MemTotal:\s+(\d+)\s+kB$' } |
        Select-Object -First 1
    if ($memoryLine -notmatch '^MemTotal:\s+(\d+)\s+kB$') {
        throw "Unable to read Android MemTotal from emulator '$Serial'."
    }

    return [long]$Matches[1]
}

function Get-FreeStellarEmulatorPort {
    Assert-StellarAndroidEnvironment
    $used = @{}
    foreach ($line in (& $script:StellarAdb devices)) {
        if ($line -match '^emulator-(\d+)\s+') {
            $used[[int]$Matches[1]] = $true
        }
    }

    for ($port = 5554; $port -le 5584; $port += 2) {
        if (-not $used.ContainsKey($port)) {
            return $port
        }
    }

    throw 'No free Android Emulator console port in range 5554..5584.'
}

function Wait-StellarAndroidBoot {
    param(
        [Parameter(Mandatory = $true)] [string] $Serial,
        [int] $TimeoutSeconds = 180
    )

    & $script:StellarAdb -s $Serial wait-for-device | Out-Null
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $boot = ((& $script:StellarAdb -s $Serial shell getprop sys.boot_completed 2>$null) -join '').Trim()
        if ($boot -eq '1') {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "Timed out waiting for $Serial to finish Android boot after $TimeoutSeconds seconds."
}

function Configure-StellarAndroidForAutomation {
    param([Parameter(Mandatory = $true)] [string] $Serial)

    & $script:StellarAdb -s $Serial shell settings put global window_animation_scale 0 | Out-Null
    & $script:StellarAdb -s $Serial shell settings put global transition_animation_scale 0 | Out-Null
    & $script:StellarAdb -s $Serial shell settings put global animator_duration_scale 0 | Out-Null
    & $script:StellarAdb -s $Serial shell settings put global stay_on_while_plugged_in 3 | Out-Null
    & $script:StellarAdb -s $Serial shell input keyevent 82 | Out-Null
}

function Get-StellarApkMetadata {
    param([Parameter(Mandatory = $true)] [string] $ApkPath)

    Assert-StellarAndroidEnvironment
    if (-not (Test-Path $ApkPath)) {
        throw "APK not found: $ApkPath"
    }

    $badging = @(& $script:StellarAapt dump badging $ApkPath)
    $packageLine = $badging | Where-Object { $_ -match '^package:' } | Select-Object -First 1
    $activityLine = $badging | Where-Object { $_ -match '^launchable-activity:' } | Select-Object -First 1
    $nativeLine = $badging | Where-Object { $_ -match '^native-code:' } | Select-Object -First 1

    if ($packageLine -notmatch "name='([^']+)'" ) {
        throw "Unable to read package name from APK: $ApkPath"
    }
    $packageName = $Matches[1]

    if ($activityLine -notmatch "name='([^']+)'" ) {
        throw "Unable to read launchable activity from APK: $ApkPath"
    }
    $activityName = $Matches[1]

    $nativeCodes = @()
    if ($nativeLine) {
        $nativeCodes = [regex]::Matches($nativeLine, "'([^']+)'" ) | ForEach-Object { $_.Groups[1].Value }
    }

    [pscustomobject]@{
        PackageName = $packageName
        ActivityName = $activityName
        NativeCodes = @($nativeCodes)
    }
}
