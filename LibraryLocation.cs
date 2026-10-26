using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace NorthIcons;

public static class LibraryLocation
{
    static readonly string cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "northIcons", "Library");
    static readonly Lazy<string> bundlePath = new(() =>
    {
        if (!File.Exists(Archive)) return Path.Combine(cacheRoot, "missing");
        using var stream = File.OpenRead(Archive);
        return Path.Combine(cacheRoot, Convert.ToHexString(SHA256.HashData(stream))[..20]);
    });
    public static string Archive => Path.Combine(AppContext.BaseDirectory, "Icons.zip");
    public static string Bundled => bundlePath.Value;
    public static string Resolve(string? savedFolder)
    {
        var legacyDefault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "icons lib");
        var legacyBundled = Path.Combine(AppContext.BaseDirectory, "Icons");
        if (!string.IsNullOrWhiteSpace(savedFolder) &&
            !string.Equals(savedFolder.TrimEnd(Path.DirectorySeparatorChar), legacyDefault.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(savedFolder.TrimEnd(Path.DirectorySeparatorChar), legacyBundled, StringComparison.OrdinalIgnoreCase) &&
            !savedFolder.StartsWith(cacheRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(savedFolder)) return savedFolder;
        return Bundled;
    }
    public static void EnsureAvailable(string folder)
    {
        if (string.Equals(folder, Bundled, StringComparison.OrdinalIgnoreCase)) Extract(Archive, folder);
    }
    public static void Extract(string archive, string destination)
    {
        destination = Path.GetFullPath(destination);
        if (File.Exists(Path.Combine(destination, ".complete"))) return;
        if (!File.Exists(archive)) throw new FileNotFoundException("Icons.zip is missing. Keep it beside northIcons.exe.", archive);
        if (Directory.Exists(destination)) throw new IOException("The icon cache is incomplete. Remove that cache folder and restart northIcons: " + destination);
        var stage = destination + ".partial-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(stage);
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            long total = 0; int count = 0;
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;
                if (name.Contains('/') || name.Contains('\\') || name.Contains(':') || name is "." or ".." ||
                    !(name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || name == "sources.tsv"))
                    throw new InvalidDataException("Unexpected file in icon archive.");
                total += entry.Length;
                if (total > 512L * 1024 * 1024 || ++count > 200000) throw new InvalidDataException("Icon archive exceeds supported limits.");
                entry.ExtractToFile(Path.Combine(stage, name));
            }
            if (count == 0 || !File.Exists(Path.Combine(stage, "sources.tsv"))) throw new InvalidDataException("Icon archive is incomplete.");
            File.WriteAllText(Path.Combine(stage, ".complete"), count.ToString());
            try { Directory.Move(stage, destination); }
            catch (IOException) when (File.Exists(Path.Combine(destination, ".complete"))) { }
        }
        finally
        {
            // Remove only the staging directory created above, inside the destination's parent.
            if (Directory.Exists(stage) && Path.GetDirectoryName(stage) == Path.GetDirectoryName(destination) &&
                stage.StartsWith(destination + ".partial-", StringComparison.Ordinal)) Directory.Delete(stage, true);
        }
    }
}
