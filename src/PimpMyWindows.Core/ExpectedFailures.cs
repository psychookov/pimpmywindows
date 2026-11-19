using System.Security;

namespace PimpMyWindows.Core;

/// <summary>
/// Falhas que esperamos ao mexer no sistema (permissão negada, arquivo bloqueado, valor inesperado).
/// Só estas são tratadas e viram mensagem para o usuário; qualquer outra é um bug de verdade e não deve ser escondida.
/// </summary>
internal static class ExpectedFailures
{
    public static bool Is(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException
            or NotSupportedException or InvalidCastException or InvalidOperationException or ArgumentException;
}
