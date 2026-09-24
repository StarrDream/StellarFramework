param(
    [switch] $Windowed,
    [int] $TimeoutSeconds = 180,
    [string] $LogDirectory = '',
    [ValidateRange(1024, 8192)] [int] $MemoryMegabytes = 2048
)

. "$PSScriptRoot\Common.ps1"

Assert-StellarAndroidEnvironment

$serial = Get-StellarEmulatorSerial
if ([string]::IsNullOrWhiteSpace($serial)) {
    $port = Get-FreeStellarEmulatorPort
    if ([string]::IsNullOrWhiteSpace($LogDirectory)) {
        $LogDirectory = Join-Path $PSScriptRoot 'Results\Emulator'
    }
    New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $stdoutLog = Join-Path $LogDirectory ("emulator-$stamp.stdout.log")
    $stderrLog = Join-Path $LogDirectory ("emulator-$stamp.stderr.log")

    $arguments = @(
        '-avd', $script:StellarAvdName,
        '-port', $port,
        '-no-snapshot',
        '-no-boot-anim',
        '-gpu', 'swiftshader',
        '-no-audio',
        '-memory', [string]$MemoryMegabytes,
        '-cores', '4',
        '-netdelay', 'none',
        '-netspeed', 'full'
    )

    if (-not $Windowed) {
        $arguments += '-no-window'
    }

    $process = Start-Process -FilePath $script:StellarEmulator `
        -ArgumentList $arguments `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -PassThru
    $serial = "emulator-$port"
    Write-Host "Started $($script:StellarAvdName) as $serial (PID=$($process.Id), RAM=${MemoryMegabytes}MB)."
    Write-Host "Emulator logs: $stdoutLog ; $stderrLog"
} else {
    Write-Host "$($script:StellarAvdName) is already running as $serial."
}

Wait-StellarAndroidBoot -Serial $serial -TimeoutSeconds $TimeoutSeconds
Configure-StellarAndroidForAutomation -Serial $serial

$release = ((& $script:StellarAdb -s $serial shell getprop ro.build.version.release) -join '').Trim()
$api = ((& $script:StellarAdb -s $serial shell getprop ro.build.version.sdk) -join '').Trim()
$model = ((& $script:StellarAdb -s $serial shell getprop ro.product.model) -join '').Trim()

Write-Host "READY serial=$serial Android=$release API=$api Model=$model"
$serial
