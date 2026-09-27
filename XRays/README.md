# XRays — X-Ray vision

Hides the terrain, the water or world elements so you can see through them:
trees, rocks, resources, build pieces, creatures, ground items, etc.

Two ways to use it:

* **The control panel** (vanilla woodpanel via Jötunn): one checkbox per
  category, loaded-object counters, a "Show all" button.
* **The keyboard hotkeys**: they act directly on the element under the
  cursor.

## Hotkeys (defaults, changeable in the config)

| Key | Action |
| --- | --- |
| `F8` | Opens / closes the XRays panel |
| `F7` | Hides / shows the **terrain** |
| `F10` | Hides / shows the **water** |
| `F9` | Hides / shows **the object under the cursor** |
| `Ctrl` + `F9` | Hides / shows the **whole category** of that object |

* Picking is a ray cast from the camera (screen center when the pointer is
  locked, otherwise the real mouse position), up to a maximum distance of
  100 m (configurable).
* When the terrain is already hidden, picking "sees" through it and targets
  the object behind it.
* The **Under the cursor** section of the panel shows three lines: the element
  name, its category and whether it is hidden; the game component types
  backing that category (debug info, e.g. `Game types: TreeBase, TreeLog  ·
  match: TreeBase`, `+ names` when the category also matches object names and
  `match: name ("FirTree_small")` when that name rule did match); then its
  world coordinates (`Pos (x, y, z)` for an
  object, `Hit (x, y, z)` for the aimed point on the terrain / water) and the
  distance in meters.
* Colliders are never touched: the world stays playable, only renderers are
  disabled (their original state is memorised and restored).

## Recognized categories

Trees · Rocks / ore · Pickable resources · Build pieces · Creatures ·
Ground items · Crops · Spawners · Grass / vegetation.

A category is normally backed by a game component (`TreeBase` / `TreeLog` for
Trees, `WearNTear` / `Piece` for Build pieces, ...). The **Trees** category
also matches object names — `tree`, `birch`, `beech`, `oak`, `stump`, `stub`,
`root`, `branch` (case insensitive) — because the small decorative trees, the
stubs, the roots and the branches only carry a `Destructible`: without that
name match they would never be hidden with the rest of the forest. An object
holding a known component is never claimed by the name rule: that component
decides its category, which keeps the panel label and the hidden group in
sync.

Any object that does not belong to those groups automatically creates a
"name" category (every object sharing the same name).

## Installation

**Client-side only** — nothing to install on the server.

### With a mod manager (recommended)
Vortex, r2modman or Gale.

### Manually
1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
   and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Copy `XRays.dll` into `Steam\steamapps\common\Valheim\BepInEx\plugins\`.

## Configuration

All options live in `BepInEx\config\orax.XRays.cfg` (created on the first
launch, also editable in game through Jötunn's ConfigManager):

| Section | Option | Default | Description |
| --- | --- | --- | --- |
| `Hotkeys` | `Panel` | `F8` | Opens / closes the XRays panel |
| `Hotkeys` | `Terrain` | `F7` | Hides / shows the terrain |
| `Hotkeys` | `Water` | `F10` | Hides / shows the water |
| `Hotkeys` | `Pick` | `F9` | Hides / shows the object under the cursor |
| `Hotkeys` | `Category modifier` | `LeftControl` | Hold together with `Pick` to act on the whole category |
| `Selection` | `Max distance` | `100` | Maximum picking distance (meters) |
| `Logging` | `Log cursor info` | `false` | Logs the panel's "Under the cursor" text, only while the panel is on screen |

### Logging the cursor info

With `Logging → Log cursor info = true`, the mod writes to `BepInEx\LogOutput.log`
exactly what the panel shows (element, category, hidden state, game types,
coordinates, distance). The text is sampled every 0.4 s and logged **only while
the panel is on screen — nothing is written once it is closed — and only when
it changes**, as a single line (the panel's line breaks become ` | `). The
logged line is the very text displayed by the panel at that moment. Closing
and then reopening the panel logs the current target again. Handy to find
which game types back a given prefab:

```
[Info : XRays] Under the cursor: Birch1  ·  Category: Trees  ·  hidden  |  Game types: TreeBase, TreeLog  ·  match: TreeBase  |  Pos (1234.5, 67.8, -910.2)  ·  42.3 m
[Info : XRays] Under the cursor: FirTree_small(Clone)  ·  Category: Trees  |  Game types: TreeBase, TreeLog + names  ·  match: name ("FirTree_small(Clone)")  |  Pos (199.7, 36.5, -549.0)  ·  8.6 m
```

## Development

* Build via `XRays.sln` (the post-build step publishes the DLL into the game profile).
* Jötunn API: <https://valheim-modding.github.io/Jotunn/>
