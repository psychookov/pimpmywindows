using System.Text;

namespace PimpMyWindows.Core.Diagnostics;

/// <summary>
/// Texto para colar ao pedir ajuda. Não inclui nome do computador, nome de usuário, números de série,
/// endereços de rede nem caminhos de arquivos.
/// </summary>
public static class SupportReport
{
    public static string Build(HardwareSnapshot hardware, DriverReport? drivers, DiskReport? disks)
    {
        var text = new StringBuilder();
        text.AppendLine("Relatório do Pimp my Windows (sem nome, números de série nem dados pessoais)");
        text.AppendLine();

        text.AppendLine($"Windows: {hardware.OsName} {hardware.OsVersion} {hardware.Architecture}".TrimEnd());
        if (hardware.Board.Length > 0)
        {
            text.AppendLine($"Placa-mãe: {hardware.Board}");
        }

        if (hardware.CpuName.Length > 0)
        {
            text.AppendLine($"Processador: {hardware.CpuName} ({hardware.CpuCores} núcleos, {hardware.CpuThreads} threads)");
        }

        if (hardware.RamTotalBytes > 0)
        {
            var speeds = hardware.RamModules.Select(m => m.SpeedMhz).Where(s => s > 0).Distinct().ToList();
            var speed = speeds.Count == 0 ? string.Empty : $", {string.Join("/", speeds)} MHz";
            text.AppendLine($"Memória: {HardwareService.FormatBytes(hardware.RamTotalBytes)} em {hardware.RamModules.Count} pente(s){speed}");
        }

        foreach (var gpu in hardware.Gpus)
        {
            var date = gpu.DriverDate is null ? string.Empty : $" de {gpu.DriverDate:dd/MM/yyyy}";
            text.AppendLine($"Vídeo: {gpu.Name} (driver {gpu.DriverVersion}{date})");
        }

        if (disks is not null)
        {
            text.AppendLine();
            foreach (var disk in disks.Disks)
            {
                text.AppendLine(
                    $"Disco: {disk.Name} · {disk.MediaType} · {HardwareService.FormatBytes(disk.SizeBytes)} · {disk.VerdictText}" +
                    (disk.WearPercent is null ? string.Empty : $" · desgaste {disk.WearPercent}%"));
            }
        }

        if (drivers is not null)
        {
            text.AppendLine();
            text.AppendLine($"Drivers: {drivers.Drivers.Count} instalados, {drivers.Problems.Count} aparelho(s) com problema");
            foreach (var problem in drivers.Problems)
            {
                text.AppendLine($"  - {problem.DeviceName}: código {problem.ErrorCode} ({problem.Description})");
            }
        }

        return text.ToString().TrimEnd();
    }
}
