[CmdletBinding()]
param(
    [string] $UnitySkillsUrl = 'http://localhost:8090',
    [ValidateRange(1, 30)]
    [int] $TimeoutMinutes = 20,
    [string] $EvidencePath = 'Temp/StellarHotUpdateVerification/playmode-gate-result.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([System.IO.Path]::IsPathRooted($EvidencePath))
{
    $resolvedEvidencePath = [System.IO.Path]::GetFullPath($EvidencePath)
}
else
{
    $resolvedEvidencePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $EvidencePath))
}

$prepareMenuPath = 'Tools/StellarFramework/Verification/Prepare HotUpdate PlayMode Release Gate'
$gateFullName = 'StellarFramework.Tests.ReleaseGate.YooAssetHotUpdateEndToEndTests.PreparedPackageResumesRangeAndEntersHotUpdate'
$runtimeRoot = Join-Path $projectRoot 'Temp/StellarHotUpdateVerification'
$configPath = Join-Path $runtimeRoot 'runtime-config.json'
$evidence = [ordered]@{
    status = 'RUNNING'
    startedAt = [DateTimeOffset]::Now.ToString('o')
    unitySkillsUrl = $UnitySkillsUrl
    unity = $null
    prepare = $null
    package = $null
    discovery = $null
    test = $null
    error = $null
}

function Invoke-StellarUnitySkill
{
    param(
        [Parameter(Mandatory = $true)] [string] $SkillName,
        [Parameter(Mandatory = $true)] [hashtable] $Body,
        [int] $RequestTimeoutSeconds = 30
    )

    $uri = $UnitySkillsUrl.TrimEnd('/') + '/skill/' + $SkillName
    $jsonBody = $Body | ConvertTo-Json -Depth 12 -Compress
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' `
        -Body $jsonBody -TimeoutSec $RequestTimeoutSeconds

    if ($response.status -ne 'success' -or $response.result.success -ne $true)
    {
        $details = $response | ConvertTo-Json -Depth 8 -Compress
        throw "UnitySkills skill '$SkillName' failed: $details"
    }

    return $response.result
}

