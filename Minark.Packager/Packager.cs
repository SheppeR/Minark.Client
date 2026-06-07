using System.Security.Cryptography;
using System.Text.Json;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Minark.Packager;

/// <summary>
///     Génère un game-manifest.json + une archive .tar.gz par fichier
///     à partir d'un dossier de build Unity.
///     Incrémental : ne re-archive que les fichiers nouveaux ou modifiés.
/// </summary>
public static class Packager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static async Task RunAsync(
        string sourceDir,
        string outputDir,
        string version,
        IProgress<PackagerProgress>? progress = null)
    {
        sourceDir = Path.GetFullPath(sourceDir);
        outputDir = Path.GetFullPath(outputDir);

        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source introuvable : {sourceDir}");
        }

        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"  Source  : {sourceDir}");
        Console.WriteLine($"  Output  : {outputDir}");
        Console.WriteLine($"  Version : {version}");
        Console.WriteLine();

        // 1. Charger l'ancien manifeste s'il existe (pour diff incrémental)
        var oldManifestPath = Path.Combine(outputDir, "game-manifest.json");
        var oldEntries = LoadOldManifest(oldManifestPath);
        Console.WriteLine(oldEntries.Count > 0 ? $"  Manifeste existant chargé — {oldEntries.Count} entrée(s) connues." : "  Aucun manifeste existant — packaging complet.");

        Console.WriteLine();

        // 2. Lister tous les fichiers source
        var allFiles = Directory
            .GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                if (f.StartsWith(outputDir + Path.DirectorySeparatorChar))
                {
                    return false;
                }

                if (f.StartsWith(outputDir + "/"))
                {
                    return false;
                }

                var name = Path.GetFileName(f);
                if (name.Equals("Minark.Packager.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (name.Equals("Minark.Packager", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            })
            .OrderBy(f => f)
            .ToList();

        Console.WriteLine($"  {allFiles.Count} fichier(s) trouvé(s) dans la source.");

        // 3. Calculer les hashes + archiver uniquement ce qui a changé
        var entries = new GameFileEntry[allFiles.Count];
        var done = 0;
        int skipped = 0, repackaged = 0, added = 0;

        await Parallel.ForEachAsync(
            allFiles.Select((f, i) => (f, i)),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (item, _) =>
            {
                var (file, idx) = item;
                var relativePath = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
                var hash = await ComputeSha256Async(file);
                var size = new FileInfo(file).Length;
                var archiveName = ToArchiveName(relativePath);
                var archivePath = Path.Combine(outputDir, archiveName);

                bool needsRepack;
                string reason;

                if (!oldEntries.TryGetValue(relativePath, out var oldEntry))
                {
                    // Fichier nouveau
                    needsRepack = true;
                    reason = "nouveau";
                    Interlocked.Increment(ref added);
                }
                else if (oldEntry.Sha256 != hash)
                {
                    // Fichier modifié
                    needsRepack = true;
                    reason = "modifié";
                    Interlocked.Increment(ref repackaged);
                }
                else if (!File.Exists(archivePath))
                {
                    // Archive manquante sur disque malgré hash identique
                    needsRepack = true;
                    reason = "archive manquante";
                    Interlocked.Increment(ref repackaged);
                }
                else
                {
                    // Inchangé → on réutilise l'archive existante
                    needsRepack = false;
                    reason = "inchangé";
                    Interlocked.Increment(ref skipped);
                }

                if (needsRepack)
                {
                    CreateTarGz(archivePath, file, relativePath);
                }

                entries[idx] = new GameFileEntry
                {
                    Path = relativePath,
                    Sha256 = hash,
                    Size = size,
                    Archive = archiveName
                };

                var current = Interlocked.Increment(ref done);
                progress?.Report(new PackagerProgress(current, allFiles.Count, relativePath, reason, needsRepack));
            });

        // 4. Supprimer les archives orphelines (fichiers supprimés du build)
        var expectedArchives = entries.Select(e => e.Archive).ToHashSet(StringComparer.OrdinalIgnoreCase);
        expectedArchives.Add("game-manifest.json");
        var orphanCount = 0;
        foreach (var file in Directory.GetFiles(outputDir))
        {
            var name = Path.GetFileName(file);
            if (!expectedArchives.Contains(name))
            {
                File.Delete(file);
                orphanCount++;
                Console.WriteLine($"\n  🗑  Archive orpheline supprimée : {name}");
            }
        }

        // 5. Écrire le manifeste
        var manifest = new GameManifest
        {
            Version = version,
            PublishedAt = DateTime.UtcNow,
            Files = [.. entries]
        };

        var manifestPath = Path.Combine(outputDir, "game-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));

        // 6. Résumé
        Console.WriteLine();
        Console.WriteLine("  ┌─────────────────────────────────┐");
        Console.WriteLine("  │  Résumé du packaging            │");
        Console.WriteLine("  ├─────────────────────────────────┤");
        Console.WriteLine($"  │  ✅ Inchangés  (réutilisés) : {skipped,4} │");
        Console.WriteLine($"  │  🔄 Modifiés  (re-archivés) : {repackaged,4} │");
        Console.WriteLine($"  │  ➕ Nouveaux  (archivés)    : {added,4} │");
        Console.WriteLine($"  │  🗑  Orphelins (supprimés)   : {orphanCount,4} │");
        Console.WriteLine("  └─────────────────────────────────┘");
        Console.WriteLine($"  Manifeste : {manifestPath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Charge le manifeste existant et retourne un dict path → entry.</summary>
    private static Dictionary<string, GameFileEntry> LoadOldManifest(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                return [];
            }

            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<GameManifest>(json, JsonOpts);
            return manifest?.Files.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void CreateTarGz(string archivePath, string sourceFile, string entryName)
    {
        using var outStream = File.Create(archivePath);
        using var writer = WriterFactory.OpenWriter(outStream, ArchiveType.Tar, new WriterOptions(CompressionType.GZip));
        writer.Write(entryName, sourceFile);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var bytes = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>"MyGame_Data/level1" → "MyGame_Data__level1.tar.gz"</summary>
    private static string ToArchiveName(string relativePath)
    {
        return relativePath.Replace('/', '_').Replace('\\', '_') + ".tar.gz";
    }
}

public record PackagerProgress(int Done, int Total, string CurrentFile, string Reason, bool WasRepackaged)
{
    public double Percent => Total == 0 ? 0 : (double)Done / Total * 100;
}