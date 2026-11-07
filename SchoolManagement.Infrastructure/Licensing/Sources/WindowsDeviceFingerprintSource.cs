#if WINDOWS
using System.Management;
using Microsoft.Win32;
#endif
using System.Security.Cryptography;
using System.Text;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Domain.Licensing;

namespace SchoolManagement.Infrastructure.Licensing.Sources;

/// <summary>
/// SHA-256 over MachineGuid + BIOS serial + baseboard serial + disk serial.
/// A "strong" fingerprint needs both MachineGuid and at least one hardware
/// serial. The UI warns when it is weak, because reinstalling Windows can then
/// change the hash and require a reissued license.
/// </summary>
public sealed class WindowsDeviceFingerprintSource : IDeviceFingerprintSource
{
    public Task<DeviceFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var machineGuid = GetMachineGuid();
        var biosSerial = GetWmiString("Win32_BIOS", "SerialNumber");
        var motherboard = GetWmiString("Win32_BaseBoard", "SerialNumber");
        var diskId = GetWmiString("Win32_DiskDrive", "SerialNumber");

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(machineGuid))
        {
            parts.Add($"mg:{Normalize(machineGuid)}");
        }

        var hardwareParts = 0;
        if (!string.IsNullOrWhiteSpace(biosSerial))
        {
            parts.Add($"bios:{Normalize(biosSerial)}");
            hardwareParts++;
        }

        if (!string.IsNullOrWhiteSpace(motherboard))
        {
            parts.Add($"mb:{Normalize(motherboard)}");
            hardwareParts++;
        }

        if (!string.IsNullOrWhiteSpace(diskId))
        {
            parts.Add($"disk:{Normalize(diskId)}");
            hardwareParts++;
        }

        var usedFallback = parts.Count == 0;
        if (usedFallback)
        {
            var fallback = $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}";
            parts.Add($"fb:{Normalize(fallback)}");
        }

        var isStrong = !usedFallback &&
                       !string.IsNullOrWhiteSpace(machineGuid) &&
                       hardwareParts > 0;

        var composed = string.Join("|", parts);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(composed));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();

        return Task.FromResult(new DeviceFingerprint(hash, isStrong, usedFallback));
    }

    private static string? GetMachineGuid()
    {
#if WINDOWS
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var value = key?.GetValue("MachineGuid") as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
#else
        return null;
#endif
    }

    private static string? GetWmiString(string className, string propertyName)
    {
#if WINDOWS
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var value = obj[propertyName]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
                finally
                {
                    obj.Dispose();
                }
            }
        }
        catch
        {
            // WMI can be disabled or slow; the fingerprint then falls back.
        }
#endif
        return null;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
