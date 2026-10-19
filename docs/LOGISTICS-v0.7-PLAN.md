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
