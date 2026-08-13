# Extracts the OptionZoneGeometry region from the real MapleStaxCBC.cs and compiles it
# together with OptionZoneTests.cs, so the tests exercise the exact bytes that ship
# rather than a copy that can drift.
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $repo 'MapleStaxCBC.cs'
$out  = Join-Path $env:TEMP 'maplestax-zone-tests'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$text = Get-Content -Raw $src
$m = [regex]::Match($text, '(?s)//\s*<OptionZoneGeometry>(.*?)//\s*</OptionZoneGeometry>')
if (-not $m.Success) { throw 'OptionZoneGeometry region markers not found in MapleStaxCBC.cs' }
Set-Content -Path (Join-Path $out 'OptionZoneGeometry.g.cs') -Value $m.Groups[1].Value -Encoding UTF8

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

& $csc /nologo /target:exe /out:"$out\zonetests.exe" "$out\OptionZoneGeometry.g.cs" (Join-Path $PSScriptRoot 'OptionZoneTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'zone-test harness compile failed' }

& "$out\zonetests.exe"
exit $LASTEXITCODE
