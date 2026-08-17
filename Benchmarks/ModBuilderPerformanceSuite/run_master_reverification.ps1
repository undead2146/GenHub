$ErrorActionPreference = "Stop"

$projectDir = "Z:\GeneralsGamePatch\Patch104pZH"
$csBenchExe = "Z:\GeneralsHub\GenHub\GenHub.Benchmarks\bin\Release\net8.0\GenHub.Benchmarks.exe"
$resultsDir = "Z:\GeneralsHub\Benchmarks\ModBuilderPerformanceSuite\results_gamepatch"

Write-Host "=========================================================="
Write-Host " STARTING MASTER ISOLATED REVERIFICATION BENCHMARKS       "
Write-Host "=========================================================="

# 1. C# ImageSharp Full Project Cold Build
Write-Host "`n>>> [1/4] Running C# ImageSharp Full Project Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw1 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --image-engine=imagesharp --project-dir="$projectDir" --json-out="$resultsDir\csharp_imagesharp_cold.json"
$sw1.Stop()
$timeCsImageSharpFull = $sw1.Elapsed.TotalSeconds
Write-Host ">>> C# ImageSharp Full Project Completed: $timeCsImageSharpFull seconds"

# 2. C# Crunch Full Project Cold Build
Write-Host "`n>>> [2/4] Running C# Crunch Full Project Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --image-engine=crunch --project-dir="$projectDir" --json-out="$resultsDir\csharp_crunch_cold.json"
$sw2.Stop()
$timeCsCrunchFull = $sw2.Elapsed.TotalSeconds
Write-Host ">>> C# Crunch Full Project Completed: $timeCsCrunchFull seconds"

# 3. C# ImageSharp Single Pack (FullEnglish) Cold Build
Write-Host "`n>>> [3/4] Running C# ImageSharp Single Pack (FullEnglish) Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw3 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --pack="FullEnglish" --image-engine=imagesharp --project-dir="$projectDir" --json-out="$resultsDir\csharp_imagesharp_singlepack.json"
$sw3.Stop()
$timeCsImageSharpSingle = $sw3.Elapsed.TotalSeconds
Write-Host ">>> C# ImageSharp FullEnglish Completed: $timeCsImageSharpSingle seconds"

# 4. C# Crunch Single Pack (FullEnglish) Cold Build
Write-Host "`n>>> [4/4] Running C# Crunch Single Pack (FullEnglish) Cold Build..."
Remove-Item -Recurse -Force "$projectDir\.Build" -ErrorAction SilentlyContinue
$sw4 = [System.Diagnostics.Stopwatch]::StartNew()
& $csBenchExe --bench=full-build --pack="FullEnglish" --image-engine=crunch --project-dir="$projectDir" --json-out="$resultsDir\csharp_crunch_singlepack.json"
$sw4.Stop()
$timeCsCrunchSingle = $sw4.Elapsed.TotalSeconds
Write-Host ">>> C# Crunch FullEnglish Completed: $timeCsCrunchSingle seconds"

Write-Host "`n=========================================================="
Write-Host " ALL MASTER REVERIFICATION BENCHMARKS COMPLETED! SUMMARY: "
Write-Host " C# ImageSharp Full Project Cold : $timeCsImageSharpFull s"
Write-Host " C# Crunch Full Project Cold     : $timeCsCrunchFull s"
Write-Host " C# ImageSharp FullEnglish Single: $timeCsImageSharpSingle s"
Write-Host " C# Crunch FullEnglish Single    : $timeCsCrunchSingle s"
Write-Host "=========================================================="
