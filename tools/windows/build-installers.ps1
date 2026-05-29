<#
Builds Windows deliverables for the WinUI 3 port:
  - dist/windows/exe/ULTIMATE-FILE-CONVERTER.exe (published app executable)
  - dist/windows/msi/ULTIMATE-FILE-CONVERTER-<version>-x64.msi (WiX MSI)
  - dist/windows/setup/ULTIMATE-FILE-CONVERTER-Setup-<version>-x64.exe (Inno Setup EXE installer, when ISCC.exe is installed)

Run from the repository root in a Windows Developer PowerShell:
  pwsh ./tools/windows/build-installers.ps1 -Configuration Release
#>
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.1",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Project = Join-Path $RepoRoot "Windows\UltimateFileConverter.WinUI\UltimateFileConverter.WinUI.csproj"
$DistRoot = Join-Path $RepoRoot "dist\windows"
$PublishDir = Join-Path $DistRoot "publish"
$ExeDir = Join-Path $DistRoot "exe"
$MsiDir = Join-Path $DistRoot "msi"
$SetupDir = Join-Path $DistRoot "setup"
$InstallerWork = Join-Path $DistRoot "installer-work"

New-Item -ItemType Directory -Force -Path $PublishDir, $ExeDir, $MsiDir, $SetupDir, $InstallerWork | Out-Null

Write-Host "Publishing WinUI app..."
dotnet publish $Project -c $Configuration -r $Runtime --self-contained true -p:WindowsPackageType=None -o $PublishDir

$AppExe = Join-Path $PublishDir "UltimateFileConverter.WinUI.exe"
if (-not (Test-Path $AppExe)) {
    throw "Expected app executable was not produced: $AppExe"
}
Copy-Item $AppExe (Join-Path $ExeDir "ULTIMATE-FILE-CONVERTER.exe") -Force

Write-Host "Preparing WiX MSI manifest..."
$Wxs = Join-Path $InstallerWork "UltimateFileConverter.wxs"
$ProductCode = [guid]::NewGuid().ToString().ToUpperInvariant()
$UpgradeCode = "A5A3DA6A-1C67-4DA8-9E2D-62AE234E6B6D"
$Files = Get-ChildItem $PublishDir -Recurse -File | Sort-Object FullName
$Components = New-Object System.Text.StringBuilder
$Refs = New-Object System.Text.StringBuilder
$Index = 0
foreach ($File in $Files) {
    $Index++
    $Id = "cmp$Index"
    $FileId = "fil$Index"
    $Source = $File.FullName.Replace('&','&amp;')
    [void]$Components.AppendLine("      <Component Id=`"$Id`" Guid=`"$([guid]::NewGuid().ToString().ToUpperInvariant())`">")
    [void]$Components.AppendLine("        <File Id=`"$FileId`" Source=`"$Source`" />")
    if ($File.Name -eq "UltimateFileConverter.WinUI.exe") {
        [void]$Components.AppendLine("        <Shortcut Id=`"ApplicationStartMenuShortcut`" Directory=`"ApplicationProgramsFolder`" Name=`"ULTIMATE-FILE-CONVERTER`" WorkingDirectory=`"INSTALLFOLDER`" Advertise=`"no`" />")
        [void]$Components.AppendLine("        <Shortcut Id=`"ApplicationDesktopShortcut`" Directory=`"DesktopFolder`" Name=`"ULTIMATE-FILE-CONVERTER`" WorkingDirectory=`"INSTALLFOLDER`" Advertise=`"no`" />")
        [void]$Components.AppendLine("        <RemoveFolder Id=`"ApplicationProgramsFolder`" On=`"uninstall`" />")
        [void]$Components.AppendLine("        <RegistryValue Root=`"HKCU`" Key=`"Software\ULTIMATE-FILE-CONVERTER`" Name=`"installed`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />")
    }
    [void]$Components.AppendLine("      </Component>")
    [void]$Refs.AppendLine("      <ComponentRef Id=`"$Id`" />")
}

@"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="ULTIMATE-FILE-CONVERTER" Manufacturer="ULTIMATE-FILE-CONVERTER" Version="$Version" UpgradeCode="$UpgradeCode" Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of ULTIMATE-FILE-CONVERTER is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="ULTIMATE-FILE-CONVERTER">
$Components
      </Directory>
    </StandardDirectory>
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="ULTIMATE-FILE-CONVERTER" />
    </StandardDirectory>
    <StandardDirectory Id="DesktopFolder" />
    <Feature Id="MainFeature" Title="ULTIMATE-FILE-CONVERTER" Level="1">
$Refs
    </Feature>
  </Package>
</Wix>
"@ | Set-Content -Encoding UTF8 $Wxs

$Wix = Get-Command wix.exe -ErrorAction SilentlyContinue
if (-not $Wix) {
    Write-Host "Installing WiX .NET tool..."
    dotnet tool install --global wix --version 5.*
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
}

$MsiPath = Join-Path $MsiDir "ULTIMATE-FILE-CONVERTER-$Version-x64.msi"
wix build $Wxs -arch x64 -o $MsiPath

$IssPath = Join-Path $InstallerWork "UltimateFileConverter.iss"
@"
[Setup]
AppId={{A5A3DA6A-1C67-4DA8-9E2D-62AE234E6B6D}
AppName=ULTIMATE-FILE-CONVERTER
AppVersion=$Version
AppPublisher=ULTIMATE-FILE-CONVERTER
DefaultDirName={autopf}\ULTIMATE-FILE-CONVERTER
DefaultGroupName=ULTIMATE-FILE-CONVERTER
OutputDir=$SetupDir
OutputBaseFilename=ULTIMATE-FILE-CONVERTER-Setup-$Version-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "$PublishDir\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ULTIMATE-FILE-CONVERTER"; Filename: "{app}\UltimateFileConverter.WinUI.exe"
Name: "{commondesktop}\ULTIMATE-FILE-CONVERTER"; Filename: "{app}\UltimateFileConverter.WinUI.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\UltimateFileConverter.WinUI.exe"; Description: "Launch ULTIMATE-FILE-CONVERTER"; Flags: nowait postinstall skipifsilent
"@ | Set-Content -Encoding UTF8 $IssPath

$Inno = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($Inno) {
    & $Inno.Source $IssPath
} else {
    Write-Warning "ISCC.exe was not found. Install Inno Setup 6 and rerun to create the EXE installer. Script written to $IssPath"
}

Write-Host "Windows artifacts:"
Write-Host "  Published app EXE: $(Join-Path $ExeDir 'ULTIMATE-FILE-CONVERTER.exe')"
Write-Host "  MSI installer:     $MsiPath"
Write-Host "  EXE installer:     $(Join-Path $SetupDir "ULTIMATE-FILE-CONVERTER-Setup-$Version-x64.exe")"
