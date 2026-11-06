$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'dist\IndicadorPredator.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 first.' }
$scratch = Join-Path $PSScriptRoot 'build\tests'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$cases = @(
    @{Json='{"Enable_Eco":1}'; Expected='eco'},
    @{Json='{"Enable_Eco":0}'; Expected='equilibrado-usbc'},
    @{Json='{"Enable_Eco":2}'; Expected='indisponivel'},
    @{Json='{"Enable_Eco":"1"}'; Expected='indisponivel'},
    @{Json='{}'; Expected='indisponivel'},
    @{Json='{broken'; Expected='indisponivel'}
    @{Json='{"Enable_Eco":0,"Scenario":{"active":1,"profiles":[{"opMode":1},{"opMode":0}]}}'; Adapter=1; Expected='silencioso'},
    @{Json='{"Enable_Eco":0,"Scenario":{"active":1,"profiles":[{"opMode":0},{"opMode":1}]}}'; Adapter=1; Expected='equilibrado'},
    @{Json='{"Enable_Eco":0,"Scenario":{"active":0,"profiles":[{"opMode":4}]}}'; Adapter=1; Expected='desempenho'},
    @{Json='{"Enable_Eco":0,"Scenario":{"active":0,"profiles":[{"opMode":5}]}}'; Adapter=1; Expected='turbo'},
    @{Json='{"Enable_Eco":0,"Scenario":{"active":0,"profiles":[{"opMode":5}]}}'; Adapter=4; Expected='equilibrado-usbc'},
    @{Json='{"Enable_Eco":1,"Scenario":{"active":0,"profiles":[{"opMode":5}]}}'; Adapter=4; Expected='eco'},
    @{Json='{"Enable_Eco":0,"Scenario":{"active":2,"profiles":[{"opMode":5}]}}'; Adapter=1; Expected='indisponivel'}
)
$index = 0
foreach ($case in $cases) {
    $index++
    $inputFile = Join-Path $scratch ('input-' + $index + '.json')
    $resultFile = Join-Path $scratch ('result-' + $index + '.txt')
    Set-Content -LiteralPath $inputFile -Value $case.Json -Encoding UTF8
    $adapter = if ($case.ContainsKey('Adapter')) { $case.Adapter } else { 4 }
    $process = Start-Process -FilePath $exe -ArgumentList @('--probe', ('"'+$inputFile+'"'), ('"'+$resultFile+'"'), [string]$adapter) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $resultFile -Raw) -ne $case.Expected) {
        throw "State case $index failed."
    }
}
$resultFile = Join-Path $scratch 'missing-result.txt'
$missing = Join-Path $scratch ([Guid]::NewGuid().ToString() + '.json')
$process = Start-Process -FilePath $exe -ArgumentList @('--probe', ('"'+$missing+'"'), ('"'+$resultFile+'"')) -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $resultFile -Raw) -ne 'indisponivel') { throw 'Missing-file case failed.' }
Add-Type -AssemblyName System.Drawing
$assembly = [Reflection.Assembly]::LoadFile($exe)
foreach ($name in @('eco', 'equilibrado', 'desempenho', 'turbo')) {
    foreach ($size in @(16,20,24,32)) {
        $stream = $assembly.GetManifestResourceStream($name + '.ico')
        if ($null -eq $stream) { throw "Missing icon: $name" }
        try {
            $icon = New-Object System.Drawing.Icon -ArgumentList $stream,$size,$size
            try { if ($icon.Width -ne $size -or $icon.Height -ne $size) { throw "Wrong icon size: $name" } }
            finally { $icon.Dispose() }
        } finally { $stream.Dispose() }
    }
}
Write-Output "PASS: $($cases.Count + 1) state cases and 16 embedded-icon checks."
