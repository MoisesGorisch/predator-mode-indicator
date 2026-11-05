$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# compiler not found.' }
$out = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$iconDir = Join-Path $PSScriptRoot 'assets\icons'
$arguments = @('/nologo', '/target:winexe', '/optimize+',
    ('/out:' + (Join-Path $out 'IndicadorPredator.exe')),
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll',
    ('/win32icon:' + (Join-Path $iconDir 'equilibrado.ico')))
foreach ($name in @('eco', 'equilibrado', 'desempenho', 'turbo')) {
    $arguments += '/resource:' + (Join-Path $iconDir ($name + '.ico')) + ',' + $name + '.ico'
}
$arguments += Join-Path $PSScriptRoot 'src\IndicadorPredator.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
foreach ($name in @('README.md', 'README.pt-BR.md', 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $out -Force
}
New-Item -ItemType Directory -Path (Join-Path $out 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\icons-preview.png') -Destination (Join-Path $out 'docs') -Force
Write-Output 'Built dist/IndicadorPredator.exe'
