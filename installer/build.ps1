$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = Split-Path -Parent $appRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$appExe = Join-Path $appRoot 'Airline.exe'
$portableExe = Join-Path $outputRoot '晾图-v0.3.1-单文件版.exe'
$setupExe = Join-Path $outputRoot '晾图-v0.3.1-安装版.exe'
Copy-Item -LiteralPath $appExe -Destination $portableExe -Force
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ /utf8output (('/out:')+$setupExe) (('/win32icon:')+(Join-Path $appRoot 'source\Airline.ico')) (('/win32manifest:')+(Join-Path $appRoot 'source\app.manifest')) (('/resource:')+$appExe+',Airline.Payload') @references (Join-Path $PSScriptRoot 'Installer.cs')
if ($LASTEXITCODE -ne 0) { throw '安装程序编译失败。' }
Write-Output $portableExe
Write-Output $setupExe
