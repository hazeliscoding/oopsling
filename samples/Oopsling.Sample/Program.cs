using Avalonia;

namespace Oopsling.Sample;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashReports.Start("Oopsling Sample", "hazeliscoding/oopsling");
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
