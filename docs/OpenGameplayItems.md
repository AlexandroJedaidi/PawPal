# Open Gameplay Items

Last updated: 2026-05-28

## Implemented now

- Catalog-driven shop and inventory for the current v1 item set
- Buying supported food, toys, and collars
- Inventory ownership and food quantities
- Equipping collars from Home inventory
- Visible collar spawning on the active dog
- Spawning owned toys into the room with a max of 3 active toys
- Local JSON save/load for trainer state, owned items, equipped collars, active dog, and daily tasks
- Explicit `dogId` scene binding support on `DogRoomAgent`
- Optional serialized collar anchor support on `DogRoomAgent`
- Runtime warnings for fallback scene binding and missing runtime collar/toy resource paths

## Explicitly not implemented yet

- Competitions
- Trick progression and deeper dog training logic
- Dog-breed purchasing or kennel/adoption flow
- Clothing logic
- Furniture logic
- Grooming gameplay
- Clubs / social systems / leaderboards
- Persistence for live spawned room toys across app restarts
- Full daily-task pool from the spreadsheet

## Open systems

### Competitions

Status: not implemented

Needed:

- competition data model
- entry requirements
- scoring and placements
- rewards and progression hooks
- UI flow from map/profile/shop references

Recommended next step:

- create a `CompetitionDefinition` table and a single playable beginner competition first

### Trick and training progression

Status: partial placeholder only

Needed:

- trick unlock table
- training XP or repetition rules
- stat-to-trick relationship
- save data for learned tricks

Recommended next step:

- define 3-5 starter tricks and one stat progression rule per trick

### Dog acquisition

Status: dog cards are visible but disabled

Needed:

- roster unlock rules
- adoption/purchase flow
- scene/runtime mapping for multiple owned dogs

Recommended next step:

- decide whether dogs are level unlocks, purchases, or both before enabling shop buys

### Clothing and furniture

Status: visible as coming-soon placeholders only

Needed:

- supported catalog
- ownership and equip/place rules
- scene hooks for furniture placement and dog interactions

Recommended next step:

- keep disabled until we have at least one complete vertical slice like collars

## Current risks and mitigations

### Risk: scene dogs are matched by runtime list order

Impact:

- if a scene contains multiple `DogRoomAgent` objects in a different order, the wrong collar can attach to the wrong dog

Mitigation:

- implemented in code: `DogRoomAgent` now supports a serialized `dogId`
- implemented in code: the scene bridge now maps by `dogId` first and warns when it must fall back to object order
- still needed in Unity: assign matching `dogId` values on scene dogs for stable multi-dog scenes

### Risk: collar anchor resolution is still heuristic

Impact:

- some breeds may place collars slightly too high/low or on a fallback transform

Mitigation:

- implemented in code: `DogRoomAgent` now supports an optional serialized collar anchor
- implemented in code: the bridge prefers the explicit collar anchor, then name-based neck/accessory fallback, then warns if it has to use the root
- still needed in Unity: assign collar anchors on dog prefabs/scenes that need art tuning

### Risk: shop and inventory use a runtime catalog, but future art/content may drift

Impact:

- spreadsheet, UI art, and runtime item IDs can get out of sync

Mitigation:

- treat runtime item IDs as the source of truth
- keep a short catalog block in `gamelogic.xlsx`
- implemented in code: the runtime now validates duplicate catalog IDs plus missing collar/toy resource paths at boot

### Risk: toy persistence is intentionally partial

Impact:

- players keep ownership, but the room resets after restart

Mitigation:

- leave this behavior documented for v1
- if persistence becomes important, save only lightweight spawn records instead of raw scene objects

### Risk: only 3 daily tasks are truly generated in code

Impact:

- the spreadsheet implies a larger task pool than the runtime currently supports

Mitigation:

- keep the sheet annotated with the current runtime limitation
- expand tasks only when their underlying systems exist

## Suggested implementation order

1. Add explicit `dogId` scene binding for `DogRoomAgent`
2. Add serialized collar anchor support per dog prefab
3. Build one beginner competition vertical slice
4. Expand the training/trick system to support that competition
5. Revisit dog acquisition after competitions and progression are stable
