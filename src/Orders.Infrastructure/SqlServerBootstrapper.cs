using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure;

public static class SqlServerBootstrapper
{
    public static Task EnsureReportingProcedureAsync(OrderDbContext dbContext, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE OR ALTER PROCEDURE dbo.sp_ThroughputReport
            AS
            BEGIN
                SET NOCOUNT ON;

                SELECT
                    COUNT(1) AS TotalOrders,
                    AVG(CAST(TotalWeight AS decimal(18,2))) AS AverageOrderWeightKg,
                    SUM(CASE WHEN [Status] = 3 THEN 1 ELSE 0 END) AS CancelledOrders
                FROM (
                    SELECT
                        o.Id,
                        o.[Status],
                        SUM(CAST(line.WeightKg * line.Quantity AS decimal(18,2))) AS TotalWeight
                    FROM dbo.Orders o
                    LEFT JOIN dbo.OrderLines line ON line.OrderId = o.Id
                    GROUP BY o.Id, o.[Status]
                ) source;
            END;
            """;

        return dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
