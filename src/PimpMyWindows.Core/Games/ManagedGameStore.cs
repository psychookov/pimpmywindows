using System.Text.Json;
using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Games;

/// <summary>
/// Guarda os jogos que o usuário adicionou ou otimizou. Sem isso, um jogo desinstalado ou fora da lista automática
/// sumiria da Central Desfazer mesmo com o ajuste ainda gravado no Windows.
/// </summary>
public sealed class ManagedGameStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public ManagedGameStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<GameInfo> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var games = JsonSerializer.Deserialize<List<GameInfo>>(File.ReadAllText(_path), Options) ?? [];
            // Arquivo adulterado ou de outra versão: só aceita itens com id coerente com o executável.
            return games
                .Where(g => !string.IsNullOrWhiteSpace(g.ExecutablePath) && g.Id == GameInfo.IdFor(g.ExecutablePath))
                .ToList();
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex) || ex is JsonException)
        {
            return [];
        }
    }

    public void Remember(GameInfo game)
    {
        var games = Load().Where(g => g.Id != game.Id).Append(game).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(games, Options));
    }

    /// <summary>Todos os ajustes de jogo dos jogos lembrados (para a Central Desfazer reconhecer os backups).</summary>
    public IReadOnlyList<Tweaks.TweakDefinition> AllTweaks() => Load().SelectMany(GameTweaks.For).ToList();
}
