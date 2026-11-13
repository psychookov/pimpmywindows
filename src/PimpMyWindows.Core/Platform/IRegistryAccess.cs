namespace PimpMyWindows.Core.Platform;

/// <summary>
/// Acesso ao registro do Windows. Existe como interface para que o motor de ajustes
/// possa ser testado sem tocar no registro real.
/// </summary>
public interface IRegistryAccess
{
    /// <summary>Lê um valor. Retorna <c>null</c> se a chave ou o valor não existirem.</summary>
    RegistryData? Read(RegistryLocation location);

    /// <summary>Grava um valor, criando a chave se necessário.</summary>
    void Write(RegistryLocation location, RegistryData data);

    /// <summary>Remove um valor. Não falha se ele já não existir.</summary>
    void Delete(RegistryLocation location);

    /// <summary>Lista os nomes dos valores de uma chave (vazio se a chave não existir).</summary>
    IReadOnlyList<string> GetValueNames(RegistryRoot root, string subKey);
}
