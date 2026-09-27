# THE JOURNEY — towns and wild routes as interactive maps (plan, 2026-09-26)

**Status: the LOOP IS BUILT (2026-09-26)** — town map → route map → next town → leader. Next is
Shayne's playtest, then the art pass and the later buildings. Shayne, after the style-D pass:

> "I don't want the simple screens that we have now. The game loop: a TOWN — visit different areas
> of the town, like specific buildings, the hospital, shop, training facilities. Not a single screen
> but an interactive map. On the second town you have to fight the town leader to progress. After
> the town is a WILD route — catch wild monsters to add to your team, and find other things. Again
> an interactive map. Then you reach the next town: hospital, shop, train, fight the leader… and
> repeat. Once you are ready to move on you traverse the wild area."

Exploring, not tuning (root `CLAUDE.md`): tests prove each piece fires; no sims.

## 1. The loop, as read

```
 TOWN 1 (start village)  ──►  ROUTE 1  ──►  TOWN 2 ──[leader]──►  ROUTE 2  ──►  TOWN 3 ──[leader]──► …
 hospital · shop · train       catch · find · fight     services + LEADER gates the exit
```

- **A town is a place you walk around**, not a menu: buildings on a map, each a service. You leave
  when YOU are ready (the leader, from town 2, must be beaten first).
- **A route is a map you cross**, not a fixed list: branching paths from the town you left to the
  next one, with things to find, fight and catch along the way.
- What it replaces: `RunPhase.Town / ChooseArea / Trail / Gym` and the six flat screens in
  `KinPartyRunScreens(.Map)`. **What it keeps:** battles, catching, gold, the shop's goods, healing,
  the bench, reward cards, region tiers (`PartyWorld.Tiers`), the areas' creature pools and the two
  gym encounters (they become LEADERS).

## 2. What "interactive map" should mean — three options

| | A. Node map (recommended) | B. Point-and-click scene | C. Walkable overworld |
|---|---|---|---|
| What it is | Slay the Spire / Inscryption: an illustrated map, clickable places joined by paths, a token that moves along them | a painted scene with clickable hotspots, a token walks between them | Pokémon: tile map, a character you steer, tall grass |
| Town | the town drawn from above; buildings are clickable places | same, from the street | walk into doors |
| Route | a branching graph of nodes: you see what is ahead and pick your path | a few scenes in a row | open area, random encounters |
| **The decision** | **the path** — every fork is a choice between visible things | where to click, little else | steering; the choices are mostly hidden |
| Build cost | small: data graph + one screen per map kind | medium: every scene hand-placed | large: tilesets, walking animation, collision, camera |
| Art cost | a backdrop per biome + icons + building sprites | a scene per place | full tileset per biome, animated trainer — **the hardest thing for our AI pipeline** |
| Fits a deckbuilder | yes — the genre's own answer | yes | fights the genre: minutes of walking between decisions |

**Recommendation: A for both, with B's feel in towns** — a town is ONE painted map with building
sprites placed by data, a trainer token walks the street between them. It is the cheapest option
that is truly a map, every move is a decision, and it is what the art pipeline can make.

## 3. The town — buildings (brainstorm)

| Building | Does | Exists today? |
|---|---|---|
| **Hospital** | heal the team (and bench) | yes — town heals everyone on arrival today |
| **Shop** | Snares, cards, remove a card | yes — the town screen's shop |
| **Training grounds** | teach, swap or improve a MOVE in a monster's cycle (the planned "training cycles in towns", `KinRelayPlan.md` §4) | **new mechanic** — needs its own design |
| **Leader's hall** | the leader fight (preview its line first); beating it opens the exit | yes, as the gym encounter |
| **Pen / storage** | reorder the team, swap with the bench | yes — the swap on the town screen |
| Card workshop | upgrade a card, or remove one (moved out of the shop) | upgrade is new |
| Notice board | a bounty for the next route ("catch a Wisp", "win without a faint") | new |
| Tavern / rumours | see the next route's layout, or its rare | new, cheap |

**The design question a town must answer: what makes visiting a choice?** If every building is free
and unlimited, a town is a checklist, which fails the design philosophy (every screen must create a
decision). Options:
- **Gold only** (today): services cost gold; you cannot afford everything. Simple; the hospital is
  the problem (free full heal = no decision).
- **Limited visits** ("the day has three stops"): you pick 3 of 5 buildings each town.
- **Hospital costs, not free**: healing competes with cards for gold.
- **Leader first or last**: fight the leader early (weaker team, better reward) or after shopping.

## 4. The wild route — what is on it (brainstorm)

A branching path of ~6–10 nodes from town to town, generated per region from its tier and its
areas' pools (seeded). You see the whole map (or the next few rows) and choose where to walk.

