using System.Runtime.InteropServices;

namespace PimpMyWindows.Core.Controllers;

/// <summary>
/// Lê controles compatíveis com Xbox pela API XInput do Windows. Controles de PlayStation e Switch só aparecem
/// aqui se algum programa (como o Steam Input) os apresentar ao Windows como um controle Xbox.
/// </summary>
public sealed class XInputProbe : IControllerProbe
{
    private const int Success = 0;

    public ControllerState? GetState(int slot)
    {
        try
        {
            if (XInputGetState(slot, out var state) != Success)
            {
                return null;
            }

            var pad = state.Gamepad;
            return new ControllerState(
                (ControllerButtons)pad.Buttons,
                pad.LeftTrigger,
                pad.RightTrigger,
                pad.ThumbLX,
                pad.ThumbLY,
                pad.ThumbRX,
                pad.ThumbRY);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    public bool SetVibration(int slot, ushort left, ushort right)
    {
        try
        {
            return XInputSetState(slot, new Vibration { Left = left, Right = right }) == Success;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(int userIndex, out State state);

    [DllImport("xinput1_4.dll")]
    private static extern int XInputSetState(int userIndex, Vibration vibration);

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration
    {
        public ushort Left;
        public ushort Right;
    }
}
