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
    [string]$Runtime = "win-x64"
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
Write-Host "==> Publishing WinUI app (self-contained, unpackaged, $Runtime)..."
dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:Version=$Version `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

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
