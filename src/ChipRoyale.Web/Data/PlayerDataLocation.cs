using Microsoft.Data.Sqlite;

namespace ChipRoyale.Web.Data;

/// <summary>
/// Keeps the SQLite database (accounts, chips, crowns) outside the program folder, so replacing
/// the game with a newer version does not delete it. Default: the user's local app data folder
/// (Windows: %LOCALAPPDATA%\ChipRoyale). Override with the setting Storage:DataDirectory.
/// </summary>
public static class PlayerDataLocation
{
    public static string Resolve(string connectionString, string? configuredDirectory, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrEmpty(builder.DataSource) || builder.DataSource == ":memory:" || Path.IsPathRooted(builder.DataSource))
            return connectionString;

        var directory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "ChipRoyale")
            : configuredDirectory;
        Directory.CreateDirectory(directory);

        var target = Path.Combine(directory, Path.GetFileName(builder.DataSource));
        var old = Path.GetFullPath(builder.DataSource, contentRoot);

        // First start after the move: take over the database from the old place next to the program.
        if (!File.Exists(target) && File.Exists(old))
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(old + suffix))
                    File.Copy(old + suffix, target + suffix);
        }

        builder.DataSource = target;
        return builder.ToString();
    }
}
