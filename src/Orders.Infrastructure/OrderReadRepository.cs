using Dapper;
using Microsoft.Data.SqlClient;

namespace Orders.Infrastructure;

public sealed record OrderListItem(Guid Id, string CustomerId, string Status, string Priority, DateTime CreatedAtUtc);
public sealed record ThroughputReportResponse(int TotalOrders, decimal AverageOrderWeightKg, int CancelledOrders);

public sealed class OrderReadRepository
{
    private readonly string _connectionString;

    public OrderReadRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<OrderListItem>> GetOrderSummariesAsync(
        string? status,
        string? priority,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                Id,
                CustomerId,
                CAST(Status AS NVARCHAR(32)) AS Status,
                CAST(Priority AS NVARCHAR(32)) AS Priority,
                CreatedAtUtc
            FROM dbo.Orders
            WHERE (@status IS NULL OR CAST(Status AS NVARCHAR(32)) = @status)
              AND (@priority IS NULL OR CAST(Priority AS NVARCHAR(32)) = @priority)
              AND (@from IS NULL OR CreatedAtUtc >= @from)
              AND (@to IS NULL OR CreatedAtUtc <= @to)
            ORDER BY CreatedAtUtc DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        var offset = (page - 1) * pageSize;
        return await connection.QueryAsync<OrderListItem>(new CommandDefinition(
            sql,
            new
            {
                status,
                priority,
                from = from?.UtcDateTime,
                to = to?.UtcDateTime,
                offset,
                pageSize
            },
            cancellationToken: cancellationToken));
    }

    public async Task<ThroughputReportResponse> GetThroughputReportAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                COUNT(1) AS TotalOrders,
                AVG(CAST([TotalWeight] AS decimal(18,2))) AS AverageOrderWeightKg,
                SUM(CASE WHEN CAST([Status] AS NVARCHAR(32)) = 'Cancelled' THEN 1 ELSE 0 END) AS CancelledOrders
            FROM (
                SELECT
                    o.[Id],
                    o.[Status],
                    (
                        SELECT SUM(CAST(line.WeightKg * line.Quantity AS decimal(18,2)))
                        FROM OPENJSON(o.[Lines])
                        WITH (
                            Sku nvarchar(64) '$.sku',
                            Quantity int '$.quantity',
                            WeightKg decimal(18,2) '$.weightKg'
                        ) AS line
                    ) AS TotalWeight
                FROM dbo.Orders o
            ) source;
            """;

        var result = await connection.QuerySingleAsync<ThroughputReportResponse>(new CommandDefinition(
            sql,
            cancellationToken: cancellationToken));

        return result;
    }
}
