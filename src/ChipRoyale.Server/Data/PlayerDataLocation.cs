using Microsoft.Data.Sqlite;

namespace ChipRoyale.Server.Data;

/// <summary>
/// Where the SQLite database (accounts, chips, crowns, items) lives. Default: the folder
/// "Datenbank" next to the server (setting Storage:DataDirectory; relative paths are relative to
/// the server folder). On the first start an older database is taken over: from "Data\app.db"
/// next to the program or from the previous default %LOCALAPPDATA%\ChipRoyale.
/// A database shipped as an empty template (marker file <see cref="TemplateMarker"/> next to it)
/// counts as "not there yet", so an older database still wins.
/// </summary>
public static class PlayerDataLocation
{
    public const string TemplateMarker = "leere-vorlage.txt";

    public static (string ConnectionString, string Directory) Resolve(string connectionString, string? configuredDirectory, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(contentRoot, "Datenbank")
            : Path.GetFullPath(configuredDirectory, contentRoot);
        Directory.CreateDirectory(directory);

        if (string.IsNullOrEmpty(builder.DataSource) || builder.DataSource == ":memory:" || Path.IsPathRooted(builder.DataSource))
            return (connectionString, directory);

        var fileName = Path.GetFileName(builder.DataSource);
        var target = Path.Combine(directory, fileName);
        var marker = Path.Combine(directory, TemplateMarker);
        var isTemplate = File.Exists(marker);
        if (!File.Exists(target) || isTemplate)
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine("Data", fileName), contentRoot),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChipRoyale", fileName),
            };
            if (candidates.FirstOrDefault(File.Exists) is { } old)
            {
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                {
                    if (File.Exists(target + suffix)) File.Delete(target + suffix);
                    if (File.Exists(old + suffix)) File.Copy(old + suffix, target + suffix);
                }
            }
            if (isTemplate) File.Delete(marker);
        }

        builder.DataSource = target;
        return (builder.ToString(), directory);
    }

    /// <summary>
    /// Copies the database into "Sicherungen" next to it before the server opens it (one copy per
    /// start, the newest <paramref name="keep"/> are kept). Protects accounts against a database file
    /// that gets overwritten or broken.
    /// </summary>
    /// <returns>The backup file, or null if there was nothing to back up.</returns>
    public static string? Backup(string connectionString, int keep = 10)
    {
        var source = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(source) || source == ":memory:" || !File.Exists(source) || new FileInfo(source).Length == 0) return null;

        var folder = Path.Combine(Path.GetDirectoryName(source)!, "Sicherungen");
        Directory.CreateDirectory(folder);
        var name = Path.GetFileNameWithoutExtension(source);
        var target = Path.Combine(folder, $"{name}-{DateTime.Now:yyyy-MM-dd-HHmmss}.db");
        // The server has not opened the database yet, so the files are consistent; a -wal file holds
        // changes not yet written into the main file and is copied along.
        File.Copy(source, target, overwrite: true);
        if (File.Exists(source + "-wal")) File.Copy(source + "-wal", target + "-wal", overwrite: true);

        foreach (var old in Directory.GetFiles(folder, $"{name}-*.db").OrderDescending().Skip(keep))
        {
            File.Delete(old);
            if (File.Exists(old + "-wal")) File.Delete(old + "-wal");
        }
        return target;
    }
}
