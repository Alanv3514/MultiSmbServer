using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace MultiSmbServer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog("AppDomain.UnhandledException", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            WriteCrashLog("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    private static void WriteCrashLog(string source, Exception? exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MultiSmbServer");

            Directory.CreateDirectory(directory);

            File.AppendAllText(
                Path.Combine(directory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // No podemos hacer nada más si falla la escritura del log.
        }
    }
}
