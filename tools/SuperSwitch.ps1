# SuperSwitch one-click launcher: builds SuperSwitch when the source changed, then starts it.
param([switch]$ForceBuild, [switch]$BuildOnly)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$buildDir = Join-Path $root 'build'
$exe = Join-Path $buildDir 'SuperSwitch.exe'
$logDir = Join-Path $root 'build-logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
# A new log per run, so a still-running earlier build window can't lock it.
$log = Join-Path $logDir ("build-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt")
# Written after every successful build. The "did the source change?" check compares against this file, not against
# SuperSwitch.exe: the exe is only rewritten when the main project itself recompiles, so after an edit to one of the
# libraries it kept its old date and every start ran the build again.
$stamp = Join-Path $logDir 'last-successful-build.stamp'

function Write-Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

function Test-NeedsBuild {
    if ($ForceBuild -or -not (Test-Path $exe) -or -not (Test-Path $stamp)) { return $true }
    $built = (Get-Item $stamp).LastWriteTimeUtc
    $newer = Get-ChildItem -Path (Join-Path $root 'src'), (Join-Path $root 'Directory.Packages.props'), (Join-Path $root 'Directory.Build.props') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTimeUtc -gt $built -and $_.FullName -notmatch '\\(bin|obj)\\' } |
        Select-Object -First 1
    return $null -ne $newer
}

function Use-LocalLibHac {
    # Ryubing's LibHac package feed (git.ryujinx.app) is offline; build the package from the mirrored source instead.
    $props = Get-Content (Join-Path $root 'Directory.Packages.props') -Raw
    if ($props -notmatch 'Include="Ryujinx.LibHac" Version="([^"]+)"') { throw 'Could not find the Ryujinx.LibHac version in Directory.Packages.props.' }
    $version = $Matches[1]

    $feed = Join-Path $root 'localfeed'
    $package = Join-Path $feed ("ryujinx.libhac.$version.nupkg")

    if (-not (Test-Path $package)) {
        Write-Step "Building LibHac $version locally (one time only)"
        # Outside the SuperSwitch folder: Ryubing's Directory.Packages.props / Directory.Build.props must not apply to LibHac.
        $src = Join-Path $env:LOCALAPPDATA 'SuperSwitch\LibHac-src'
        $old = Join-Path $root '.libhac'
        if ((Test-Path $old) -and -not (Test-Path $src)) {
            New-Item -ItemType Directory -Force -Path (Split-Path $src) | Out-Null
            Move-Item $old $src
        }
        if (-not (Test-Path (Join-Path $src '.git'))) {
            git clone https://starforgejo.dev/archive/LibHac_Ryubing.git $src 2>&1 | Tee-Object -FilePath $log -Append | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'Could not download the LibHac source.' }
        }
        # Use the exact release the emulator expects, if the mirror has a tag for it.
        $tag = git -C $src tag -l "*$version*" | Select-Object -First 1
        if ($tag) { git -C $src checkout --quiet $tag 2>&1 | Out-Host }
        $proj = Get-ChildItem -Path $src -Recurse -Filter 'LibHac.csproj' | Select-Object -First 1
        if ($null -eq $proj) { throw 'LibHac.csproj not found in the LibHac source.' }
        New-Item -ItemType Directory -Force -Path $feed | Out-Null
        dotnet pack $proj.FullName -c Release -o $feed "-p:PackageId=Ryujinx.LibHac" "-p:Version=$version" "-p:PackageVersion=$version" "-p:ManagePackageVersionsCentrally=false" 2>&1 | Tee-Object -FilePath $log -Append | Out-Host
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $package)) { throw "Building the LibHac package failed. See $log" }
    }

    # Point the LibHac package source at the local folder.
    $config = Join-Path $root 'nuget.config'
    $text = Get-Content $config -Raw
    $fixed = $text -replace 'key="LibHacAlpha" value="[^"]*"', 'key="LibHacAlpha" value="localfeed"'
    if ($fixed -ne $text) { Set-Content -Path $config -Value $fixed -Encoding UTF8 }
}

function Invoke-Build {
    Write-Step 'Building SuperSwitch (first build takes a few minutes)'
    "=== Build $(Get-Date)" | Out-File $log -Append
    dotnet build (Join-Path $root 'src\SuperSwitch\SuperSwitch.csproj') -c Release -o $buildDir 2>&1 | Tee-Object -FilePath $log -Append | Out-Host
    return $LASTEXITCODE
}

function Backup-Source {
    # Keep a copy of the SuperSwitch source on OneDrive, so a reformat can't lose the work again.
    # Runs hidden in the background (only changed files are copied), so it never delays starting SuperSwitch.
    $oneDrive = $env:OneDrive
    if (-not $oneDrive -or -not (Test-Path $oneDrive)) { return }
    # One folder per PC, so an out-of-date PC can't overwrite the other's newer backup.
    $dest = Join-Path (Join-Path $oneDrive 'SuperSwitch Source Backup') $env:COMPUTERNAME
    $rcArgs = @("`"$root`"", "`"$dest`"", '/MIR', '/R:1', '/W:1', '/NP', '/XJ',
        '/XD', "`"$(Join-Path $root 'build')`"", "`"$(Join-Path $root 'build-logs')`"", "`"$(Join-Path $root 'build-converter')`"",
        "`"$(Join-Path $root '.libhac')`"", 'bin', 'obj', '.vs',
        "/LOG:`"$(Join-Path $logDir 'source-backup.txt')`"")
    try { Start-Process -FilePath 'robocopy.exe' -ArgumentList $rcArgs -WindowStyle Hidden } catch { }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET SDK is not installed. Run: winget install Microsoft.DotNet.SDK.10' -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

if (Test-NeedsBuild) {
    "SuperSwitch build log" | Out-File $log

    $feedUp = $false
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $feedUp = $client.ConnectAsync('git.ryujinx.app', 443).Wait(3000) -and $client.Connected
        $client.Close()
    } catch { }

    if (-not $feedUp -and -not ((Get-Content (Join-Path $root 'nuget.config') -Raw) -match 'value="localfeed"')) {
        Write-Step "Ryubing's package server is offline; using a locally built LibHac"
        try {
            Use-LocalLibHac
        } catch {
            Write-Host $_ -ForegroundColor Red
            "LibHac fallback failed: $_" | Out-File $log -Append
        }
    }

    $code = Invoke-Build

    if ($code -ne 0 -and (Select-String -Path $log -Pattern 'Ryujinx.LibHac|NU1301|NU1101|NU1102|git.ryujinx.app' -Quiet)) {
        try {
            Use-LocalLibHac
            $code = Invoke-Build
        } catch {
            Write-Host $_ -ForegroundColor Red
            "LibHac fallback failed: $_" | Out-File $log -Append
        }
    }

    if ($code -ne 0) {
        Write-Host "`nSuperSwitch failed to build. The full output is in $log" -ForegroundColor Red
        Write-Host 'Copy the error lines above (or the log file) and send them to Claude.' -ForegroundColor Yellow
        Read-Host 'Press Enter to close'
        exit 1
    }

    Set-Content -Path $stamp -Value (Get-Date -Format 'o')
    Write-Host "`nBuild succeeded." -ForegroundColor Green
}

Backup-Source

if ($BuildOnly) { exit 0 }

Start-Process -FilePath $exe -WorkingDirectory $buildDir
