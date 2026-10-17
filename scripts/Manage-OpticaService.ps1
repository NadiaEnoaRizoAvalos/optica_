[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [ValidateSet('Plan','Install','Start','Stop','Uninstall')][string]$Action='Plan',
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$DataDirectory,
    [ValidateRange(1024,65535)][int]$Port=5080,
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$OperationalChangesAuthorized,
    [switch]$PermissionsReviewed
)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
function Assert-LocalPath([string]$Path) {
    if ($Path -notmatch '^[A-Za-z]:\\' -or $Path -match '["\r\n]') { throw 'Se requiere ruta absoluta local sin comillas ni saltos de línea.' }
    $full=[IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($full.Length -le 3) { throw 'No usar la raíz del disco.' }
    for ($item=[IO.DirectoryInfo]::new($full); $item; $item=$item.Parent) {
        if ($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'La ruta no admite enlaces.' }
    }
    return $full
}
$exe=Assert-LocalPath $Executable
$data=Assert-LocalPath $DataDirectory
if ([IO.Path]::GetFileName($exe) -ne 'OpticaSosa.Web.exe') { throw 'Seleccionar el ejecutable publicado OpticaSosa.Web.exe.' }
$contentRoot=[IO.Path]::GetDirectoryName($exe)
if ($data.Equals($contentRoot,[StringComparison]::OrdinalIgnoreCase) -or $data.StartsWith($contentRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $contentRoot.StartsWith($data+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Separar datos privados de los binarios publicados.' }
$binary='"'+$exe+'" --contentRoot "'+$contentRoot+'" --environment Production --Storage:DataDirectory "'+$data+'" --urls http://localhost:'+ $Port
$plan=[ordered]@{ PlanOnly=($Action -eq 'Plan'); Name='OpticaSosa'; BinaryPath=$binary; Startup='Automatic'; Recovery='restart/60000/restart/120000/restart/300000'; ResetSeconds=86400; Identity='Cuenta dedicada existente, no administradora; credencial solicitada sólo al instalar'; ExternalWrites='Desactivadas al instalar; habilitación separada'; Uninstall='Sólo registro del servicio; no borrar binarios, datos, certificados o copias' }
if ($Action -eq 'Plan') { $plan | ConvertTo-Json -Depth 3; return }
if (-not $OperationalChangesAuthorized -or -not $PermissionsReviewed) { throw 'Faltan autorización operativa específica y revisión de cuenta/ACL/claves/recuperación.' }
if (-not (Test-Path -LiteralPath $exe -PathType Leaf) -or ((Get-Item -LiteralPath $exe).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Ejecutable publicado no disponible o enlazado.' }
$identity=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'La gestión del servicio requiere terminal elevada autorizada; el servicio no debe ejecutarse como administrador.' }
$service=Get-CimInstance Win32_Service -Filter "Name='OpticaSosa'"
if ($Action -eq 'Install') {
    if ($service) { throw 'El servicio ya existe. No se sobrescribe ni modifica automáticamente.' }
    if (-not $Credential -or $Credential.UserName -match '(^|\\)(SYSTEM|LocalSystem|Administrator|Administrador)$') { throw 'Proporcionar credencial de cuenta dedicada existente y no administradora.' }
    if (-not (Test-Path -LiteralPath (Join-Path $data 'optica.db') -PathType Leaf)) { throw 'Preparar/revisar la base con el procedimiento de instalación; este script no migra.' }
    foreach ($settings in @('appsettings.json','appsettings.Production.json')) {
        $settingsPath=Join-Path $contentRoot $settings
        if (Test-Path -LiteralPath $settingsPath) {
            $config=Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
            if ($config.Backups.ExternalWritesAuthorized -eq $true -or $config.Backups.SigningEnabled -eq $true) { throw 'Instalar inicialmente con escritura externa y firmas operativas desactivadas.' }
        }
    }
    if ($env:Backups__ExternalWritesAuthorized -eq 'true' -or $env:Backups__SigningEnabled -eq 'true') { throw 'Revisar las variables de entorno antes de instalar.' }
    if ($PSCmdlet.ShouldProcess('OpticaSosa','Registrar inicio automático y recuperación, sin iniciar el servicio')) {
        New-Service -Name 'OpticaSosa' -BinaryPathName $binary -DisplayName 'Sistema de Gestión para Ópticas' -StartupType Automatic -Credential $Credential | Out-Null
        & "$env:SystemRoot\System32\sc.exe" failure OpticaSosa reset= 86400 actions= restart/60000/restart/120000/restart/300000
        if ($LASTEXITCODE -ne 0) { throw 'No se configuró recuperación. No iniciar; conservar estado y revisar manualmente.' }
        & "$env:SystemRoot\System32\sc.exe" failureflag OpticaSosa 1
        if ($LASTEXITCODE -ne 0) { throw 'No se confirmó recuperación. No iniciar; revisar manualmente.' }
    }
    return
}
if (-not $service -or $service.PathName -cne $binary) { throw 'El servicio no existe o su ejecutable/configuración no coincide. No se modifica otro servicio.' }
if ($PSCmdlet.ShouldProcess('OpticaSosa',$Action)) {
    if ($Action -eq 'Start') { Start-Service -Name 'OpticaSosa' }
    else {
        $controller=Get-Service -Name 'OpticaSosa'
        if ($controller.Status -ne 'Stopped') { Stop-Service -Name 'OpticaSosa'; $controller.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(60)) }
        if ($Action -eq 'Uninstall') {
            & "$env:SystemRoot\System32\sc.exe" delete OpticaSosa
            if ($LASTEXITCODE -ne 0) { throw 'No se eliminó el registro. No borrar archivos ni reintentar destructivamente.' }
        }
    }
}
