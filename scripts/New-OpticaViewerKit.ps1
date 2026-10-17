[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath (Join-Path $project 'OpticaSosa.sln'))) { throw 'No se encontró el proyecto.' }
$installed=Join-Path $env:ProgramFiles 'dotnet'; $version='10.0.13'
foreach ($item in @('dotnet.exe',"host\fxr\$version\hostfxr.dll","shared\Microsoft.NETCore.App\$version","shared\Microsoft.AspNetCore.App\$version")) {
    if (-not (Test-Path -LiteralPath (Join-Path $installed $item))) { throw 'Falta el runtime existente 10.0.13. No se instala ni descarga automáticamente.' }
}
$root=Join-Path $project ('data\visor-emergencia-52b-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$oldExport=$env:OPTICA_VIEWER_EXPORT_ROOT
Push-Location $project
try {
    dotnet publish src/OpticaSosa.Viewer/OpticaSosa.Viewer.csproj -c Release --no-restore --self-contained false -o (Join-Path $root 'app')
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación del visor.' }
    $runtime=Join-Path $root 'runtime'; New-Item -ItemType Directory -Path $runtime | Out-Null
    foreach ($name in @('dotnet.exe','LICENSE.txt','ThirdPartyNotices.txt')) { Copy-Item -LiteralPath (Join-Path $installed $name) -Destination $runtime }
    $fxr=Join-Path $runtime 'host\fxr'; New-Item -ItemType Directory -Path $fxr -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $installed "host\fxr\$version") -Destination $fxr -Recurse
    foreach ($framework in @('Microsoft.NETCore.App','Microsoft.AspNetCore.App')) {
        $destination=Join-Path $runtime ('shared\'+$framework); New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $installed "shared\$framework\$version") -Destination $destination -Recurse
    }
    [IO.File]::WriteAllText((Join-Path $root 'INICIAR-VISOR.ps1'), [IO.File]::ReadAllText((Join-Path $project 'scripts\Start-OpticaViewer.ps1')), [Text.UTF8Encoding]::new($true))
    Copy-Item -LiteralPath (Join-Path $project 'docs\VISOR-EMERGENCIA.md') -Destination (Join-Path $root 'LEER-PRIMERO.md')
    [IO.File]::WriteAllText((Join-Path $root 'INICIAR-VISOR.cmd'),"@echo off`r`n`"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`" -NoProfile -STA -ExecutionPolicy RemoteSigned -File `"%~dp0INICIAR-VISOR.ps1`"`r`npause`r`n")
    [ordered]@{ BaseCommit=(git rev-parse HEAD); SourceTreeDirty=[bool](git status --porcelain); Runtime=$version; Architecture='win-x64'; ProductionReady=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'VERSION.json') -Encoding UTF8
    dotnet build OpticaSosa.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de pruebas.' }
    $env:OPTICA_VIEWER_EXPORT_ROOT=$root
    dotnet test tests/OpticaSosa.Web.Tests/OpticaSosa.Web.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~ViewerPortableKitTests --verbosity minimal -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'El paquete no pasó la preparación ficticia.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=$root+'.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($root,$zip,[IO.Compression.CompressionLevel]::Optimal,$true)
    $receipt=[ordered]@{ Folder=$root; Zip=$zip; ZipSha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash; SignerSha256=[IO.File]::ReadAllText((Join-Path $root 'firmante-publico.txt')); ExternalDiskWritten=$false; PhysicalTest='PENDIENTE'; ProductionReady=$false }
    $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'data\latest-viewer-kit.json') -Encoding UTF8
    $receipt | ConvertTo-Json
} finally { $env:OPTICA_VIEWER_EXPORT_ROOT=$oldExport; Pop-Location }
