using System.IO;
using System.Windows;
using System.Windows.Threading;
using LinenLuxe.POS.Services.Configuration;

namespace LinenLuxe.POS;

/// <summary>
/// Application entry point. Loads configuration (Supabase URL/key) once at startup
/// from the .env file and exposes it app-wide via <see cref="Config"/>.
/// </summary>
public partial class App : Application
{
    /// <summary>App-wide configuration, loaded from .env at startup.</summary>
    public static AppConfig Config { get; private set; } = AppConfig.Empty;

    /// <summary>Where the last few faults are written, for when one needs chasing.</summary>
    public static string ErrorLogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LinenLuxe POS", "errors.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Config = AppConfig.Load();
        DispatcherUnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Anything that goes wrong and isn't caught closes a WPF app on the spot. On a
    /// till that is the worst possible answer: a half-typed order disappears and the
    /// customer is still standing there. So the fault is written down, shown plainly,
    /// and the app is kept open — the screen that broke can be left and the order
    /// finished. It is not a substitute for fixing the fault; it is what stands
    /// between a bug and a lost sale.
    /// </summary>
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Log(e.Exception);

        MessageBox.Show(
            "Something went wrong on this screen.\n\n"
            + e.Exception.Message
            + "\n\nThe POS is still running — go back to another screen and carry on. "
            + "If it keeps happening, the details are saved in:\n" + ErrorLogPath,
            "LinenLuxe POS", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            File.AppendAllText(ErrorLogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Failing to write the log must never itself take the app down.
        }
    }
}
