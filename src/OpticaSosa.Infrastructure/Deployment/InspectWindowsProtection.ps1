$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$report = [ordered]@{ Edition='No comprobada'; ProviderAvailable=$false; ServiceState='No instalado'; ServiceStartMode='No comprobado'; ServiceProcessId=0; Volumes=@(); Alert=$null }
try { $report.Edition = (Get-CimInstance Win32_OperatingSystem).Caption } catch { $report.Alert='No se pudo comprobar la edición de Windows.' }
try {
    $service = Get-CimInstance Win32_Service -Filter "Name='OpticaSosa'"
    if ($service) { $report.ServiceState=$service.State; $report.ServiceStartMode=$service.StartMode; $report.ServiceProcessId=[int]$service.ProcessId }
} catch { $report.ServiceState='No comprobado'; $report.Alert='No se pudo consultar el servicio.' }
try {
    $namespace = 'root/CIMV2/Security/MicrosoftVolumeEncryption'
    $volumes = @(Get-CimInstance -Namespace $namespace -ClassName Win32_EncryptableVolume)
    $report.ProviderAvailable=$true
    $report.Volumes = @($volumes | ForEach-Object {
        $protection=Invoke-CimMethod -InputObject $_ -MethodName GetProtectionStatus
        $conversion=Invoke-CimMethod -InputObject $_ -MethodName GetConversionStatus
        $lock=Invoke-CimMethod -InputObject $_ -MethodName GetLockStatus
        $recovery=Invoke-CimMethod -InputObject $_ -MethodName GetKeyProtectors -Arguments @{ KeyProtectorType=[uint32]3 }
        if ($protection.ReturnValue -ne 0 -or $conversion.ReturnValue -ne 0 -or $lock.ReturnValue -ne 0 -or $recovery.ReturnValue -ne 0) { throw 'Estado no comprobado.' }
        [ordered]@{ VolumeId=$_.DeviceID; ProtectionStatus=[int]$protection.ProtectionStatus; ConversionStatus=[int]$conversion.ConversionStatus; LockStatus=[int]$lock.LockStatus; RecoveryProtectors=@($recovery.VolumeKeyProtectorID | Where-Object { $_ }).Count }
    })
} catch { $report.ProviderAvailable=$false; $report.Volumes=@(); $report.Alert='BitLocker no verificable: puede faltar compatibilidad, componente o permiso de consulta. No se acredita cifrado ni recuperación.' }
$report | ConvertTo-Json -Depth 5 -Compress
