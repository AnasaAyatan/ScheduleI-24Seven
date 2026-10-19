# v0.7 Logistics — Implementation Status

**Status: planning engine committed; NO in-game auto-restock or transfers enabled yet.**

## Product behavior
- Keep the interaction as simple as lasersquid-style restocking: automatic inventory replenishment with minimal setup.
- Two built-in systems: automatic purchasing/restocking and property-to-property transfers.
- Prefer surplus at owned properties before spending cash.
- Never consume stock below a property's target/protected reserve.
- Respect free capacity, existing funds and a cash reserve.
- Core features should be enabled by default once the adapters are verified; users can disable them.

## Implementation already added
`src/Logistics/LogisticsPlanner.cs` is a side-effect-free stock planner, with `Stock`, `Transfer` and `Purchase` proposals. No game inventory/money mutations happen in this engine.
Run isolated tests: `dotnet run --project tests/LogisticsPlanner.Tests/LogisticsPlanner.Tests.csproj`.

## Blockers before a working release
1. Inspect Schedule I 0.4.7f12 IL2CPP generated assemblies for owned property enumeration, storage and station item slots, item definitions, prices, and legitimate purchase methods.
2. Implement an inventory snapshot adapter using only verified game APIs and item identifiers.
3. Use a server-authoritative commit adapter with confirmation before/after each action. Avoid duplicating inventory, network desync, or charging twice.
4. Distinguish manually-removed stock from normal production consumption; don't surprise the player by immediately refilling deliberately cleared slots.
5. Add MelonPreferences/JSON settings and scheduled runtime checks only after the adapters are verified.
6. Add in-game end-to-end checks for save/load, scarce funds, capacity constraints, multiplayer, and game updates.

**Neither the planner nor this document copies lasersquid source code.** Public source visibility is not permission to reuse it.

## Clarified core feature: locker-to-locker logistics across properties

The transport feature targets **individual player-owned storage lockers on different owned properties**, not just an abstract property-wide stock total. Example: a source warehouse locker containing soil transfers a permitted quantity to a destination production locker at another property.

### Default experience
- Auto-transfer on, with no routes required for basic operation.
- Detect shortages at destination lockers and compatible surplus in lockers at other owned properties.
- Respect existing stacks, item identity, capacity, protected source stock, and any locker or slot filters supported by the game.
- Allow a simple designation of destination locker / desired stock per item, with source lockers automatic by default.
- Never pull from containers not explicitly eligible (e.g. player inventory, personal storage, processing stations, customer/dealer inventory).
- Inventory moves should be transactional: reserve exact source items and destination slots; verify host authority; remove source and add destination with confirmation, rollback or safely halt on failure.
- No visible vehicle requirement: transfer is virtual, but items must genuinely move within game inventory.
- Avoid repeated transfers when stock is intentionally removed; treat stock targets as player-controlled demand rather than blindly cloning source contents.
- Display basic last-transfer summary and a failure reason if delivery was blocked.

### Required implementation adjustment
The current Stock model is property-level and cannot distinguish two lockers within one property. Before any live transfer is implemented, upgrade the model to include stable locker/container identifiers and per-item inventory snapshots, plus source/destination eligibility. The existing planner is a prototype and **does not currently move locker contents**.

### Validation
Test with two lockers in different properties, mixed item stacks, no destination space, blocked items, save/reload, multiple candidate source lockers, interrupted transactions, and multiplayer host/client ownership.

## Product transfer scope clarification (October 18)

The primary cross-property transport use case is **finished goods**, e.g. Heavenly-grade Coco Leaves, between individual lockers. Basic store supplies such as Cuke and MegaBean remain the responsibility of AutoRestock, and are not the primary transport target.

- Identify exact product variants, including quality/grade and other meaningful per-item instance metadata. Never conflate Heavenly-grade with lower-quality variants.
- Transfer the actual item instance/stack data rather than reconstructing items from a generic product identifier.
- Default to opt-in locker destinations or explicit product filters to prevent moving goods needed in production.
- Support source keep-back quantities and destination capacity, partial transfers, and multiple production/distribution properties.
- Prefer preserving produced goods across locker transfers rather than purchasing replacement ingredients.
- Defer buying any products: this is a finished-product redistribution feature, separate from supply restocking.
- Verify host-authoritative inventory movement APIs and save serialization prior to any live mutation.
- Update the planner schema to include container ID and a product variant key; the existing abstract stock model is inadequate for product-instance transfer.

This section overrides earlier wording suggesting that automatic purchases and generic raw-material balancing are the main purpose of inter-property transport.
