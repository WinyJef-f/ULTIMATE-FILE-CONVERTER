<#
.SYNOPSIS
    Builds the Windows deliverables for ULTIMATE-FILE-CONVERTER (WinUI 3).

.DESCRIPTION
    1. Publishes the WinUI app as a self-contained, unpackaged win-x64 app.
    2. Builds an MSI with the WiX toolset (installed automatically as a .NET tool).
    3. Builds an EXE installer with Inno Setup 6 when ISCC.exe is available.

    Outputs land in dist/windows/:
        publish/                                              the published app
        msi/ULTIMATE-FILE-CONVERTER-<version>-x64.msi         Windows Installer package
        setup/ULTIMATE-FILE-CONVERTER-Setup-<version>-x64.exe Inno Setup installer

.EXAMPLE
    pwsh ./tools/windows/build-installers.ps1 -Configuration Release
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.2",
    [string]$Runtime = "win-x64",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$RepoRoot     = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$Project      = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\UltimateFileConverter.WinUI.csproj"
$IconFile     = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\Assets\app.ico"
$InstallerSrc = Join-Path $PSScriptRoot "installer"
$DistRoot     = Join-Path $RepoRoot "dist\windows"
$PublishDir   = Join-Path $DistRoot "publish"
$MsiDir       = Join-Path $DistRoot "msi"
$SetupDir     = Join-Path $DistRoot "setup"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir, $MsiDir, $SetupDir | Out-Null

# --------------------------------------------------------------------------
# Build with MSBuild from Visual Studio, not `dotnet`: WinUI 3 needs the
# resources.pri / AppxPackage build tasks (Microsoft.Build.Packaging.Pri.Tasks.dll)
# that ship with VS's MSBuild and aren't present in the bare .NET SDK.
function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -prerelease -products * `
            -requires Microsoft.Component.MSBuild `
            -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    $cmd = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

$MSBuild = Find-MSBuild
if (-not $MSBuild) {
    throw "MSBuild was not found. Install Visual Studio 2022 with the '.NET Desktop' and 'Windows App SDK / WinUI' components."
}

Write-Host "==> Restoring ($MSBuild)..."
# WindowsAppSDKSelfContained MUST be on the command line: the WASDK targets that pull in
# Bootstrap.dll / XAML DLLs evaluate this property before the csproj body is fully read.
& $MSBuild $Project /t:Restore /p:Configuration=$Configuration /p:Platform=$Platform /p:RuntimeIdentifier=$Runtime `
    /p:WindowsAppSDKSelfContained=true
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }

Write-Host "==> Publishing WinUI app (self-contained, unpackaged, $Runtime)..."
# Forward slash on PublishDir avoids the trailing-backslash-inside-quotes arg-escaping bug.
& $MSBuild $Project /t:Publish `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=$Runtime `
    /p:SelfContained=true `
    /p:WindowsPackageType=None `
    /p:WindowsAppSDKSelfContained=true `
    /p:Version=$Version `
    "/p:PublishDir=$PublishDir/"
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$AppExe = Join-Path $PublishDir "UltimateFileConverter.WinUI.exe"
if (-not (Test-Path $AppExe)) { throw "Publish did not produce $AppExe" }
Write-Host "    Published to $PublishDir"

# --------------------------------------------------------------------------
Write-Host "==> Building MSI with WiX..."
if (-not (Get-Command wix.exe -ErrorAction SilentlyContinue)) {
    Write-Host "    Installing WiX as a global .NET tool..."
    dotnet tool install --global wix | Out-Null
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
}

# The .wxs reads these as $(env.*), which is portable across WiX v4/v5/v6.
$env:UFC_VERSION    = $Version
$env:UFC_PUBLISHDIR = $PublishDir
$env:UFC_ICONFILE   = $IconFile

$Wxs     = Join-Path $InstallerSrc "Product.wxs"
$MsiPath = Join-Path $MsiDir "ULTIMATE-FILE-CONVERTER-$Version-x64.msi"
wix build $Wxs -arch x64 -o $MsiPath
if ($LASTEXITCODE -ne 0) { throw "wix build failed." }
Write-Host "    MSI:  $MsiPath"

# --------------------------------------------------------------------------
Write-Host "==> Building EXE installer with Inno Setup..."
$Iscc = $null
$cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($cmd) { $Iscc = $cmd.Source }
foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe")) {
    if (-not $Iscc -and (Test-Path $candidate)) { $Iscc = $candidate }
}

if ($Iscc) {
    $Iss = Join-Path $InstallerSrc "setup.iss"
    & $Iscc "/DAppVersion=$Version" "/DPublishDir=$PublishDir" "/DIconFile=$IconFile" "/O$SetupDir" $Iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup (ISCC.exe) failed." }
    Write-Host "    EXE:  $(Join-Path $SetupDir "ULTIMATE-FILE-CONVERTER-Setup-$Version-x64.exe")"
} else {
    Write-Warning "Inno Setup (ISCC.exe) not found - skipped the EXE installer. Install Inno Setup 6 and rerun to build it."
}

# --------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Done. Artifacts under $DistRoot"
Get-ChildItem -Path $MsiDir, $SetupDir -File -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("    {0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
