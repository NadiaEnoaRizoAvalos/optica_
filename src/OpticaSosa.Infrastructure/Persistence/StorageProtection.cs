using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;

namespace OpticaSosa.Infrastructure.Persistence;

public static class StorageProtection
{
    [SupportedOSPlatform("windows")]
    public static void RestrictToCurrentWindowsUser(string directory)
    {
        var identity = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("No se pudo identificar al usuario de Windows.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[]
        {
            identity,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        })
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }
}
