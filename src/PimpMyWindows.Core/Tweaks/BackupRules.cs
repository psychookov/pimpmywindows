using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tweaks;

internal static class BackupRules
{
    /// <summary>
    /// Um backup só é usado para reverter se cobre exatamente os valores do ajuste. Isso impede que um arquivo
    /// de backup adulterado (ou de uma versão antiga do catálogo) faça o programa gravar em outro lugar.
    /// </summary>
    public static bool Covers(TweakDefinition tweak, TweakBackup backup) =>
        backup.Values.Count == tweak.Changes.Count
        && tweak.Changes.All(c => backup.Values.Count(v => v.Location == c.Location) == 1);
}
