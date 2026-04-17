using System.Globalization;

namespace XboxOneStorageConverter.Cli;

internal sealed class CliApplication
{
    private readonly TextWriter _standardOutput;
    private readonly TextWriter _standardError;

    public CliApplication(TextWriter standardOutput, TextWriter standardError)
    {
        _standardOutput = standardOutput;
        _standardError = standardError;
    }

    public int Run(string[] args)
    {
        if (!OperatingSystem.IsMacOS())
        {
            _standardError.WriteLine("This port currently supports macOS only.");
            return 1;
        }

        var diskService = new MacDiskService(new DiskUtilProcessRunner(), new MbrDeviceModeService());
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "scan";

        try
        {
            return command switch
            {
                "scan" => RunScan(diskService),
                "inspect" => RunInspect(diskService, args),
                "set-mode" => RunSetMode(diskService, args),
                "prepare" => RunPrepare(),
                "help" or "--help" or "-h" => RunHelp(),
                _ => RunUnknownCommand(command),
            };
        }
        catch (Exception exception)
        {
            _standardError.WriteLine(exception.Message);
            return 1;
        }
    }

    private int RunScan(MacDiskService diskService)
    {
        var devices = diskService.ListCandidateDisks();
        if (devices.Count == 0)
        {
            _standardOutput.WriteLine("No external physical disks were found.");
            return 0;
        }

        _standardOutput.WriteLine("Identifier  Mode         Size      Bus   Name");
        foreach (var device in devices)
        {
            _standardOutput.WriteLine(
                $"{device.Identifier,-10}  {device.ModeDisplayName,-11}  {FormatBytes(device.SizeBytes),-8}  {device.BusProtocol,-4}  {device.MediaName}");
        }

        _standardOutput.WriteLine();
        _standardOutput.WriteLine("Use 'inspect <disk>' for volume details.");
        _standardOutput.WriteLine("Use 'sudo ... set-mode <disk> xbox|pc' when you need to modify a disk.");
        return 0;
    }

    private int RunInspect(MacDiskService diskService, string[] args)
    {
        if (args.Length < 2)
        {
            _standardError.WriteLine("Usage: inspect <disk>");
            return 1;
        }

        var device = diskService.GetCandidateDisk(args[1]);

        _standardOutput.WriteLine($"Identifier: {device.Identifier}");
        _standardOutput.WriteLine($"Device Node: {device.DeviceNode}");
        _standardOutput.WriteLine($"Raw Device Node: {device.RawDeviceNode}");
        _standardOutput.WriteLine($"Mode: {device.ModeDisplayName}");
        _standardOutput.WriteLine($"Name: {device.MediaName}");
        _standardOutput.WriteLine($"Bus: {device.BusProtocol}");
        _standardOutput.WriteLine($"Size: {FormatBytes(device.SizeBytes)} ({device.SizeBytes.ToString(CultureInfo.InvariantCulture)} bytes)");
        _standardOutput.WriteLine($"Mounted Volumes: {(device.VolumeNames.Count == 0 ? "none" : string.Join(", ", device.VolumeNames))}");

        if (!string.IsNullOrWhiteSpace(device.ModeWarning))
        {
            _standardOutput.WriteLine($"Mode Note: {device.ModeWarning}");
        }

        return 0;
    }

    private int RunSetMode(MacDiskService diskService, string[] args)
    {
        if (args.Length < 3)
        {
            _standardError.WriteLine("Usage: set-mode <disk> xbox|pc");
            return 1;
        }

        if (!TryParseMode(args[2], out var targetMode))
        {
            _standardError.WriteLine("Mode must be 'xbox' or 'pc'.");
            return 1;
        }

        var result = diskService.SetMode(args[1], targetMode);

        _standardOutput.WriteLine(
            $"{result.DeviceIdentifier} is now in {result.NewModeDisplayName} mode.");

        if (!string.IsNullOrWhiteSpace(result.PostActionMessage))
        {
            _standardOutput.WriteLine(result.PostActionMessage);
        }

        return 0;
    }

    private int RunPrepare()
    {
        _standardError.WriteLine("Drive formatting is not implemented on macOS.");
        _standardError.WriteLine("Prepare the disk separately, then use 'set-mode <disk> xbox'.");
        return 1;
    }

    private int RunHelp()
    {
        _standardOutput.WriteLine("xbox-storage-converter");
        _standardOutput.WriteLine();
        _standardOutput.WriteLine("Commands:");
        _standardOutput.WriteLine("  scan                    List external physical disks and detect their MBR mode.");
        _standardOutput.WriteLine("  inspect <disk>          Show detailed information for one disk.");
        _standardOutput.WriteLine("  set-mode <disk> <mode>  Toggle a disk between xbox and pc mode.");
        _standardOutput.WriteLine("  prepare                 Explain the missing macOS formatting step.");
        _standardOutput.WriteLine();
        _standardOutput.WriteLine("Examples:");
        _standardOutput.WriteLine("  dotnet run --project src/XboxOneStorageConverter.Cli -- scan");
        _standardOutput.WriteLine("  sudo dotnet run --project src/XboxOneStorageConverter.Cli -- inspect disk4");
        _standardOutput.WriteLine("  sudo dotnet run --project src/XboxOneStorageConverter.Cli -- set-mode disk4 xbox");
        return 0;
    }

    private int RunUnknownCommand(string command)
    {
        _standardError.WriteLine($"Unknown command '{command}'.");
        _standardError.WriteLine("Run 'help' for usage.");
        return 1;
    }

    private static bool TryParseMode(string value, out DeviceMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "xbox":
                mode = DeviceMode.Xbox;
                return true;
            case "pc":
                mode = DeviceMode.Pc;
                return true;
            default:
                mode = DeviceMode.Unknown;
                return false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}
