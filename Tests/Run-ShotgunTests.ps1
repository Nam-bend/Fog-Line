param(
    [string]$UnityData = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Force -Path Logs | Out-Null
    & "$UnityData\NetCoreRuntime\dotnet.exe" "$UnityData\DotNetSdkRoslyn\csc.dll" `
        /nologo /noconfig /nostdlib /target:exe /out:Logs/ShotgunActionTests.exe `
        "/reference:$UnityData\MonoBleedingEdge\lib\mono\4.5\mscorlib.dll" `
        Assets/_Project/Scripts/Weapons/ShotgunAmmo.cs `
        Assets/_Project/Scripts/Weapons/ShotgunAction.cs Tests/ShotgunActionTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Shotgun test compilation failed.' }
    & "$UnityData\MonoBleedingEdge\bin\mono.exe" Logs/ShotgunActionTests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Shotgun tests failed.' }
}
finally {
    Pop-Location
}
