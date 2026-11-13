namespace PimpMyWindows.Core;

/// <summary>
/// Resultado de uma operação, com uma mensagem em português pronta para ser mostrada ao usuário.
/// </summary>
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);

    public static OperationResult Fail(string message) => new(false, message);
}
