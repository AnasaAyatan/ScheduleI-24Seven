using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleI24Seven.Logistics;

/// <summary>
/// Adapter contract for verified, authoritative Schedule I game APIs.
/// Implementations must preserve exact item instance metadata and never synthesize goods.
/// </summary>
public interface IGameLogisticsAdapter
{
    bool IsAuthoritative { get; }
    IReadOnlyList<UnifiedLogisticsPlanner.Slot> ReadInventory();
    decimal ReadAvailableFunds();
    bool TryTransfer(UnifiedLogisticsPlanner.Move move, out string reason);
    bool TryPurchase(UnifiedLogisticsPlanner.Buy purchase, out string reason);
}

/// <summary>
/// Explicitly read-only default: prevents accidental inventory/money changes
/// until the game-specific IL2CPP adapter has been implemented and verified.
/// </summary>
public sealed class UnavailableGameLogisticsAdapter : IGameLogisticsAdapter
{
    public bool IsAuthoritative => false;
    public IReadOnlyList<UnifiedLogisticsPlanner.Slot> ReadInventory()
        => Array.Empty<UnifiedLogisticsPlanner.Slot>();
    public decimal ReadAvailableFunds() => 0m;
    public bool TryTransfer(UnifiedLogisticsPlanner.Move move, out string reason)
    {
        reason = "Game inventory transfer API not verified.";
        return false;
    }
    public bool TryPurchase(UnifiedLogisticsPlanner.Buy purchase, out string reason)
    {
        reason = "Game purchasing API not verified.";
        return false;
    }
}

/// <summary>
/// Fail-closed execution coordinator. Plans are proposals, not transactions.
/// Only one verified action per call is attempted, and each new call must re-read
/// live inventory and funds. No action is retried automatically after failure.
/// </summary>
public sealed class LogisticsCoordinator
{
    private readonly IGameLogisticsAdapter _game;
    private bool _halted;
    public bool Halted => _halted;

    public LogisticsCoordinator(IGameLogisticsAdapter game)
        => _game = game ?? throw new ArgumentNullException(nameof(game));

    public sealed record Step(bool Executed, string Reason);

    public Step RunOnce(decimal cashReserve, bool transport = true, bool restock = true)
    {
        if (_halted) return new Step(false, "Coordinator halted after an unverified transaction.");
        if (!_game.IsAuthoritative) return new Step(false, "Not an authoritative game session.");
        try
        {
            var plan = UnifiedLogisticsPlanner.Create(
                _game.ReadInventory(), _game.ReadAvailableFunds(), cashReserve,
                transport, restock);
            // Do not execute a second action from a stale snapshot.
            if (plan.Moves.Count > 0)
            {
                var move = plan.Moves[0];
                if (_game.TryTransfer(move, out var reason))
                    return new Step(true, "Transfer confirmed by adapter.");
                _halted = true;
                return new Step(false, reason);
            }
            if (plan.Buys.Count > 0)
            {
                var buy = plan.Buys[0];
                if (_game.TryPurchase(buy, out var reason))
                    return new Step(true, "Purchase confirmed by adapter.");
                _halted = true;
                return new Step(false, reason);
            }
            return new Step(false, "No eligible logistics action.");
        }
        catch (Exception ex)
        {
            _halted = true;
            return new Step(false, "Logistics halted: " + ex.Message);
        }
    }
}
