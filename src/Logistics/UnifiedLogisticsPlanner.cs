using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleI24Seven.Logistics;

/// <summary>
/// Side-effect-free unified plan for finished-product locker transfers and
/// purchasable-input restocking. Never mutates inventory or charges money.
/// </summary>
public static class UnifiedLogisticsPlanner
{
    public enum ItemKind { Produced, Purchasable, Unknown }

    public sealed record Slot(
        string PropertyId,
        string ContainerId,
        string ItemId,
        string VariantKey,
        ItemKind Kind,
        int Quantity,
        int Target,
        int Protected,
        int FreeCapacity,
        decimal PurchasePrice,
        bool SourceEligible,
        bool DestinationEligible);

    public sealed record Move(string SourceProperty, string SourceContainer,
        string DestinationProperty, string DestinationContainer,
        string ItemId, string VariantKey, int Quantity);

    public sealed record Buy(string Property, string Container, string ItemId,
        string VariantKey, int Quantity, decimal EstimatedCost);

    public sealed record Plan(IReadOnlyList<Move> Moves, IReadOnlyList<Buy> Buys,
        decimal EstimatedTotalCost);

    public static Plan Create(IEnumerable<Slot> inventory, decimal funds, decimal reserve,
        bool autoTransport = true, bool autoRestock = true)
    {
        if (inventory is null) throw new ArgumentNullException(nameof(inventory));
        if (funds < 0 || reserve < 0) throw new ArgumentOutOfRangeException(nameof(funds));
        var rows = inventory.ToArray();
        var keys = new HashSet<(string, string, string, string)>();
        foreach (var s in rows)
        {
            if (string.IsNullOrWhiteSpace(s.PropertyId) ||
                string.IsNullOrWhiteSpace(s.ContainerId) ||
                string.IsNullOrWhiteSpace(s.ItemId) ||
                string.IsNullOrWhiteSpace(s.VariantKey) ||
                s.Quantity < 0 || s.Target < 0 || s.Protected < 0 ||
                s.FreeCapacity < 0 || s.PurchasePrice < 0 ||
                !Enum.IsDefined(typeof(ItemKind), s.Kind))
                throw new ArgumentException("Invalid inventory snapshot.", nameof(inventory));
            if (!keys.Add((s.PropertyId, s.ContainerId, s.ItemId, s.VariantKey)))
                throw new ArgumentException("Duplicate item variant in container.", nameof(inventory));
        }

        var moves = new List<Move>();
        var buys = new List<Buy>();
        decimal budget = Math.Max(0, funds - reserve);

        // Stable ordering produces reproducible plans and avoids overcommitting source stock.
        var groups = rows.GroupBy(s => (s.ItemId, s.VariantKey))
            .OrderBy(g => g.Key.ItemId, StringComparer.Ordinal)
            .ThenBy(g => g.Key.VariantKey, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            // Ambiguous or conflicting classifications must never result in guessed transactions.
            if (group.Select(x => x.Kind).Distinct().Count() != 1) continue;
            var kind = group.First().Kind;
            if (kind == ItemKind.Unknown) continue;
            var sources = group.Where(x => x.SourceEligible)
                .OrderBy(x => x.PropertyId, StringComparer.Ordinal)
                .ThenBy(x => x.ContainerId, StringComparer.Ordinal)
                .Select(x => new Supply(x, Math.Max(0, x.Quantity - Math.Max(x.Protected, x.Target))))
                .ToArray();
            foreach (var dest in group.Where(x => x.DestinationEligible)
                         .OrderBy(x => x.PropertyId, StringComparer.Ordinal)
                         .ThenBy(x => x.ContainerId, StringComparer.Ordinal))
            {
                int need = Math.Min(dest.FreeCapacity, Math.Max(0, dest.Target - dest.Quantity));
                if (need == 0) continue;

                if (kind == ItemKind.Produced && autoTransport)
                {
                    foreach (var source in sources)
                    {
                        if (source.Row.PropertyId == dest.PropertyId || source.Available == 0) continue;
                        int amount = Math.Min(need, source.Available);
                        if (amount == 0) continue;
                        moves.Add(new Move(source.Row.PropertyId, source.Row.ContainerId,
                            dest.PropertyId, dest.ContainerId, dest.ItemId, dest.VariantKey, amount));
                        source.Available -= amount;
                        need -= amount;
                        if (need == 0) break;
                    }
                }

                if (kind != ItemKind.Purchasable || !autoRestock || need == 0 ||
                    dest.PurchasePrice <= 0) continue;
                var affordableDecimal = decimal.Floor(budget / dest.PurchasePrice);
                int amountToBuy = (int)Math.Min((decimal)need, affordableDecimal);
                if (amountToBuy <= 0) continue;
                decimal cost = amountToBuy * dest.PurchasePrice;
                buys.Add(new Buy(dest.PropertyId, dest.ContainerId, dest.ItemId,
                    dest.VariantKey, amountToBuy, cost));
                budget -= cost;
            }
        }
        return new Plan(moves, buys, buys.Sum(x => x.EstimatedCost));
    }

    private sealed class Supply
    {
        public Slot Row { get; }
        public int Available { get; set; }
        public Supply(Slot row, int available) { Row = row; Available = available; }
    }
}
