using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

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
                CASE [Status]
                    WHEN 0 THEN 'Received'
                    WHEN 1 THEN 'Building'
                    WHEN 2 THEN 'Ready'
                    WHEN 3 THEN 'Cancelled'
                    WHEN 4 THEN 'Dispatched'
                    ELSE CAST([Status] AS NVARCHAR(32))
                END AS Status,
                CASE [Priority]
                    WHEN 0 THEN 'Low'
                    WHEN 1 THEN 'Normal'
                    WHEN 2 THEN 'Urgent'
                    ELSE CAST([Priority] AS NVARCHAR(32))
                END AS Priority,
                CreatedAtUtc
            FROM dbo.Orders
            WHERE (@status IS NULL OR CASE [Status]
                    WHEN 0 THEN 'Received'
                    WHEN 1 THEN 'Building'
                    WHEN 2 THEN 'Ready'
                    WHEN 3 THEN 'Cancelled'
                    WHEN 4 THEN 'Dispatched'
                    ELSE CAST([Status] AS NVARCHAR(32))
                END = @status)
              AND (@priority IS NULL OR CASE [Priority]
                    WHEN 0 THEN 'Low'
                    WHEN 1 THEN 'Normal'
                    WHEN 2 THEN 'Urgent'
                    ELSE CAST([Priority] AS NVARCHAR(32))
                END = @priority)
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

        var result = await connection.QuerySingleAsync<ThroughputReportResponse>(new CommandDefinition(
            "dbo.sp_ThroughputReport",
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return result;
    }
}
