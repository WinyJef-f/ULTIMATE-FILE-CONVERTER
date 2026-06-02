<#
.SYNOPSIS
    Builds the Windows deliverables for ULTIMATE-FILE-CONVERTER (WinUI 3).

.DESCRIPTION
    1. Publishes the WinUI app as a self-contained, unpackaged win-x64 app.
    2. Builds an EXE installer with Inno Setup 6.

    Outputs land in dist/windows/:
        publish/                                              the published app
        setup/ULTIMATE-FILE-CONVERTER-Setup-<version>-x64.exe Inno Setup installer

.EXAMPLE
    pwsh ./tools/windows/build-installers.ps1 -Configuration Release
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.2.0",
    [string]$Runtime = "win-x64",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$RepoRoot     = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$Project      = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\UltimateFileConverter.WinUI.csproj"
$CliProject   = Join-Path $RepoRoot "Windows\UltimateFileConverter.CLI\UltimateFileConverter.CLI.csproj"
$IconFile     = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\Assets\app.ico"
$InstallerSrc = Join-Path $PSScriptRoot "installer"
$DistRoot     = Join-Path $RepoRoot "dist\windows"
$PublishDir   = Join-Path $DistRoot "publish"
$SetupDir     = Join-Path $DistRoot "setup"

if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PublishDir, $SetupDir | Out-Null

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

# Explicit Build before Publish: this is the pass that compiles XAML -> XBF and runs MakePri,
# producing $(AssemblyName).pri in the canonical bin dir. /t:Publish with an explicit PublishDir
# redirects intermediate output and, on its own, can leave the app PRI out of the publish folder.
Write-Host "==> Building WinUI app ($Runtime)..."
& $MSBuild $Project /t:Build `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:RuntimeIdentifier=$Runtime `
    /p:SelfContained=true `
    /p:WindowsPackageType=None `
    /p:WindowsAppSDKSelfContained=true `
    /p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

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

# Build the CLI companion (ufc.exe) and copy it into the publish folder.
Write-Host "==> Building ufc CLI..."
& dotnet publish $CliProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -o "$PublishDir"
if ($LASTEXITCODE -ne 0) { throw "ufc CLI build failed." }
$UfcExe = Join-Path $PublishDir "ufc.exe"
if (Test-Path $UfcExe) {
    Write-Host "    ufc.exe built and staged."
} else {
    Write-Warning "ufc.exe was not found in publish folder after build - CLI will be missing from installer."
}

# Safety net: ensure the app PRI (compiled XAML) is in the publish folder. Without it the app
# starts but throws XamlParseException 0x802B000A on the first LoadComponent call. The csproj
# AfterTargets hook normally copies it; if it didn't, pull it from the bin output of the Build above.
$PriName = "UltimateFileConverter.WinUI.pri"
if (-not (Test-Path (Join-Path $PublishDir $PriName))) {
    $BinPri = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\$Runtime\$PriName"
    if (Test-Path $BinPri) {
        Copy-Item $BinPri (Join-Path $PublishDir $PriName) -Force
        Write-Host "    Copied $PriName into publish folder."
    } else {
        throw "Required $PriName not found in publish folder or bin output ($BinPri)."
    }
}
Write-Host "    Published to $PublishDir"

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
Get-ChildItem -Path $SetupDir -File -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("    {0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB)) }
