[CmdletBinding()]
param([string]$SignerSha256, [string]$BackupSignerSha256, [string]$Backup, [string]$Workspace, [int]$Port=5092, [switch]$NonInteractive, [switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$kit=[IO.Path]::GetFullPath($PSScriptRoot)
function Child([string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw 'Ruta inválida en el kit.' }
    $full=[IO.Path]::GetFullPath((Join-Path $kit $Relative))
    if (-not $full.StartsWith($kit.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Archivo fuera del kit.' }
    $entry=Get-Item -LiteralPath $full -Force
    while ($entry) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se admiten enlaces/uniones.' }
        if ($entry -is [IO.FileInfo]) { $entry=$entry.Directory } else { $entry=$entry.Parent }
    }
    return $full
}
function Pick-Folder([string]$Description) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog=[Windows.Forms.FolderBrowserDialog]::new(); $dialog.Description=$Description
    try {
        if ($dialog.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { throw 'Selección cancelada.' }
        return $dialog.SelectedPath
    } finally { $dialog.Dispose() }
}
if (-not $SignerSha256 -and -not $NonInteractive) { $SignerSha256=Read-Host 'Huella PUBLICA SHA-256 recibida por canal independiente (no contraseña)' }
if ($SignerSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Falta una huella pública válida aprobada fuera del kit.' }
$manifest=Child 'complete.json'; $signature=Child 'manifest.p7s'
if ((Get-Item -LiteralPath $manifest).Length -gt 4MB -or (Get-Item -LiteralPath $signature).Length -gt 64KB) { throw 'Manifiesto o firma fuera de límites.' }
Add-Type -AssemblyName System.Security
$content=[Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes($manifest))
$cms=[Security.Cryptography.Pkcs.SignedCms]::new($content,$true)
$cms.Decode([IO.File]::ReadAllBytes($signature))
if (-not $cms.Detached -or $cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].DigestAlgorithm.Value -ne '2.16.840.1.101.3.4.2.1') { throw 'Firma CMS no permitida.' }
$sha=[Security.Cryptography.SHA256]::Create()
try { $fingerprint=([BitConverter]::ToString($sha.ComputeHash($cms.SignerInfos[0].Certificate.RawData))).Replace('-','') } finally { $sha.Dispose() }
if ($fingerprint -ne $SignerSha256) { throw 'El firmante no coincide con la huella independiente.' }
$cms.CheckSignature($true)
$metadata=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($metadata.Format -ne 1 -or $metadata.Purpose -ne 'VISOR-FICTICIO-52B') { throw 'No es un paquete ficticio compatible del visor.' }
$allowed=@('complete.json','manifest.p7s')
foreach ($entry in $metadata.Files.PSObject.Properties) {
    $file=Child $entry.Name
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Archivo alterado: '+$entry.Name) }
    $allowed+=@($entry.Name.Replace('/','\'))
}
foreach ($file in Get-ChildItem -LiteralPath $kit -File -Recurse -Force) {
    if ($file.FullName.Substring($kit.Length+1) -notin $allowed) { throw 'El paquete contiene archivos ajenos al manifiesto.' }
}
$runtime=Child 'runtime\dotnet.exe'; $application=Child 'app\OpticaSosa.Viewer.dll'
$previous=@{}
foreach ($name in @('DOTNET_ROOT','DOTNET_ROOT_X64','DOTNET_MULTILEVEL_LOOKUP')) { $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:DOTNET_ROOT=Join-Path $kit 'runtime'; $env:DOTNET_ROOT_X64=$env:DOTNET_ROOT; $env:DOTNET_MULTILEVEL_LOOKUP='0'
    $sdks=@(& $runtime --list-sdks)
    if ($LASTEXITCODE -ne 0 -or $sdks.Count -ne 0) { throw 'El runtime privado no pasó la comprobación sin SDK.' }
    if ($VerifyOnly) { Write-Output 'KIT DEL VISOR VERIFICADO; SIN SDK; NO SE INICIO SERVIDOR'; return }
    if (-not $NonInteractive) {
        if ((Read-Host 'Confirmá que usarás EXCLUSIVAMENTE DATOS FICTICIOS escribiendo SI') -cne 'SI') { throw 'Prueba no confirmada.' }
    }
    if (-not $Backup -and -not $NonInteractive -and (Read-Host '¿Usar el respaldo FICTICIO incluido? SI / NO') -cne 'SI') {
        $Backup=Pick-Folder 'Elegí la carpeta de un respaldo FICTICIO completo firmado (complete.json), nunca la base operativa.'
    }
    if (-not $Backup) {
        $Backup=Join-Path $kit 'backup'
        if (-not $BackupSignerSha256) { $BackupSignerSha256=$SignerSha256 } # Sólo el fixture incluido comparte firmante.
    }
    if (-not $BackupSignerSha256 -and -not $NonInteractive) { $BackupSignerSha256=Read-Host 'Huella PUBLICA del firmante del RESPALDO, aprobada fuera de esa copia' }
    if ($BackupSignerSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Falta confianza independiente para el respaldo seleccionado.' }
    if (-not $Workspace -and -not $NonInteractive) {
        $parent=Pick-Folder 'Elegí una carpeta LOCAL PRIVADA previamente autorizada. Se creará dentro una subcarpeta nueva del visor; no se cambian permisos.'
        $Workspace=Join-Path $parent ('VisorFicticio-'+[guid]::NewGuid().ToString('N'))
    }
    if (-not $Workspace -or $Port -lt 1024 -or $Port -gt 65535) { throw 'Indicá carpeta nueva y puerto local válido.' }
    Write-Output ('Abrí http://127.0.0.1:'+ $Port +' cuando el visor termine de verificar. Ctrl+C detiene el visor. No desconectes el disco durante la preparación.')
    & $runtime $application --contentRoot (Join-Path $kit 'app') --environment Production --urls ('http://127.0.0.1:'+$Port) --Viewer:BackupDirectory ([IO.Path]::GetFullPath($Backup)) --Viewer:Workspace ([IO.Path]::GetFullPath($Workspace)) --Viewer:FictitiousDataConfirmed true --Backups:TrustedSigningCertificatesSha256:0 $BackupSignerSha256
    if ($LASTEXITCODE -ne 0) { throw 'El visor rechazó la preparación o no pudo iniciar. Conservar archivos; no se sobrescribió la instalación operativa.' }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
