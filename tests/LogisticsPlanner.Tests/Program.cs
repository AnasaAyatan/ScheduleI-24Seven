using ScheduleI24Seven.Logistics;
using static ScheduleI24Seven.Logistics.LogisticsPlanner;

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var source = new Stock("Warehouse", "soil", 100, 40, 40, 100, 10m);
var destination = new Stock("Lab", "soil", 0, 50, 0, 50, 10m);
var plan = Create(new[] { source, destination }, 1000m, 100m);
Assert(plan.Transfers.Count == 1 && plan.Transfers[0].Quantity == 50,
    "Transfer should fill the target before purchasing.");
Assert(plan.Purchases.Count == 0, "No purchase when surplus covers demand.");

plan = Create(new[] {
    source with { Quantity = 45 },
    destination
}, 1000m, 900m);
Assert(plan.Transfers.Single().Quantity == 5, "Protect source stock.");
Assert(plan.Purchases.Single().Quantity == 10, "Respect cash reserve and price.");
Assert(plan.EstimatedPurchaseCost == 100m, "Total purchase expense.");

plan = Create(new[] { destination with { FreeCapacity = 8 } }, 1000m, 0m);
Assert(plan.Purchases.Single().Quantity == 8, "Respect destination capacity.");

plan = Create(new[] { source, destination }, 1000m, 0m, transfersEnabled:false);
Assert(plan.Transfers.Count == 0 && plan.Purchases.Single().Quantity == 50,
    "Disable transfers independently.");

plan = Create(new[] { source, destination }, 1000m, 0m, purchasesEnabled:false);
Assert(plan.Purchases.Count == 0, "Disable purchases independently.");

try {
    Create(new[] { destination, destination }, 100m, 0m);
    throw new Exception("Duplicate stock rows accepted.");
} catch (ArgumentException) { }

Console.WriteLine("All logistics planner tests passed.");
