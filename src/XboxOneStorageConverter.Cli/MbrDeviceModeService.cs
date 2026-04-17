namespace XboxOneStorageConverter.Cli;

internal sealed class MbrDeviceModeService
{
    private const int SectorSize = 512;
    private const int SignatureOffset = 0x1FE;
    private static ReadOnlySpan<byte> XboxSignature => [0x99, 0xCC];
    private static ReadOnlySpan<byte> PcSignature => [0x55, 0xAA];

    public DeviceMode ReadMode(string rawDeviceNode)
    {
        var buffer = ReadSector(rawDeviceNode, FileAccess.Read);
        return GetMode(buffer);
    }

    public DeviceMode WriteMode(string rawDeviceNode, DeviceMode targetMode)
    {
        if (targetMode == DeviceMode.Unknown)
        {
            throw new InvalidOperationException("Cannot write an unknown mode.");
        }

        var buffer = ReadSector(rawDeviceNode, FileAccess.ReadWrite);
        var currentMode = GetMode(buffer);

        if (currentMode == targetMode)
        {
            return currentMode;
        }

        ApplyMode(buffer, targetMode);

        using var stream = OpenDevice(rawDeviceNode, FileAccess.ReadWrite);
        stream.Position = 0;
        stream.Write(buffer);
        stream.Flush(flushToDisk: true);

        return targetMode;
    }

    private static byte[] ReadSector(string rawDeviceNode, FileAccess access)
    {
        using var stream = OpenDevice(rawDeviceNode, access);
        var buffer = new byte[SectorSize];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static FileStream OpenDevice(string rawDeviceNode, FileAccess access)
    {
        return new FileStream(
            rawDeviceNode,
            FileMode.Open,
            access,
            FileShare.ReadWrite);
    }

    private static DeviceMode GetMode(byte[] buffer)
    {
        if (buffer.Length < SectorSize)
        {
            throw new InvalidDataException("The raw disk read returned fewer than 512 bytes.");
        }

        if (buffer.AsSpan(SignatureOffset, 2).SequenceEqual(XboxSignature))
        {
            return DeviceMode.Xbox;
        }

        if (buffer.AsSpan(SignatureOffset, 2).SequenceEqual(PcSignature))
        {
            for (var index = 0; index < 0x1B8; index++)
            {
                if (buffer[index] != 0x00)
                {
                    return DeviceMode.Unknown;
                }
            }

            return DeviceMode.Pc;
        }

        return DeviceMode.Unknown;
    }

    private static void ApplyMode(byte[] buffer, DeviceMode targetMode)
    {
        switch (targetMode)
        {
            case DeviceMode.Xbox:
                XboxSignature.CopyTo(buffer.AsSpan(SignatureOffset, 2));
                break;
            case DeviceMode.Pc:
                PcSignature.CopyTo(buffer.AsSpan(SignatureOffset, 2));
                break;
            default:
                throw new InvalidOperationException("Cannot apply an unknown mode.");
        }
    }
}
