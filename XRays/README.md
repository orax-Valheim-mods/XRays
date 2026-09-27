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
  match: TreeBase`); then its world coordinates (`Pos (x, y, z)` for an
  object, `Hit (x, y, z)` for the aimed point on the terrain / water) and the
  distance in meters.
* Colliders are never touched: the world stays playable, only renderers are
  disabled (their original state is memorised and restored).

## Recognized categories

Trees · Rocks / ore · Pickable resources · Build pieces · Creatures ·
Ground items · Crops · Spawners · Grass / vegetation.

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
| `Logging` | `Log cursor info` | `false` | Writes the panel's "Under the cursor" text to the BepInEx log |

### Logging the cursor info

With `Logging → Log cursor info = true`, the mod writes to `BepInEx\LogOutput.log`
exactly what the panel shows (element, category, hidden state, game types,
coordinates, distance). The text is sampled every 0.4 s and logged **only when
it changes**, as a single line (the panel's line breaks become ` | `), and the
panel does not need to be open. Handy to find which game types back a given
prefab:

```
[Info : XRays] Under the cursor: Birch1  ·  Category: Trees  ·  hidden  |  Game types: TreeBase, TreeLog  ·  match: TreeBase  |  Pos (1234.5, 67.8, -910.2)  ·  42.3 m
```

## Development

* Build via `XRays.sln` (the post-build step publishes the DLL into the game profile).
* Jötunn API: <https://valheim-modding.github.io/Jotunn/>
