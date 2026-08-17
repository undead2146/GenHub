$ErrorActionPreference = "Stop"

$projectDir = "Z:\GeneralsGamePatch\Patch104pZH"
$csBenchExe = "Z:\GeneralsHub\GenHub\GenHub.Benchmarks\bin\Release\net8.0\GenHub.Benchmarks.exe"
$goBenchExe = "Z:\GeneralsHub\.gomodbuilder_ref\gomodbuilder.exe"
$resultsDir = "Z:\GeneralsHub\Benchmarks\ModBuilderPerformanceSuite\results_gamepatch"

Write-Host "=========================================================="
Write-Host " STARTING AUTOMATED STRICTLY SEQUENTIAL BENCHMARK SUITE   "
Write-Host "=========================================================="

# 1. C# ImageSharp Full Cold Project Build
Write-Host "`n>>> [1/4] Running C# ImageSharp Full Cold Project Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw1 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --image-engine=imagesharp --project-dir="$projectDir" --json-out="$resultsDir\csharp_imagesharp_cold.json"
$sw1.Stop()
$timeCsImageSharp = $sw1.Elapsed.TotalSeconds
Write-Host ">>> C# ImageSharp Cold Completed: $timeCsImageSharp seconds"

# 2. C# Crunch Full Cold Project Build
Write-Host "`n>>> [2/4] Running C# Crunch Full Cold Project Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --image-engine=crunch --project-dir="$projectDir" --json-out="$resultsDir\csharp_crunch_cold.json"
$sw2.Stop()
$timeCsCrunch = $sw2.Elapsed.TotalSeconds
Write-Host ">>> C# Crunch Cold Completed: $timeCsCrunch seconds"

# 3. C# Warm Incremental Build
Write-Host "`n>>> [3/4] Running C# Warm Incremental Build..."
$sw3 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --image-engine=imagesharp --project-dir="$projectDir" --json-out="$resultsDir\csharp_imagesharp_warm.json"
$sw3.Stop()
$timeCsWarm = $sw3.Elapsed.TotalSeconds
Write-Host ">>> C# Warm Incremental Completed: $timeCsWarm seconds"

# 4. Go Parallel Single Pack (FullEnglish) Build
Write-Host "`n>>> [4/4] Running Go Parallel Single Pack (FullEnglish) Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw4 = [System.Diagnostics.Stopwatch]::StartNew()
& $goBenchExe -build -parallel -pack="FullEnglish" -project="$projectDir"
$sw4.Stop()
$timeGoParallelSingle = $sw4.Elapsed.TotalSeconds
Write-Host ">>> Go Parallel Single Pack Completed: $timeGoParallelSingle seconds"

Write-Host "`n=========================================================="
Write-Host " ALL BENCHMARKS COMPLETED SUCCESSFULLY! SUMMARY:          "
Write-Host " C# ImageSharp Full Cold : $timeCsImageSharp s"
Write-Host " C# Crunch Full Cold     : $timeCsCrunch s"
Write-Host " C# Warm Incremental     : $timeCsWarm s"
Write-Host " Go Parallel FullEnglish : $timeGoParallelSingle s"
Write-Host "=========================================================="
