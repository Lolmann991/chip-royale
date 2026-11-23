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
}
