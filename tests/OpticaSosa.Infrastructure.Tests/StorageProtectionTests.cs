using System.Security.AccessControl;
using System.Security.Principal;
using OpticaSosa.Infrastructure.Persistence;
using Xunit;

namespace OpticaSosa.Infrastructure.Tests;

public sealed class StorageProtectionTests
{
    [Fact]
    public void WindowsStorageExcludesInheritedBroadAccessAndChildrenInheritRestrictedRules()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "OpticaSosa.AclTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            StorageProtection.RestrictToCurrentWindowsUser(root);
            var allowed = new[]
            {
                WindowsIdentity.GetCurrent().User!.Value,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value
            };
            var acl = new DirectoryInfo(root).GetAccessControl();
            Assert.True(acl.AreAccessRulesProtected);
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                Assert.Contains(rule.IdentityReference.Value, allowed);
            var child = Path.Combine(root, "probe.txt");
            File.WriteAllText(child, "synthetic");
            var rules = new FileInfo(child).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
            Assert.NotEmpty(rules.Cast<FileSystemAccessRule>());
            foreach (FileSystemAccessRule rule in rules)
                Assert.Contains(rule.IdentityReference.Value, allowed);
        }
        finally
        {
            var target = new DirectoryInfo(Path.GetFullPath(root));
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OpticaSosa.AclTests"));
            if (!string.Equals(target.Parent?.FullName, parent, StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParseExact(target.Name, "N", out _)) throw new InvalidOperationException("Ruta de limpieza inválida.");
            if (target.Exists) target.Delete(recursive: true);
        }
    }
}
