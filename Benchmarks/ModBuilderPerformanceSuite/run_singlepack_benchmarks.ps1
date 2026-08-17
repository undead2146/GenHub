$ErrorActionPreference = "Stop"

$projectDir = "Z:\GeneralsGamePatch\Patch104pZH"
$csBenchExe = "Z:\GeneralsHub\GenHub\GenHub.Benchmarks\bin\Release\net8.0\GenHub.Benchmarks.exe"
$resultsDir = "Z:\GeneralsHub\Benchmarks\ModBuilderPerformanceSuite\results_gamepatch"

Write-Host "=========================================================="
Write-Host " STARTING SINGLE PACK (FullEnglish) ISOLATED BENCHMARKS   "
Write-Host "=========================================================="

# 1. C# ImageSharp Single Pack (FullEnglish) Cold Build
Write-Host "`n>>> [1/2] Running C# ImageSharp Single Pack (FullEnglish) Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw1 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --pack="FullEnglish" --image-engine=imagesharp --project-dir="$projectDir" --json-out="$resultsDir\csharp_imagesharp_singlepack.json"
$sw1.Stop()
$timeCsImageSharpSingle = $sw1.Elapsed.TotalSeconds
Write-Host ">>> C# ImageSharp FullEnglish Completed: $timeCsImageSharpSingle seconds"

# 2. C# Crunch Single Pack (FullEnglish) Cold Build
Write-Host "`n>>> [2/2] Running C# Crunch Single Pack (FullEnglish) Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --pack="FullEnglish" --image-engine=crunch --project-dir="$projectDir" --json-out="$resultsDir\csharp_crunch_singlepack.json"
$sw2.Stop()
$timeCsCrunchSingle = $sw2.Elapsed.TotalSeconds
Write-Host ">>> C# Crunch FullEnglish Completed: $timeCsCrunchSingle seconds"

Write-Host "`n=========================================================="
Write-Host " ALL SINGLE PACK BENCHMARKS COMPLETED SUCCESSFULLY!       "
Write-Host " C# ImageSharp FullEnglish Single Pack : $timeCsImageSharpSingle s"
Write-Host " C# Crunch FullEnglish Single Pack     : $timeCsCrunchSingle s"
Write-Host "=========================================================="
