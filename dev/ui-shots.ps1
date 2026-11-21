$ErrorActionPreference = 'Continue'
$wd = 'F:\WorkSpace\LensPop'
$exe = Join-Path $wd 'dist\test-harness.exe'
$p = Start-Process -FilePath $exe -ArgumentList '--ui' -WorkingDirectory $wd -PassThru
$null = $p.WaitForExit(90000)
Start-Sleep -Milliseconds 500
'harness exit=' + $p.ExitCode
Get-ChildItem (Join-Path $wd 'dev\shots') -Filter *.png | ForEach-Object { $_.Name + ' ' + $_.Length }
