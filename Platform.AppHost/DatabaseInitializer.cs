using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

internal static partial class DatabaseInitializer
{
    private static readonly string[] Scripts = ["schema.sql", "procedures.sql", "seed.sql"];

    [GeneratedRegex(@"^[ \t]*GO[ \t]*(?:--[^\r\n]*)?\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BatchSeparator();

    public static async Task ApplyAsync(string connectionString, string scriptsDirectory, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // schema.sql перестворює таблиці, тому на БД з даними (наприклад, зі збереженого volume) скрипти не виконуємо
        if (await SchemaExistsAsync(connection, cancellationToken))
            return;

        foreach (var scriptName in Scripts)
        {
            var script = await File.ReadAllTextAsync(Path.Combine(scriptsDirectory, scriptName), cancellationToken);

            foreach (var batch in BatchSeparator().Split(script))
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    private static async Task<bool> SchemaExistsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @Name";
        command.Parameters.Add(new SqlParameter("@Name", System.Data.SqlDbType.NVarChar, 128) { Value = "Orders" });
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }
}
