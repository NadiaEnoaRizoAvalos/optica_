[CmdletBinding()]
param([string]$SignerSha256, [string]$Workspace, [switch]$NonInteractive)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$kit=$PSScriptRoot
$oldEnvironment=@{}
function Child([string]$Root,[string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative)) { throw 'Ruta de kit inválida.' }
    $full=[IO.Path]::GetFullPath((Join-Path $Root $Relative))
    if ($full -ne $Root -and -not $full.StartsWith($Root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta fuera del kit.' }
    $item=Get-Item -LiteralPath $full -Force
    while ($item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'No se admiten enlaces/uniones.' }
        if ($item -is [IO.FileInfo]) { $item=$item.Directory } else { $item=$item.Parent }
    }
    return $full
}
function Verify-Kit {
    $manifest=Child $kit 'kit-manifest.json'; $signature=Child $kit 'kit-manifest.p7s'
    if ((Get-Item -LiteralPath $manifest).Length -gt 4MB -or (Get-Item -LiteralPath $signature).Length -gt 64KB) { throw 'Manifiesto/firma demasiado grande.' }
    Add-Type -AssemblyName System.Security
    $content=[Security.Cryptography.Pkcs.ContentInfo]::new([IO.File]::ReadAllBytes($manifest))
    $cms=[Security.Cryptography.Pkcs.SignedCms]::new($content,$true)
    $cms.Decode([IO.File]::ReadAllBytes($signature))
    if (-not $cms.Detached -or $cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].DigestAlgorithm.Value -ne '2.16.840.1.101.3.4.2.1') { throw 'Firma CMS no permitida.' }
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $pin=([BitConverter]::ToString($sha.ComputeHash($cms.SignerInfos[0].Certificate.RawData))).Replace('-','') } finally { $sha.Dispose() }
    if ($pin -ne $SignerSha256) { throw 'Firmante distinto de la huella aprobada por canal independiente.' }
    $cms.CheckSignature($true)
    $metadata=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($metadata.Format -ne 1 -or $metadata.Purpose -ne 'SIMULACRO FICTICIO 5.2.A') { throw 'Este kit no corresponde al simulacro.' }
    $names=@('kit-manifest.json','kit-manifest.p7s')
    foreach ($entry in $metadata.Files.PSObject.Properties) {
        $file=Child $kit $entry.Name
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Archivo alterado: '+$entry.Name) }
        $names+=@($entry.Name.Replace('/','\'))
    }
    foreach ($file in Get-ChildItem -LiteralPath $kit -File -Recurse -Force) {
        $relative=$file.FullName.Substring($kit.Length+1)
        if ($relative -notin $names) { throw 'Hay archivos ajenos al kit firmado. No continuar.' }
    }
}
function Run-Recovery([string[]]$Arguments,[string]$Log) {
    # Windows PowerShell 5.1 transforma stderr nativo en ErrorRecord; el código de salida decide el resultado.
    $ErrorActionPreference='Continue'
    & (Join-Path $kit 'runtime\dotnet.exe') (Join-Path $kit 'tool\OpticaSosa.Recovery.dll') @Arguments *> $Log
    return $LASTEXITCODE
}
try {
    if (-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64' -or [Environment]::OSVersion.Version.Build -lt 22000) { throw 'Este kit requiere Windows 11 x64 y PowerShell de 64 bits.' }
    if ([string]::IsNullOrWhiteSpace($SignerSha256)) { $SignerSha256=Read-Host 'Pegá la huella pública SHA256 entregada en el chat (no una clave privada)' }
    if ($SignerSha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Huella pública inválida.' }
    Verify-Kit
    Write-Host 'Firma y hashes del kit correctos. Sólo datos ficticios.'
    $offline=$false
    if (-not $NonInteractive) {
        if ((Read-Host 'Desconectá Wi-Fi y cable de red. Escribí SI para confirmar') -ne 'SI') { throw 'Prueba sin internet no confirmada.' }
        $offline=$true
    }
    if ([string]::IsNullOrWhiteSpace($Workspace)) {
        Write-Host ('Ejemplo: '+(Join-Path $env:LOCALAPPDATA ('OpticaSimulacro-'+[guid]::NewGuid().ToString('N'))))
        $Workspace=Read-Host 'Carpeta LOCAL NUEVA de recuperación (ruta completa, no debe existir)'
    }
    if ($Workspace -notmatch '^[A-Za-z]:\\' -or (Test-Path -LiteralPath $Workspace)) { throw 'Se requiere una carpeta local nueva; no se reemplaza ninguna existente.' }
    $Workspace=[IO.Path]::GetFullPath($Workspace).TrimEnd('\')
    if ($Workspace.StartsWith($kit.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetPathRoot($Workspace) -eq [IO.Path]::GetPathRoot($kit)) { throw 'Elegí otro volumen local, fuera del carry disk/kit.' }
    $drive=[IO.DriveInfo]::new([IO.Path]::GetPathRoot($Workspace))
    if ($drive.DriveType -ne 'Fixed' -or $drive.DriveFormat -ne 'NTFS' -or $drive.AvailableFreeSpace -lt 1GB) { throw 'Se requiere disco local NTFS y al menos 1 GiB libre.' }
    $parent=[IO.Path]::GetDirectoryName($Workspace); [void](Child $parent '.')
    New-Item -ItemType Directory -Path $Workspace | Out-Null
    $acl=[Security.AccessControl.DirectorySecurity]::new(); $acl.SetAccessRuleProtection($true,$false)
    $allowed=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')
    foreach ($sid in $allowed) { $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow')) }
    [IO.DirectoryInfo]::new($Workspace).SetAccessControl($acl)
    foreach ($ace in ([IO.DirectoryInfo]::new($Workspace).GetAccessControl()).GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        if ($ace.AccessControlType -eq 'Allow' -and $ace.IdentityReference.Value -notin $allowed) { throw 'La carpeta no quedó privada.' }
    }
    foreach ($name in @('DOTNET_ROOT','DOTNET_MULTILEVEL_LOOKUP','DOTNET_CLI_TELEMETRY_OPTOUT')) { $oldEnvironment[$name]=[Environment]::GetEnvironmentVariable($name,'Process') }
    $env:DOTNET_ROOT=Join-Path $kit 'runtime'; $env:DOTNET_MULTILEVEL_LOOKUP='0'; $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $sdks=@(& (Join-Path $kit 'runtime\dotnet.exe') --list-sdks)
    if ($LASTEXITCODE -ne 0 -or $sdks.Count -ne 0) { throw 'El runtime portátil no quedó aislado del SDK.' }
    $source=Join-Path $kit 'backup'; $empty=Join-Path $Workspace 'trabajo-vacio'; New-Item -ItemType Directory -Path $empty | Out-Null
    if ((Run-Recovery @('verify','--backup',$source,'--trust',$SignerSha256,'--workspace',$empty) (Join-Path $Workspace '01-verificacion.log')) -ne 0) { throw 'El respaldo no pasó la verificación.' }
    if ((Run-Recovery @('restore','--backup',$source,'--trust',$SignerSha256,'--workspace',$empty) (Join-Path $Workspace '02-restauracion.log')) -ne 0) { throw 'La restauración no se completó.' }
    $restored=@(Get-ChildItem -LiteralPath (Join-Path $empty 'restore-tests') -Directory)
    if ($restored.Count -ne 1) { throw 'Directorio restaurado ambiguo.' }
    $inventoryWork=Join-Path $Workspace 'inventario-vacio'; New-Item -ItemType Directory -Path $inventoryWork | Out-Null
    $inventoryLog=Join-Path $Workspace '03-inventario.json'
    if ((Run-Recovery @('inventory','--backup',$restored[0].FullName,'--trust',$SignerSha256,'--workspace',$inventoryWork) $inventoryLog) -ne 0) { throw 'No se pudo comparar inventario.' }
    $actual=Get-Content -LiteralPath $inventoryLog -Raw | ConvertFrom-Json
    $expected=Get-Content -LiteralPath (Join-Path $kit 'resultado-esperado.json') -Raw | ConvertFrom-Json
    if (-not $expected.Fictitious -or $expected.RealPatientData) { throw 'No se identificó una base ficticia.' }
    foreach ($table in $expected.Tables.PSObject.Properties) { if ($actual.($table.Name) -ne $table.Value) { throw ('No coincide tabla '+$table.Name) } }
    if ((Get-FileHash -LiteralPath (Join-Path $restored[0].FullName 'optica.db') -Algorithm SHA256).Hash -ne $expected.DatabaseSha256) { throw 'SQLite restaurada diferente del original.' }
    $bad=Join-Path $Workspace 'copia-corrupta-ficticia'; Copy-Item -LiteralPath $source -Destination $bad -Recurse
    [IO.File]::AppendAllText((Join-Path $bad 'optica.db'),'CORRUPCION FICTICIA DEL ENSAYO')
    if ((Run-Recovery @('verify','--backup',$bad,'--trust',$SignerSha256,'--workspace',$inventoryWork) (Join-Path $Workspace '04-corrupcion-rechazada.log')) -eq 0) { throw 'No se rechazó la corrupción.' }
    Verify-Kit
    [ordered]@{ Result='RECUPERACION FICTICIA CORRECTA'; AtUtc=[DateTime]::UtcNow.ToString('o'); WindowsBuild=[Environment]::OSVersion.Version.Build; Architecture='x64'; OfflineDeclaredByUser=$offline; NoSdkInPortableRuntime=$true; TablesMatch=$true; OriginalUnmodified=$true; CorruptionRejected=$true; PrivateNewWorkspace=$true; PhysicalTestRequiresUserConfirmation=$true; NoProductionEncryptionValidated=$true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Workspace 'RESULTADO.json') -Encoding UTF8
    Write-Host ('RECUPERACION FICTICIA CORRECTA. Resultado: '+(Join-Path $Workspace 'RESULTADO.json'))
    Write-Host 'No se inició servidor, servicio, migración ni programación. La seguridad/cifrado de producción siguen pendientes.'
    exit 0
} catch {
    [Console]::Error.WriteLine('PRUEBA NO COMPLETADA: '+$_.Exception.Message)
    exit 1
} finally {
    foreach ($name in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$oldEnvironment[$name],'Process') }
}
