using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Orders.Dal.Database;

public sealed partial class DatabaseInitializer
{
    private static readonly string[] ScriptOrder = ["schema.sql", "procedures.sql", "seed.sql"];

    [GeneratedRegex(@"^[ \t]*GO[ \t]*(?:--[^\r\n]*)?\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BatchSeparator();

    private readonly string _connectionString;
    private readonly string _scriptsDirectory;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string connectionString, string scriptsDirectory, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = connectionString;
        _scriptsDirectory = scriptsDirectory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenWithRetryAsync(ct);

        if (await SchemaExistsAsync(connection, ct))
        {
            _logger.LogInformation("Database schema already exists - initialization skipped.");
            return;
        }

        foreach (var fileName in ScriptOrder)
        {
            var path = Path.Combine(_scriptsDirectory, fileName);
            if (!File.Exists(path))
                throw new FileNotFoundException($"SQL script '{fileName}' was not found.", path);

            var script = await File.ReadAllTextAsync(path, ct);
            var batches = BatchSeparator().Split(script)
                .Where(batch => !string.IsNullOrWhiteSpace(batch))
                .ToList();

            _logger.LogInformation("Running {Script} ({Count} batches)", fileName, batches.Count);

            foreach (var batch in batches)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        _logger.LogInformation("Database initialized.");
    }

    private static async Task<bool> SchemaExistsAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @Name";
        command.Parameters.Add(new SqlParameter("@Name", System.Data.SqlDbType.NVarChar, 128) { Value = "Orders" });
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    private async Task<SqlConnection> OpenWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(ct);
                return connection;
            }
            catch (SqlException ex) when (attempt < maxAttempts)
            {
                await connection.DisposeAsync();
                _logger.LogWarning("SQL Server is not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }
}
