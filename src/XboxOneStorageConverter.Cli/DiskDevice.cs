namespace XboxOneStorageConverter.Cli;

internal sealed record DiskDevice(
    string Identifier,
    string DeviceNode,
    string RawDeviceNode,
    string MediaName,
    string BusProtocol,
    long SizeBytes,
    IReadOnlyList<string> VolumeNames,
    DeviceMode Mode,
    string ModeDisplayName,
    string? ModeWarning);
