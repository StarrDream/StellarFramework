. "$PSScriptRoot\Common.ps1"

$serial = Get-StellarEmulatorSerial
if ([string]::IsNullOrWhiteSpace($serial)) {
    Write-Host "$($script:StellarAvdName) is not running."
    exit 0
}

& $script:StellarAdb -s $serial emu kill | Out-Null
Write-Host "Stopped $($script:StellarAvdName) ($serial)."
