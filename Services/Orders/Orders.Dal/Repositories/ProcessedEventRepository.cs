using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Abstractions;
using Orders.Dal.Infrastructure;

namespace Orders.Dal.Repositories;

public sealed class ProcessedEventRepository(IDbSession session) : IProcessedEventRepository
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    /// <summary>Той самий скрипт, що й Migrations/002_ProcessedEvents.sql та db/p1/schema.sql.</summary>
    public const string CreateTableSql = """
        IF OBJECT_ID(N'dbo.ProcessedEvents', N'U') IS NULL
            CREATE TABLE dbo.ProcessedEvents (
                EventId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProcessedEvents PRIMARY KEY,
                ProcessedAt DATETIME2        NOT NULL CONSTRAINT DF_ProcessedEvents_ProcessedAt DEFAULT SYSUTCDATETIME()
            );
        """;

    public async Task<bool> TryAddAsync(Guid eventId, CancellationToken ct = default)
    {
        var connection = await session.GetConnectionAsync(ct);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.ProcessedEvents (EventId) VALUES (@EventId)",
                new { EventId = eventId }, session.Transaction, cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is PrimaryKeyViolation or UniqueIndexViolation)
        {
            return false;               // уже оброблено
        }
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        var connection = await session.GetConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(CreateTableSql, transaction: session.Transaction, cancellationToken: ct));
    }
}
