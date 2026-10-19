using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleI24Seven.Logistics;

/// <summary>
/// Pure stock-reconciliation planner. This code NEVER edits game inventory or money.
/// A separate, game-version-verified adapter must validate and commit its proposals.
/// </summary>
public static class LogisticsPlanner
{
    public sealed record Stock(
        string PropertyId,
        string ItemId,
        int Quantity,
        int TargetQuantity,
        int ProtectedQuantity,
        int FreeCapacity,
        decimal UnitPrice);

    public sealed record Transfer(string SourcePropertyId, string DestinationPropertyId,
        string ItemId, int Quantity);
    public sealed record Purchase(string DestinationPropertyId, string ItemId,
        int Quantity, decimal EstimatedCost);
    public sealed record Plan(IReadOnlyList<Transfer> Transfers,
        IReadOnlyList<Purchase> Purchases, decimal EstimatedPurchaseCost);

    /// <summary>
    /// Plan restocking across owned properties. Each input row identifies one
    /// property's stock of one item. Prices are estimates, not validated charges.
    /// There must be at most one row per (PropertyId, ItemId).
    /// </summary>
    public static Plan Create(IEnumerable<Stock> inventory, decimal spendableCash,
        decimal cashReserve, bool transfersEnabled = true, bool purchasesEnabled = true)
    {
        if (inventory == null) throw new ArgumentNullException(nameof(inventory));
        if (spendableCash < 0 || cashReserve < 0)
            throw new ArgumentOutOfRangeException(nameof(spendableCash));
        var rows = inventory.ToArray();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in rows)
        {
            if (string.IsNullOrWhiteSpace(s.PropertyId) || string.IsNullOrWhiteSpace(s.ItemId)
                || s.Quantity < 0 || s.TargetQuantity < 0 || s.ProtectedQuantity < 0
                || s.FreeCapacity < 0 || s.UnitPrice < 0)
                throw new ArgumentException("Invalid inventory row", nameof(inventory));
            if (!used.Add(s.PropertyId + "\u001f" + s.ItemId))
                throw new ArgumentException("Duplicate property/item pair", nameof(inventory));
        }

        var transfers = new List<Transfer>();
        var purchases = new List<Purchase>();
        var remainingBudget = Math.Max(0m, spendableCash - cashReserve);

        foreach (var group in rows.GroupBy(x => x.ItemId, StringComparer.Ordinal)
                                  .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var supply = group.OrderBy(s => s.PropertyId, StringComparer.Ordinal)
                .Select(s => new Source(s.PropertyId,
                    Math.Max(0, s.Quantity - Math.Max(s.TargetQuantity, s.ProtectedQuantity))))
                .ToArray();

            foreach (var dest in group.OrderBy(s => s.PropertyId, StringComparer.Ordinal))
            {
                var need = Math.Min(Math.Max(0, dest.TargetQuantity - dest.Quantity),
                                    dest.FreeCapacity);
                if (need == 0) continue;

                if (transfersEnabled)
                {
                    foreach (var source in supply)
                    {
                        if (source.PropertyId == dest.PropertyId || source.Available == 0) continue;
                        int take = Math.Min(need, source.Available);
                        if (take == 0) continue;
                        transfers.Add(new Transfer(source.PropertyId, dest.PropertyId,
                            dest.ItemId, take));
                        source.Available -= take;
                        need -= take;
                        if (need == 0) break;
                    }
                }
                if (!purchasesEnabled || need == 0 || dest.UnitPrice <= 0) continue;
                int affordable = (int)Math.Min(need,
                    Math.Min(int.MaxValue, decimal.Floor(remainingBudget / dest.UnitPrice)));
                if (affordable <= 0) continue;
                decimal cost = affordable * dest.UnitPrice;
                purchases.Add(new Purchase(dest.PropertyId, dest.ItemId, affordable, cost));
                remainingBudget -= cost;
            }
        }
        return new Plan(transfers, purchases,
            purchases.Sum(p => p.EstimatedCost));
    }

    private sealed class Source
    {
        public string PropertyId { get; }
        public int Available { get; set; }
        public Source(string propertyId, int available)
        { PropertyId = propertyId; Available = available; }
    }
}
