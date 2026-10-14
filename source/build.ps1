$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows .NET Framework 4.7.2 或更新版本。' }
$references = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkPath $_) }
$references += @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $frameworkPath 'WPF') $_) }
$sourceFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
$compilerArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $appRoot 'Airline.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Airline.ico')) { $compilerArgs += '/win32icon:' + (Join-Path $PSScriptRoot 'Airline.ico') }
& $compilerPath @compilerArgs @references @sourceFiles
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
# DPI behavior is initialized in Main; the portable executable needs no sidecar config.
if (Test-Path -LiteralPath (Join-Path $appRoot 'Airline.exe.config')) { Remove-Item -LiteralPath (Join-Path $appRoot 'Airline.exe.config') -Force }
Write-Output ('已生成：' + (Join-Path $appRoot 'Airline.exe'))
