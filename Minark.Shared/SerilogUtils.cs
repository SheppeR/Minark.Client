using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Minark.Shared;

public static class SerilogUtils
{
    public static Logger SetupClient(string? logDirectory = null)
    {
        // Scoper le fichier par PID : si plusieurs clients tournent sur la même machine
        // (un par compte), chacun a son propre log au lieu d'écrire en concurrence dans
        // un seul fichier partagé, ce qui rend le debug impossible.
        var pid = Environment.ProcessId;
        var logPath = string.IsNullOrWhiteSpace(logDirectory)
            ? $"Logs/Client-{pid}.log"
            : Path.Combine(logDirectory, $"Client-{pid}.log");

        return new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Fatal)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Fatal)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Pid", pid)
            .WriteTo.Console(
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [pid:{Pid}] {Message:lj}{NewLine}{Exception}",
                theme: Theme())
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 2,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    public static Logger SetupServer()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Fatal)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Fatal)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: Theme())
            .WriteTo.File(
                "Logs/Server.log",
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 2,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    public static SystemConsoleTheme Theme()
    {
        return new SystemConsoleTheme(new Dictionary<ConsoleThemeStyle, SystemConsoleThemeStyle>
        {
            // Texte normal [INF] → blanc (plus de vert)
            [ConsoleThemeStyle.Text] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.SecondaryText] = new() { Foreground = ConsoleColor.Gray },
            [ConsoleThemeStyle.String] = new() { Foreground = ConsoleColor.Yellow },
            [ConsoleThemeStyle.Number] = new() { Foreground = ConsoleColor.Cyan },
            [ConsoleThemeStyle.Boolean] = new() { Foreground = ConsoleColor.Red },
            [ConsoleThemeStyle.Scalar] = new() { Foreground = ConsoleColor.White },
            [ConsoleThemeStyle.Null] = new() { Foreground = ConsoleColor.Black, Background = ConsoleColor.Yellow },
            [ConsoleThemeStyle.Name] = new() { Foreground = ConsoleColor.Black, Background = ConsoleColor.Yellow },
            // Niveaux de log
            [ConsoleThemeStyle.LevelVerbose] = new() { Foreground = ConsoleColor.DarkGray },
            [ConsoleThemeStyle.LevelDebug] = new() { Foreground = ConsoleColor.DarkCyan },
            [ConsoleThemeStyle.LevelInformation] = new() { Foreground = ConsoleColor.Cyan },
            [ConsoleThemeStyle.LevelWarning] = new()
                { Foreground = ConsoleColor.Magenta, Background = ConsoleColor.Yellow },
            [ConsoleThemeStyle.LevelError] = new() { Foreground = ConsoleColor.White, Background = ConsoleColor.Red },
            [ConsoleThemeStyle.LevelFatal] = new() { Foreground = ConsoleColor.Black, Background = ConsoleColor.Cyan }
        });
    }

    /// <summary>
    ///     Affiche une section bien visible en jaune dans la console.
    ///     Ex : ===============================-[ DATABASE ]
    /// </summary>
    public static void PrintSection(string section)
    {
        section = "-[ " + section + " ]";
        while (section.Length < 79)
        {
            section = "=" + section;
        }

        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(section);
        Console.ForegroundColor = prev;
    }
}