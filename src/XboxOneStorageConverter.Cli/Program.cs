namespace XboxOneStorageConverter.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var application = new CliApplication(Console.Out, Console.Error);
        return application.Run(args);
    }
}
