namespace PimpMyWindows.Core.Controllers;

/// <summary>Botões do controle, com os mesmos valores bit a bit usados pelo XInput.</summary>
[Flags]
public enum ControllerButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftStick = 0x0040,
    RightStick = 0x0080,
    LeftBumper = 0x0100,
    RightBumper = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>Leitura de um instante do controle. Gatilhos vão de 0 a 255 e eixos dos direcionais analógicos de -32768 a 32767.</summary>
public sealed record ControllerState(
    ControllerButtons Buttons,
    byte LeftTrigger,
    byte RightTrigger,
    short LeftX,
    short LeftY,
    short RightX,
    short RightY);

/// <summary>Acesso ao controle (separado para testar sem um controle de verdade).</summary>
public interface IControllerProbe
{
    /// <summary>Estado do controle da posição 0 a 3, ou <c>null</c> se não houver controle nela.</summary>
    ControllerState? GetState(int slot);

    /// <summary>Liga os motores de vibração (0 a 65535). Retorna falso se o controle não respondeu.</summary>
    bool SetVibration(int slot, ushort left, ushort right);
}

public enum DriftVerdict
{
    Good,
    Slight,
    Drifting,
}

public sealed record DriftResult(double MaxDeflection, DriftVerdict Verdict)
{
    public string Text => Verdict switch
    {
        DriftVerdict.Good => "Sem desvio: o direcional volta ao centro.",
        DriftVerdict.Slight => "Desvio leve: dentro do que a maioria dos jogos ignora.",
        _ => "Desvio (drift): o direcional marca movimento mesmo parado. Tente limpar em volta da alavanca; se continuar, o componente está gasto.",
    };
}
