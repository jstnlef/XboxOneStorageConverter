using System.Diagnostics;
using System.Text;

namespace XboxOneStorageConverter.Cli;

internal sealed class DiskUtilProcessRunner
{
    public string Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/sbin/diskutil",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start diskutil.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(standardError)
                ? $"diskutil exited with code {process.ExitCode}."
                : standardError.Trim();

            throw new InvalidOperationException(message);
        }

        return standardOutput;
    }
}
