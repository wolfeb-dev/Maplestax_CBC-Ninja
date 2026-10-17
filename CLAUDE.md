# Project rules

## NinjaScript
- Any NinjaScript .cs can be test-compiled headlessly (no NinjaTrader, no F5):
  `python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py <file.cs> [more.cs ...]`
  It builds the real NinjaTrader.Custom project (auto-located under Documents\NinjaTrader*\bin\Custom) with the file(s) added by absolute path; live NT files are never modified. Judge on `0 Error(s)` / exit code; pre-existing NT noise (CS0436, CS8012, MSB3245, MSB3884) is auto-filtered from the warning report. Use this to verify every NinjaScript change before claiming it compiles.

## Trading guards
- No backtest/strategy result is kept or committed until the `backtest-skeptic` subagent validates it. This is a mandatory domain gate, so it is the one exception to "never spawn a subagent to review your own work".
