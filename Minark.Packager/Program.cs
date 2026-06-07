using Minark.Packager;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║        Minark Game Packager v1.1         ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine();

var sourceDir = Directory.GetCurrentDirectory();
var outputDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "dist"));
var version = DateTime.UtcNow.ToString("yyyy.MM.dd.HHmm");

try
{
    var progress = new Progress<PackagerProgress>(p =>
    {
        int w = 28, filled = (int)(p.Percent / 100 * w);
        var bar = new string('█', filled) + new string('░', w - filled);
        var name = p.CurrentFile.Length > 40 ? "..." + p.CurrentFile[^37..] : p.CurrentFile.PadRight(40);

        // Couleur selon l'action
        Console.ForegroundColor = p.WasRepackaged ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
        var tag = p.Reason switch
        {
            "nouveau" => "[+]",
            "modifié" => "[~]",
            "archive manquante" => "[!]",
            _ => "[ ]"
        };
        Console.Write($"\r  {tag} [{bar}] {p.Percent:F0}%  {name}");
        Console.ResetColor();
    });

    await Packager.RunAsync(sourceDir, outputDir, version, progress);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n\n  ✔ Terminé — dépose le contenu de dist/ sous /game/ sur ton serveur.");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"\n  ✘ {ex.Message}");
    Console.ResetColor();
}