function Wait-StellarUnitySkillsJob
{
    param(
        [Parameter(Mandatory = $true)] [string] $SkillName,
        [Parameter(Mandatory = $true)] [string] $JobId,
        [Parameter(Mandatory = $true)] [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline)
    {
        if ($SkillName -eq 'test_discover_get_result')
        {
            $job = Invoke-StellarUnitySkill -SkillName $SkillName -Body @{ jobId = $JobId; limit = 5000 }
        }
        else
        {
            $job = Invoke-StellarUnitySkill -SkillName $SkillName -Body @{ jobId = $JobId }
        }

        if ($job.status -eq 'completed')
        {
            return $job
        }

        if ($job.status -in @('failed', 'error', 'cancelled', 'canceled'))
        {
            $details = $job | ConvertTo-Json -Depth 12 -Compress
            throw "UnitySkills job '$JobId' ended with status '$($job.status)': $details"
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out after $TimeoutSeconds seconds waiting for UnitySkills job '$JobId'."
}

function Save-StellarGateEvidence
{
    param([Parameter(Mandatory = $true)] [System.Collections.IDictionary] $Value)

    $directory = [System.IO.Path]::GetDirectoryName($resolvedEvidencePath)
    if (-not [string]::IsNullOrWhiteSpace($directory))
    {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $Value.updatedAt = [DateTimeOffset]::Now.ToString('o')
    $json = $Value | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText(
        $resolvedEvidencePath,
        $json,
        [System.Text.UTF8Encoding]::new($false))
}

try
{
    $health = Invoke-RestMethod -Uri ($UnitySkillsUrl.TrimEnd('/') + '/health') -Method Get -TimeoutSec 5
    if ($health.status -ne 'ok' -or $health.projectName -ne 'StellarFramework')
    {
        throw "UnitySkills is not serving StellarFramework at '$UnitySkillsUrl'."
    }

    $projectInfo = Invoke-StellarUnitySkill -SkillName 'project_get_info' -Body @{}
    $unityProjectRoot = [System.IO.Directory]::GetParent(
        [System.IO.Path]::GetFullPath($projectInfo.projectPath)).FullName
    if (-not [string]::Equals(
        $unityProjectRoot.TrimEnd('\', '/'),
        $projectRoot.TrimEnd('\', '/'),
        [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "UnitySkills project path mismatch. Expected='$projectRoot', Actual='$unityProjectRoot'."
    }

    $evidence.unity = @{
        instanceId = $health.instanceId
        unityVersion = $health.unityVersion
        projectName = $health.projectName
        projectRoot = $unityProjectRoot
    }

    $prepareTimeoutSeconds = [Math]::Min(900, $TimeoutMinutes * 60)
    $prepare = Invoke-StellarUnitySkill -SkillName 'editor_execute_menu' `
        -Body @{ menuPath = $prepareMenuPath } -RequestTimeoutSeconds $prepareTimeoutSeconds
    $evidence.prepare = $prepare

    if (-not (Test-Path -LiteralPath $configPath))
    {
        throw "Prepare completed without writing the runtime config: $configPath"
    }

    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if ($config.packageName -ne 'StellarHotUpdateVerification' -or
        $config.expectedPackageVersion -ne 'verification-v1' -or
        -not (Test-Path -LiteralPath $config.packageDirectory -PathType Container))
    {
        throw "Prepared runtime config is invalid or its package directory is missing: $configPath"
    }

    $bundles = @(Get-ChildItem -LiteralPath $config.packageDirectory -File -Recurse |
        Where-Object { $_.Extension -eq '.bundle' } |
        Sort-Object Length -Descending)
    if ($bundles.Count -eq 0 -or $bundles[0].Length -le 1MB)
    {
        throw 'Prepared verification package has no bundle larger than 1 MiB for Range resume validation.'
    }

    $evidence.package = @{
        name = $config.packageName
        version = $config.expectedPackageVersion
        directory = $config.packageDirectory
        bundleCount = $bundles.Count
        largestBundle = $bundles[0].Name
        largestBundleBytes = $bundles[0].Length
    }

    $discoveryStart = Invoke-StellarUnitySkill -SkillName 'test_discover_start' `
        -Body @{ testMode = 'PlayMode' }
    $discovery = Wait-StellarUnitySkillsJob -SkillName 'test_discover_get_result' `
        -JobId $discoveryStart.jobId -TimeoutSeconds ([Math]::Min(300, $TimeoutMinutes * 60))

    if ($discovery.truncated)
    {
        throw "PlayMode discovery returned a truncated list ($($discovery.returned)/$($discovery.count)); increase the discovery limit."
    }

    $gate = @($discovery.tests | Where-Object { $_.fullName -eq $gateFullName })
    if ($gate.Count -ne 1 -or $gate[0].runState -ne 'Runnable')
    {
        throw "Fresh PlayMode discovery did not return the exact gate as Runnable: $gateFullName"
    }

    $evidence.discovery = @{
        jobId = $discovery.jobId
        total = $discovery.count
        returned = $discovery.returned
        truncated = $discovery.truncated
        gateFullName = $gate[0].fullName
        gateRunState = $gate[0].runState
        gateCategories = @($gate[0].categories)
    }

    $testStart = Invoke-StellarUnitySkill -SkillName 'test_run_by_name' `
        -Body @{ testName = $gateFullName; testMode = 'PlayMode' }
    $test = Wait-StellarUnitySkillsJob -SkillName 'test_get_result' `
        -JobId $testStart.jobId -TimeoutSeconds ($TimeoutMinutes * 60)
    $evidence.test = $test

    if ($test.totalTests -ne 1 -or $test.passedTests -ne 1 -or
        $test.failedTests -ne 0 -or $test.skippedTests -ne 0 -or
        $test.inconclusiveTests -ne 0)
    {
        throw "Exact PlayMode release gate did not pass: $($test.resultSummary)"
    }

    $evidence.status = 'PASS'
    $evidence.error = $null
    Save-StellarGateEvidence -Value $evidence
    Write-Host "HotUpdate PlayMode Release Gate PASS. Evidence: $resolvedEvidencePath"
}
catch
{
    $evidence.status = 'FAIL'
    $evidence.error = $_.Exception.Message
    Save-StellarGateEvidence -Value $evidence
    throw "HotUpdate PlayMode Release Gate failed. Evidence: $resolvedEvidencePath. $($evidence.error)"
}
