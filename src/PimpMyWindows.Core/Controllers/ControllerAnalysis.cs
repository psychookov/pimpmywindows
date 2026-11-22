namespace PimpMyWindows.Core.Controllers;

public static class ControllerAnalysis
{
    public const double SlightThreshold = 0.06;
    public const double DriftThreshold = 0.15;

    /// <summary>Converte o eixo bruto em um número de -1 a 1.</summary>
    public static double Normalize(short raw) => Math.Max(-1.0, raw / 32767.0);

    /// <summary>Distância do centro (0 a 1) de um direcional analógico.</summary>
    public static double Deflection(short x, short y)
    {
        var nx = Normalize(x);
        var ny = Normalize(y);
        return Math.Min(1.0, Math.Sqrt((nx * nx) + (ny * ny)));
    }

    /// <summary>
    /// Avalia o desvio de um direcional deixado em repouso. Usa o maior desvio visto no período,
    /// porque um drift que só aparece de vez em quando também atrapalha o jogo.
    /// </summary>
    public static DriftResult EvaluateDrift(IEnumerable<(short X, short Y)> restingSamples)
    {
        var max = restingSamples.Select(s => Deflection(s.X, s.Y)).DefaultIfEmpty(0).Max();
        var verdict = max >= DriftThreshold ? DriftVerdict.Drifting
            : max >= SlightThreshold ? DriftVerdict.Slight
            : DriftVerdict.Good;
        return new DriftResult(max, verdict);
    }

    /// <summary>Nomes em português dos botões apertados.</summary>
    public static IReadOnlyList<string> PressedNames(ControllerButtons buttons) =>
        Names.Where(n => buttons.HasFlag(n.Button)).Select(n => n.Name).ToList();

    public static IReadOnlyList<(ControllerButtons Button, string Name)> Names { get; } =
    [
        (ControllerButtons.A, "A"),
        (ControllerButtons.B, "B"),
        (ControllerButtons.X, "X"),
        (ControllerButtons.Y, "Y"),
        (ControllerButtons.LeftBumper, "LB"),
        (ControllerButtons.RightBumper, "RB"),
        (ControllerButtons.Back, "Voltar"),
        (ControllerButtons.Start, "Iniciar"),
        (ControllerButtons.LeftStick, "L3"),
        (ControllerButtons.RightStick, "R3"),
        (ControllerButtons.DPadUp, "Cima"),
        (ControllerButtons.DPadDown, "Baixo"),
        (ControllerButtons.DPadLeft, "Esquerda"),
        (ControllerButtons.DPadRight, "Direita"),
    ];
}
