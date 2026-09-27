# Builds the Windows release package: output\Quentra-<version>-win-x64.zip
# Self-contained (no .NET SDK or runtime needed on the target). Must be built on a machine with ETABS 22.7 installed,
# so the ETABS adapter is compiled in; ETABS's own API DLL is never included (Quentra loads it from ETABS at run time).
param([string]$Version = "0.2.0-rc1")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$api = "C:\Program Files\Computers and Structures\ETABS 22\ETABSv1.dll"
if (-not (Test-Path $api)) { throw "ETABS 22.7 is not installed here ($api). Build the package on a machine with ETABS 22.7." }

$stage = Join-Path $root "output\package\Quentra-$Version-win-x64"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null

foreach ($project in "Quentra.Cli", "Quentra.Gui") {
    dotnet publish (Join-Path $root "src\$project\$project.csproj") -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:DebugType=none -p:Version=$Version -o (Join-Path $stage "app") --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed" }
}
Rename-Item (Join-Path $stage "app\Quentra.Cli.exe") "quentra.exe"
Rename-Item (Join-Path $stage "app\Quentra.Gui.exe") "quentra-gui.exe"

$leaked = Get-ChildItem -Recurse $stage -Filter "ETABSv1.dll"
if ($leaked) { throw "ETABSv1.dll must not be shipped: $($leaked.FullName -join ', ')" }

Copy-Item (Join-Path $root "docs\ETABS_INTEGRATION.md"), (Join-Path $root "docs\SNAPSHOT_FORMAT.md") $stage
$readme = @"
Quentra $Version - concrete and reinforcement quantity takeoff from ETABS

REQUIREMENTS
  Windows 10/11 x64. No .NET installation is needed.
  ETABS 22.7 (tested build 22.7.0.4095) installed and running with the model open.
  Other ETABS versions are detected but untested; Quentra refuses them unless you pass
  --pid <id> --allow-untested-version, and then marks every output as untested.

START
  GUI:  app\quentra-gui.exe          (opens http://127.0.0.1:5178 in your browser; this computer only)
  CLI:  app\quentra.exe etabs status
        app\quentra.exe etabs extract model.json --steel-approved-by "Name" --policy-approved-by "Name"
        app\quentra.exe calculate model.json run.json
        app\quentra.exe help

WHAT IT DOES AND DOES NOT DO
  ETABS is only read; Quentra never changes, saves, analyses or designs your model.
  Concrete: modeled gross and opening-adjusted volumes (member intersections retained). Not BOQ quantities.
  Steel: ETABS design reinforcement equivalents and modeled column bars only; shear, wall and slab steel is
  reported as unknown, never zero. See ETABS_INTEGRATION.md.
  Accepted runs are saved in %LOCALAPPDATA%\Quentra\runs.
"@
Set-Content (Join-Path $stage "README-FIRST.txt") $readme -Encoding UTF8

$zip = Join-Path $root "output\Quentra-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $stage -DestinationPath $zip
"Package: $zip ({0:N1} MB)" -f ((Get-Item $zip).Length / 1MB)
