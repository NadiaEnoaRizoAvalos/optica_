[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath (Join-Path $project 'OpticaSosa.sln'))) { throw 'Ejecutar dentro del proyecto optica.' }
$runtimeVersion='10.0.13'
$installed=Join-Path $env:ProgramFiles 'dotnet'
foreach ($part in @('dotnet.exe',"host\fxr\$runtimeVersion\hostfxr.dll","shared\Microsoft.NETCore.App\$runtimeVersion\coreclr.dll","shared\Microsoft.AspNetCore.App\$runtimeVersion")) {
    if (-not (Test-Path -LiteralPath (Join-Path $installed $part))) { throw ('Falta runtime existente '+$part+'. No se descarga ni instala automáticamente.') }
}
$root=Join-Path $project ('data\prueba-recuperacion-52a-'+[guid]::NewGuid().ToString('N'))
if (Test-Path -LiteralPath $root) { throw 'El directorio debe ser nuevo.' }
New-Item -ItemType Directory -Path $root | Out-Null
Push-Location $project
$oldExport=$env:OPTICA_REHEARSAL_EXPORT_ROOT
try {
    dotnet publish src/OpticaSosa.Recovery/OpticaSosa.Recovery.csproj -c Release --no-restore --self-contained false -o (Join-Path $root 'tool')
    if ($LASTEXITCODE -ne 0) { throw 'Falló publicación.' }
    $runtime=Join-Path $root 'runtime'; New-Item -ItemType Directory -Path $runtime | Out-Null
    foreach ($file in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')) { Copy-Item -LiteralPath (Join-Path $installed $file) -Destination $runtime }
    $fxr=Join-Path $runtime 'host\fxr'; New-Item -ItemType Directory -Path $fxr -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $installed "host\fxr\$runtimeVersion") -Destination $fxr -Recurse
    foreach ($framework in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')) {
        $destination=Join-Path $runtime ('shared\'+$framework); New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $installed "shared\$framework\$runtimeVersion") -Destination $destination -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $project 'scripts\Invoke-OpticaRecoveryRehearsal.ps1') -Destination (Join-Path $root 'INICIAR-PRUEBA.ps1')
    Copy-Item -LiteralPath (Join-Path $project 'docs\PRUEBA-SEGUNDA-PC.md') -Destination (Join-Path $root 'LEER-PRIMERO.md')
    [IO.File]::WriteAllText((Join-Path $root 'INICIAR-PRUEBA.cmd'),"@echo off`r`n`"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`" -NoProfile -ExecutionPolicy RemoteSigned -File `"%~dp0INICIAR-PRUEBA.ps1`"`r`npause`r`n",[Text.Encoding]::ASCII)
    $commit=(git rev-parse HEAD).Trim()
    $dirty=@(git status --porcelain).Count -gt 0
    [ordered]@{ Kit='SIMULACRO FICTICIO 5.2.A'; CreatedUtc=[DateTime]::UtcNow.ToString('o'); BaseCommit=$commit; SourceTreeDirty=$dirty; Runtime=$runtimeVersion; Architecture='win-x64'; Windows='Windows 11 x64'; SdkIncluded=$false; RuntimeIncluded=$true; ProductionReady=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'VERSION.json') -Encoding UTF8
    dotnet build OpticaSosa.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Falló compilación del generador ficticio.' }
    $env:OPTICA_REHEARSAL_EXPORT_ROOT=$root
    dotnet test tests/OpticaSosa.Web.Tests/OpticaSosa.Web.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~PhysicalRecoveryFixtureTests --verbosity minimal -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Falló preparación/verificación ficticia. No usar este directorio.' }
    $env:OPTICA_REHEARSAL_EXPORT_ROOT=$oldExport
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=$root+'.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($root,$zip,[IO.Compression.CompressionLevel]::Optimal,$true)
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    $result=[ordered]@{ LocalFolder=$root; Zip=$zip; ZipSha256=$hash; SignerSha256=([IO.File]::ReadAllText((Join-Path $root 'firmante-publico.txt'))); ExternalDiskWritten=$false; PhysicalTest='PENDIENTE DE EJECUCION Y CONFIRMACION DEL USUARIO' }
    $result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'data\latest-physical-rehearsal-kit.json') -Encoding UTF8
    $result | ConvertTo-Json
} finally { $env:OPTICA_REHEARSAL_EXPORT_ROOT=$oldExport; Pop-Location }
