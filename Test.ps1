$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'build\InstantModeTests.exe'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
$sources = @('AoE2DELatestTrainer.cs', 'Localization.cs', 'tests\InstantModeTests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$controller = Join-Path $PSScriptRoot 'ContinuousInstantModes.cs'
if (Test-Path -LiteralPath $controller) { $sources += $controller }
$sources += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'UI') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
& $compiler /nologo /target:exe /platform:x64 /main:InstantModeTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$output" @sources
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed ($LASTEXITCODE)" }
& $output
if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE)" }
