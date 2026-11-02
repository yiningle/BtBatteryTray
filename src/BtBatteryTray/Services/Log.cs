namespace BtBatteryTray.Services;

/// <summary>Minimal append-only log in %APPDATA%\BtBatteryTray, which also holds settings.json.</summary>
public static class Log
{
    const long MaxBytes = 1024 * 1024;

    public static readonly string Directory = CreateDirectory();
    static readonly string FilePath = Path.Combine(Directory, "log.txt");
    static readonly Lock Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never take the app down.
            }
        }
    }

    static string CreateDirectory()
    {
        // Not %LOCALAPPDATA%\BtBatteryTray: that is the Velopack install root, which setup wipes on (re)install.
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BtBatteryTray");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }
}
