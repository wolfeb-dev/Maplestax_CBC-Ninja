# Extracts the AdvancedState region from the real MapleStaxCBCAdvanced.cs and compiles it
# together with AdvancedStateTests.cs, so the tests exercise the exact bytes that ship
# rather than a copy that can drift.
$ErrorActionPreference = 'Stop'

$advanced = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $advanced 'MapleStaxCBCAdvanced.cs'
$out  = Join-Path $env:TEMP 'maplestax-advanced-tests'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$text = Get-Content -Raw $src
$m = [regex]::Match($text, '(?s)//\s*<AdvancedState>(.*?)//\s*</AdvancedState>')
if (-not $m.Success) { throw 'AdvancedState region markers not found in MapleStaxCBCAdvanced.cs' }
Set-Content -Path (Join-Path $out 'AdvancedState.g.cs') -Value $m.Groups[1].Value -Encoding UTF8

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

& $csc /nologo /target:exe /out:"$out\advancedtests.exe" "$out\AdvancedState.g.cs" (Join-Path $PSScriptRoot 'AdvancedStateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'advanced-test harness compile failed' }

& "$out\advancedtests.exe"
exit $LASTEXITCODE