| Node | Does |
|---|---|
| **Wild monster** | a VISIBLE species on the map — you choose what to fight and so what you can catch (keeps "choosing an area is choosing what you catch") |
| **Tall grass** | an unknown fight from the pool — risk for surprise |
| **Rare lair** | optional, harder, the route's rare (today's deeper path) |
| **Find** | a Snare, gold, an item |
| **Rest spring** | heal a share (today's rest find) |
| **Trainer** | a trainer's line — cannot catch, pays gold and a card |
| **Event** | a small choice with a trade-off (text + two buttons) |
| **Forks between biomes** | the two areas become two BRANCHES of one route (forest path vs rocky pass) |

HP carries across the route; only a town heals fully. That keeps the push-your-luck of the deeper
path: every extra node is reward against the HP you will bring to the next leader.

## 5. The architecture

**Engine (`KinCore/Party/`, no Godot, fully tested, records only — the Serialization Rule holds):**
- `TownMap(Buildings)` — each `Building(Kind, Name, X, Y)`; X/Y normalised 0–1 so the screen places
  sprites and hotspots from DATA, never from pixels in a painting.
- `RouteMap(Nodes, Links)` — `RouteNode(Id, Kind, Row, X, Y, Encounter?, Find?)`; a layered DAG like
  Slay the Spire's, generated from the region's tier and pools (`PartyWorld.Route(region, rng)`).
- `PartyRun`: `Phase` becomes `InTown | OnRoute | Won | Lost`; add `Town`, `Route`, `NodeId`,
  `Visited`, `LeaderBeaten`. Verbs with refusals, like the battle's: `Visit(building)`,
  `LeaveTown()` (refused until the leader is beaten, from town 2), `MoveTo(node)` (refused unless
  linked to the current node), `ResolveNode()`.
- Battles are unchanged: a node's encounter starts a battle exactly as a trail stop does today.

**Screens (`SQGodotCommon/KinGame/`):**
- `KinTownMap` — the town backdrop, building SPRITES placed from `TownMap`, a trainer token that
  walks to a clicked building, then that building's PANEL (hospital, shop, pen… — today's run-screen
  pieces, moved into panels).
- `KinRouteMap` — the route backdrop, node icons, paths drawn between them, the token, reachable
  nodes lit gold (the same "legal drop" language as the battle).
- The run-screen helpers (`Tile`, `Button`, the kit) are reused inside panels.

**Art (the `match-mockup` pipeline):** a town backdrop per region style (streets, no buildings);
building sprites generated and matted like creatures (`install_art.py`), so any town can be
assembled from data; a route backdrop per biome; node icons drawn by `make_ui.py` or game-icons.

## 6. Order of work (each step playable, each tested)

1. **Decide** the open questions below (Shayne). ☑ (four of five, §7)
2. **Mockups** — ChatGPT: the town map, the route map, one building panel (prompts in
   `docs/mockups/mockup-prompts.md`, style D). ☐
3. **Engine: the route** ☑ (2026-09-26) — `PartyRoute.cs` (`RouteNode`, `RouteLink`, `RouteMap`,
   `PartyRoutes.Build`) and `PartyRun.Route.cs` (`EnterRoute`, `CannotMoveTo`, `MoveTo`, `Here`,
   `Cleared`); 14 tests in `PartyRunTests.Route.cs`. Built BESIDE the trail: nothing enters a route
   until step 4 switches `LeaveTown` to `EnterRoute`, so the game plays unchanged meanwhile.
   Shape: the town, a first fork of one VISIBLE fight per area (left of the map is the first area,
   right the second), four rows of 2–3 places (tall grass guaranteed in the first, the rare in the
   last), the next town. A find or a spring resolves on arrival; a fight must be won before you walk
   on, and you stay on its place after. Gold: wild 20, rare 35, trainer 40.
4. **Screen: the route map** ☑ (2026-09-26) — `KinRouteMap`: places drawn from their data on a
   ComfyUI map (`backdrops/route.png`), dashed paths with a dark casing, the lead monster as the
   token, reachable places lit gold, species shown on visible fights, "?" on tall grass.
   **The trail is RETIRED** in the same step (areas, stops, the deeper path, their screens and
   tests): "Set out" now enters the route. **The leader gate is built early** (it was step 5):
   from town 2 the town screen offers the leader, and "Set out" is refused until it is beaten —
   without it, a run on routes had no boss at all. The bot (`PartySim`) walks routes and fights
   leaders. 274 KinCore tests.
5. **Engine: the town** ☑ — `PartyTown.cs` (`BuildingKind`, `Building`, `TownMap`, `PartyTowns.For`:
   one layout, no hall in town 1) and `PartyRun.Town.cs` (`HealAtHospital`, `CannotHeal`,
   `HospitalPrice` 25). **Arriving in a town no longer heals** — the hospital sells it. No `Visit`
   verb was needed: which building is open is SCREEN state; each building uses verbs that existed.
6. **Screen: the town map** ☑ — `KinTownMap` (a village green, `buildings/<kind>.png` placed from
   data, name plates, the token walks to the door) and one screen per building in
   `KinPartyRunScreens.Map.cs` (`ShowBuilding`: hospital, shop, pen, hall), each with BACK TO TOWN;
   the gate sets out, and says why when shut. `KinMapKit` holds what both maps share.
7. **Art pass** — backdrops, building sprites, node icons. ☐
8. **Playtest** (Shayne). ☐
9. **Later, designed separately:** training grounds, notice board, events, tavern. ☐

## 7. Decided (2026-09-26)

- **Leaders from town 2 on** (Shayne). Town 1 is the start village; every later town's leader gates
  its exit.
- **Route encounters: BOTH** (Shayne) — visible monsters you choose to fight, and tall grass with
  an unknown fight from the pool.
- **Map style: the NODE MAP (A)** — Shayne had no preference; the recommendation stands.
- **Town choice: gold, and the hospital costs gold** — Shayne had no preference. The simplest real
  choice: healing competes with cards and Snares. "Limited visits" is held back as the next lever
  if towns still play like a checklist.

## 8. Still open

1. Training grounds: in the first build, or after the loop is playable? (Recommended: after.)
2. How much of a route is visible — the whole map, or the next two rows?
3. Hospital price, and whether it heals the bench too — numbers are guesses until played.
