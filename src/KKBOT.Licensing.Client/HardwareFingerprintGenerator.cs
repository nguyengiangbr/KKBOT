// Copyright (c) KKBOT. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using KKBOT.Licensing.Models;

namespace KKBOT.Licensing.Client;

/// <summary>
/// Generates stable hardware fingerprints for license binding.
/// </summary>
public static class HardwareFingerprintGenerator
{
    /// <summary>
    /// Generates a stable hardware fingerprint for license binding.
    /// Uses motherboard serial, MAC addresses, disk serials, and CPU ID.
    /// </summary>
    public static HardwareFingerprint Generate()
    {
        var fingerprint = new HardwareFingerprint
        {
            MotherboardSerial = GetMotherboardSerial(),
            MacAddresses = GetMacAddresses(),
            DiskSerials = GetDiskSerials(),
            CpuId = GetCpuId(),
            BiosVersion = GetBiosVersion()
        };

        return fingerprint;
    }

    /// <summary>
    /// Generates a stable SHA256 hash for license binding.
    /// </summary>
    public static string GenerateHash()
    {
        var fingerprint = Generate();
        return fingerprint.ComputeHash();
    }

    /// <summary>
    /// Gets the motherboard serial number.
    /// </summary>
    private static string GetMotherboardSerial()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    var serial = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                    {
                        return serial;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try reading from /sys/class/dmi/id/board_serial
                var path = "/sys/class/dmi/id/board_serial";
                if (File.Exists(path))
                {
                    var serial = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(serial))
                    {
                        return serial;
                    }
                }
            }

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets MAC addresses of physical network adapters.
    /// </summary>
    private static string GetMacAddresses()
    {
        try
        {
            var macs = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT MACAddress FROM Win32_NetworkAdapter WHERE PhysicalAdapter = True AND MACAddress IS NOT NULL");
                
                foreach (var obj in searcher.Get())
                {
                    var mac = obj["MACAddress"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(mac) && mac != "00:00:00:00:00:00")
                    {
                        macs.Add(mac.Replace(":", "").Replace("-", "").ToLowerInvariant());
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var netPath = "/sys/class/net";
                if (Directory.Exists(netPath))
                {
                    foreach (var iface in Directory.GetDirectories(netPath))
                    {
                        var addrPath = Path.Combine(iface, "address");
                        if (File.Exists(addrPath))
                        {
                            var mac = File.ReadAllText(addrPath).Trim().ToLowerInvariant();
                            if (mac != "00:00:00:00:00:00")
                            {
                                macs.Add(mac.Replace(":", ""));
                            }
                        }
                    }
                }
            }

            macs.Sort();
            return string.Join(";", macs);
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets disk serial numbers.
    /// </summary>
    private static string GetDiskSerials()
    {
        try
        {
            var serials = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType = 'Fixed hard disk media'");
                
                foreach (var obj in searcher.Get())
                {
                    var serial = obj["SerialNumber"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(serial))
                    {
                        serials.Add(serial);
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try lsblk or /sys/block
                var blockPath = "/sys/block";
                if (Directory.Exists(blockPath))
                {
                    foreach (var disk in Directory.GetDirectories(blockPath))
                    {
                        var serialPath = Path.Combine(disk, "device", "serial");
                        if (File.Exists(serialPath))
                        {
                            var serial = File.ReadAllText(serialPath).Trim();
                            if (!string.IsNullOrEmpty(serial))
                            {
                                serials.Add(serial);
                            }
                        }
                    }
                }
            }

            serials.Sort();
            return string.Join(";", serials);
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets CPU ID.
    /// </summary>
    private static string GetCpuId()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    var cpuId = obj["ProcessorId"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(cpuId))
                    {
                        return cpuId;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read from /proc/cpuinfo
                if (File.Exists("/proc/cpuinfo"))
                {
                    var lines = File.ReadAllLines("/proc/cpuinfo");
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("serial") || line.StartsWith("Serial"))
                        {
                            var parts = line.Split(':');
                            if (parts.Length > 1)
                            {
                                return parts[1].Trim();
                            }
                        }
                    }
                }
            }

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Gets BIOS version.
    /// </summary>
    private static string GetBiosVersion()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
                foreach (var obj in searcher.Get())
                {
                    var version = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var path = "/sys/class/dmi/id/bios_version";
                if (File.Exists(path))
                {
                    var version = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
            }

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Generates a fingerprint string for display/debugging.
    /// </summary>
    public static string ToDisplayString()
    {
        var fp = Generate();
        return $@"
Motherboard: {fp.MotherboardSerial}
MAC Addresses: {fp.MacAddresses}
Disk Serials: {fp.DiskSerials}
CPU ID: {fp.CpuId}
BIOS Version: {fp.BiosVersion}
Hash: {fp.ComputeHash()}
";
    }
}

/// <summary>
/// Hardware fingerprint data container.
/// </summary>
public sealed class HardwareFingerprint
{
    public string MotherboardSerial { get; init; } = string.Empty;
    public string MacAddresses { get; init; } = string.Empty;
    public string DiskSerials { get; init; } = string.Empty;
    public string CpuId { get; init; } = string.Empty;
    public string BiosVersion { get; init; } = string.Empty;

    /// <summary>
    /// Generates a stable SHA256 hash for license binding.
    /// </summary>
    public string ComputeHash()
    {
        var components = new[]
        {
            MotherboardSerial,
            MacAddresses,
            DiskSerials,
            CpuId,
            BiosVersion
        }
        .Where(s => !string.IsNullOrEmpty(s) && s != "unknown")
        .OrderBy(s => s);

        var combined = string.Join("|", components);
        
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Creates a fingerprint from a hash (for verification).
    /// </summary>
    public static HardwareFingerprint FromHash(string hash)
    {
        return new HardwareFingerprint
        {
            // Cannot reverse hash, but can store for verification
        };
    }
}

