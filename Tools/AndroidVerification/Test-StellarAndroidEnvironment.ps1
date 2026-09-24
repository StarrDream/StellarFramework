. "$PSScriptRoot\Common.ps1"

Assert-StellarAndroidEnvironment

Write-Host "SDK Root: $script:StellarAndroidSdkRoot"
Write-Host "AVD:      $script:StellarAvdName"

Write-Host '--- Emulator acceleration ---'
& $script:StellarEmulator -accel-check
if ($LASTEXITCODE -ne 0) {
    throw 'Android Emulator hardware acceleration check failed.'
}

$serial = Get-StellarEmulatorSerial
if ([string]::IsNullOrWhiteSpace($serial)) {
    Write-Host 'AVD is installed but not currently running.'
    exit 0
}

Write-Host "--- Running device: $serial ---"
& $script:StellarAdb -s $serial devices -l
Write-Host ("boot_completed=" + (((& $script:StellarAdb -s $serial shell getprop sys.boot_completed) -join '').Trim()))
Write-Host ("android=" + (((& $script:StellarAdb -s $serial shell getprop ro.build.version.release) -join '').Trim()))
Write-Host ("api=" + (((& $script:StellarAdb -s $serial shell getprop ro.build.version.sdk) -join '').Trim()))
& $script:StellarAdb -s $serial shell wm size
& $script:StellarAdb -s $serial shell wm density
