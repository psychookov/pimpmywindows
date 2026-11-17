using System.Security.AccessControl;
using System.Security.Principal;

namespace PimpMyWindows.Core.Safety;

/// <summary>
/// Protege a pasta de backups da máquina (<c>%ProgramData%\PimpMyWindows-Backups</c>): só Administradores e SYSTEM
/// escrevem; usuários comuns apenas leem. Antes de confiar em uma pasta que já existe, confere o dono dela, para que
/// um programa comum não possa criá-la antecipadamente e plantar backups falsos.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class WindowsDirectoryGuard : IDirectoryGuard
{
    private readonly Func<bool> _isAdministrator;

    public WindowsDirectoryGuard(Func<bool> isAdministrator)
    {
        _isAdministrator = isAdministrator;
    }

    public void BeforeRead(string directory)
    {
        if (Directory.Exists(directory))
        {
            EnsureTrustedOwner(directory);
        }
    }

    public void BeforeWrite(string directory)
    {
        if (!_isAdministrator())
        {
            throw new UnauthorizedAccessException(
                "A pasta de cópias de segurança da máquina só pode ser alterada com o programa executado como administrador.");
        }

        if (Directory.Exists(directory))
        {
            EnsureTrustedOwner(directory);
        }
        else
        {
            Directory.CreateDirectory(directory);
        }

        Restrict(directory);
    }

    private static void EnsureTrustedOwner(string directory)
    {
        var security = new DirectoryInfo(directory).GetAccessControl();
        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        if (owner is null || (!owner.Equals(admins) && !owner.Equals(system)))
        {
            throw new UnauthorizedAccessException(
                "A pasta de cópias de segurança da máquina não é confiável (o dono dela não é o administrador). Ela foi ignorada.");
        }
    }

    private static void Restrict(string directory)
    {
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }
}
