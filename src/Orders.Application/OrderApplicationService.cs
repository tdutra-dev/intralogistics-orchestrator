using BuildingBlocks;
using Orders.Domain;

namespace Orders.Application;

public sealed class OrderApplicationService
{
    public Result<CustomerOrder> CreateOrder(string customerId, string destinationRegion, string dockPreference, params CustomerOrderLine[] lines)
    {
        if (string.IsNullOrWhiteSpace(customerId)) return Result<CustomerOrder>.Failure("Customer id is required.");
        if (string.IsNullOrWhiteSpace(destinationRegion)) return Result<CustomerOrder>.Failure("Destination region is required.");
        if (string.IsNullOrWhiteSpace(dockPreference)) return Result<CustomerOrder>.Failure("Dock preference is required.");
        if (lines is null || lines.Length == 0) return Result<CustomerOrder>.Failure("At least one order line is required.");

        var order = new CustomerOrder(Guid.NewGuid(), customerId, destinationRegion, dockPreference);
        foreach (var line in lines)
        {
            order.AddLine(line.Sku, line.Quantity, line.WeightKg);
        }

        order.SetPriority(OrderPriority.Normal);

        if (!order.IsValid)
        {
            return Result<CustomerOrder>.Failure("Order is invalid.");
        }

        return Result<CustomerOrder>.Success(order);
    }

    public Result<Pallet> SplitIntoPallet(CustomerOrder order)
    {
        if (order is null) return Result<Pallet>.Failure("Order is required.");

        var pallet = new Pallet(Guid.NewGuid(), order.Id, order.TotalWeight);
        pallet.SetRoute(order.DockPreference);
        return Result<Pallet>.Success(pallet);
    }
}
