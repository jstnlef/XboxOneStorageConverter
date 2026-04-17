using System.Text.RegularExpressions;

namespace XboxOneStorageConverter.Cli;

internal sealed class MacDiskService
{
    private static readonly Regex WholeDiskIdentifierPattern = new("^disk\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DiskUtilProcessRunner _diskUtil;
    private readonly MbrDeviceModeService _mbrDeviceModeService;

    public MacDiskService(DiskUtilProcessRunner diskUtil, MbrDeviceModeService mbrDeviceModeService)
    {
        _diskUtil = diskUtil;
        _mbrDeviceModeService = mbrDeviceModeService;
    }

    public IReadOnlyList<DiskDevice> ListCandidateDisks()
    {
        var root = PlistValueParser.ParseRootDictionary(_diskUtil.Run("list", "-plist"));
        var disks = new List<DiskDevice>();

        foreach (var diskEntry in PlistValueParser.GetDictionaryArray(root, "AllDisksAndPartitions"))
        {
            var identifier = PlistValueParser.GetString(diskEntry, "DeviceIdentifier");
            if (string.IsNullOrWhiteSpace(identifier) || !WholeDiskIdentifierPattern.IsMatch(identifier))
            {
                continue;
            }

            var info = GetDiskInfoDictionary(identifier);
            if (!IsCandidateDisk(info))
            {
                continue;
            }

            disks.Add(BuildDiskDevice(identifier, info, diskEntry));
        }

        return disks
            .OrderBy(static device => device.Identifier, StringComparer.Ordinal)
            .ToList();
    }

    public DiskDevice GetCandidateDisk(string identifier)
    {
        identifier = NormalizeDiskIdentifier(identifier);

        return ListCandidateDisks().SingleOrDefault(device => device.Identifier.Equals(identifier, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"'{identifier}' is not an external physical disk target.");
    }

    public ModeChangeResult SetMode(string identifier, DeviceMode targetMode)
    {
        var device = GetCandidateDisk(identifier);
        DeviceMode currentMode;

        try
        {
            currentMode = _mbrDeviceModeService.ReadMode(device.RawDeviceNode);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Raw disk access to {device.RawDeviceNode} requires sudo.");
        }

        if (currentMode == DeviceMode.Unknown)
        {
            throw new InvalidOperationException(
                "The disk does not look like an Xbox external storage device. Refusing to modify an unknown MBR.");
        }

        if (currentMode == targetMode)
        {
            return new ModeChangeResult(
                device.Identifier,
                targetMode,
                GetModeDisplayName(targetMode),
                "No changes were needed.");
        }

        _diskUtil.Run("unmountDisk", device.DeviceNode);
        var newMode = _mbrDeviceModeService.WriteMode(device.RawDeviceNode, targetMode);

        var postActionMessage = targetMode == DeviceMode.Pc
            ? $"Reconnect the drive or run 'diskutil mountDisk {device.DeviceNode}' if macOS does not remount it automatically."
            : "Eject and reconnect the drive before plugging it back into the console.";

        return new ModeChangeResult(
            device.Identifier,
            newMode,
            GetModeDisplayName(newMode),
            postActionMessage);
    }

    private DiskDevice BuildDiskDevice(
        string identifier,
        IReadOnlyDictionary<string, object?> info,
        IReadOnlyDictionary<string, object?> diskEntry)
    {
        var deviceNode = PlistValueParser.GetString(info, "DeviceNode") ?? $"/dev/{identifier}";
        var rawDeviceNode = GetRawDeviceNode(deviceNode);
        var mode = DeviceMode.Unknown;
        string modeDisplayName;
        string? modeWarning = null;

        try
        {
            mode = _mbrDeviceModeService.ReadMode(rawDeviceNode);
            modeDisplayName = GetModeDisplayName(mode);
        }
        catch (UnauthorizedAccessException)
        {
            modeDisplayName = "Need sudo";
            modeWarning = $"Run with sudo to read {rawDeviceNode}.";
        }
        catch (IOException ioException)
        {
            modeDisplayName = "Read error";
            modeWarning = ioException.Message;
        }

        return new DiskDevice(
            identifier,
            deviceNode,
            rawDeviceNode,
            PlistValueParser.GetString(info, "MediaName") ?? PlistValueParser.GetString(info, "IORegistryEntryName") ?? identifier,
            PlistValueParser.GetString(info, "BusProtocol") ?? "n/a",
            PlistValueParser.GetInt64(info, "TotalSize", PlistValueParser.GetInt64(info, "Size")),
            GetVolumeNames(diskEntry),
            mode,
            modeDisplayName,
            modeWarning);
    }

    private IReadOnlyDictionary<string, object?> GetDiskInfoDictionary(string identifier)
    {
        return PlistValueParser.ParseRootDictionary(_diskUtil.Run("info", "-plist", $"/dev/{NormalizeDiskIdentifier(identifier)}"));
    }

    private static IReadOnlyList<string> GetVolumeNames(IReadOnlyDictionary<string, object?> diskEntry)
    {
        var partitions = PlistValueParser.GetDictionaryArray(diskEntry, "Partitions");
        return partitions
            .Select(partition => PlistValueParser.GetString(partition, "VolumeName"))
            .Where(static volumeName => !string.IsNullOrWhiteSpace(volumeName))
            .Cast<string>()
            .ToList();
    }

    private static bool IsCandidateDisk(IReadOnlyDictionary<string, object?> info)
    {
        if (!PlistValueParser.GetBool(info, "WholeDisk"))
        {
            return false;
        }

        if (!string.Equals(PlistValueParser.GetString(info, "VirtualOrPhysical"), "Physical", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (PlistValueParser.GetBool(info, "Internal"))
        {
            return false;
        }

        if (!PlistValueParser.GetBool(info, "WritableMedia", PlistValueParser.GetBool(info, "Writable")))
        {
            return false;
        }

        return PlistValueParser.GetBool(info, "RemovableMediaOrExternalDevice")
            || PlistValueParser.GetBool(info, "Ejectable")
            || PlistValueParser.GetBool(info, "Removable");
    }

    private static string NormalizeDiskIdentifier(string identifier)
    {
        identifier = identifier.Trim();

        if (identifier.StartsWith("/dev/", StringComparison.Ordinal))
        {
            identifier = Path.GetFileName(identifier);
        }

        if (!WholeDiskIdentifierPattern.IsMatch(identifier))
        {
            throw new InvalidOperationException($"'{identifier}' is not a whole-disk identifier like 'disk4'.");
        }

        return identifier;
    }

    private static string GetRawDeviceNode(string deviceNode)
    {
        const string prefix = "/dev/disk";

        return deviceNode.StartsWith(prefix, StringComparison.Ordinal)
            ? $"/dev/rdisk{deviceNode[prefix.Length..]}"
            : deviceNode;
    }

    private static string GetModeDisplayName(DeviceMode mode)
    {
        return mode switch
        {
            DeviceMode.Xbox => "Xbox",
            DeviceMode.Pc => "PC",
            _ => "Unknown",
        };
    }
}
