using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleI24Seven.Logistics;
using static ScheduleI24Seven.Logistics.UnifiedLogisticsPlanner;

static void Check(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException(message);
}
static Slot Row(string property, string locker, string item, string variant,
    ItemKind kind, int quantity, int target, int protectedAmount, int capacity,
    decimal price = 0, bool source = true, bool destination = true)
    => new(property, locker, item, variant, kind, quantity, target, protectedAmount,
        capacity, price, source, destination);

var source = Row("Farm", "Locker1", "coco", "heavenly", ItemKind.Produced, 100, 20, 20, 0);
var destination = Row("Shop", "Locker2", "coco", "heavenly", ItemKind.Produced, 0, 50, 0, 50);
var plan = Create(new[] { source, destination }, 1000m, 100m);
Check(plan.Moves.Count == 1 && plan.Moves[0].Quantity == 50, "Move 50 finished products.");
Check(plan.Buys.Count == 0, "Never purchase produced goods.");
Check(plan.Moves[0].SourceContainer == "Locker1" &&
      plan.Moves[0].DestinationContainer == "Locker2", "Preserve locker identity.");

plan = Create(new[] { source, destination with { VariantKey = "normal" } }, 1000m, 0m);
Check(plan.Moves.Count == 0, "Never substitute another quality variant.");

var ingredient = Row("Shop", "Supplies", "cuke", "standard", ItemKind.Purchasable,
    0, 40, 0, 40, 5m);
plan = Create(new[] { ingredient }, 100m, 25m);
Check(plan.Moves.Count == 0 && plan.Buys.Single().Quantity == 15,
    "Purchasable input uses budgeted restocking.");
Check(plan.EstimatedTotalCost == 75m, "Respect reserve.");

plan = Create(new[] { source, destination with { FreeCapacity = 5 } }, 0m, 0m);
Check(plan.Moves.Single().Quantity == 5, "Respect destination capacity.");

plan = Create(new[] { source with { Quantity = 25 }, destination }, 0m, 0m);
Check(plan.Moves.Single().Quantity == 5, "Respect source protected stock.");

plan = Create(new[] { source, destination }, 0m, 0m, autoTransport:false);
Check(plan.Moves.Count == 0, "Transport toggle.");

plan = Create(new[] { ingredient }, 100m, 0m, autoRestock:false);
Check(plan.Buys.Count == 0, "Restock toggle.");

plan = Create(new[] { ingredient with { Kind = ItemKind.Unknown } }, 100m, 0m);
Check(plan.Buys.Count == 0 && plan.Moves.Count == 0, "Unknown type fails closed.");

plan = Create(new[] { source, destination with { Kind = ItemKind.Purchasable } }, 100m, 0m);
Check(plan.Moves.Count == 0 && plan.Buys.Count == 0,
    "Conflicting classification fails closed.");

try
{
    Create(new[] { source, source }, 100m, 0m);
    throw new InvalidOperationException("Duplicate container entry accepted.");
}
catch (ArgumentException) { }

Console.WriteLine("Unified logistics planner tests passed.");
