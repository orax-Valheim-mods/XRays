# XRays — X-Ray vision

Hides the terrain, the water, the clouds or world elements so you can see
through them: trees, rocks, resources, build pieces, creatures, ground items,
etc.

Two ways to use it:

* **The control panel** (vanilla woodpanel via Jötunn): one checkbox per
  category, loaded-object counters, and two global buttons — **Hide all**
  (terrain, water, clouds and every category at once) and **Show all** (back
  to the normal world).
* **The keyboard hotkeys**: they act directly on the element under the
  cursor.

## Hotkeys (defaults, changeable in the config)

| Key | Action |
| --- | --- |
| `F8` | Opens / closes the XRays panel |
| `Escape` | Closes the panel while it is on screen (the game pause menu stays closed) |
| `F7` | Hides / shows the **terrain** |
| `F10` | Hides / shows the **water** (zones and the sea on the horizon) |
| `F6` | Hides / shows the **clouds** of the sky |
| `F9` | Hides / shows **the object under the cursor** |
| `Ctrl` + `F9` | Hides / shows the **whole category** of that object |

* Picking is a ray cast from the camera (screen center when the pointer is
  locked, otherwise the real mouse position), up to a maximum distance of
  100 m (configurable).
* When the terrain is already hidden, picking "sees" through it and targets
  the object behind it.
* The **Under the cursor** section of the panel shows four lines: the element
  name, its category and whether it is hidden; the game component types
  backing that category (debug info, e.g. `Game types: TreeBase, TreeLog  ·
  match: TreeBase`, `+ names` when the category also matches object names and
  `match: name ("FirTree_small")` when that name rule did match); the path of
  the object the ray really touched (`Hit: _Zone(Clone)/Water/WaterSurface`,
  which tells apart the terrain, the water and the object itself); then its
  world coordinates (`Pos (x, y, z)`: the object position for an object, the
  aimed point for the terrain / water) and the distance in meters.
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

**Grass / vegetation** works the same way: the game draws the instanced grass
with `InstanceRenderer` (the `Renderer`s of the patches are inactive), so the
category is backed by that component; the bushes, ferns, mushrooms and
isolated plants are caught by name — `bush`, `shrub`, `grass`, `fern`, `reed`,
`moss`, `mushroom`, `flower`; and everything the clutter system spawns is
grouped under its own `grassroot` object. That root matters: the
`ClutterSystem` component itself lives on the root of the scene, hiding it
would take the clouds, the sea and the weather down with the grass.

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
| `Hotkeys` | `Close` | `Escape` | Closes the XRays panel |
| `Hotkeys` | `Terrain` | `F7` | Hides / shows the terrain |
| `Hotkeys` | `Water` | `F10` | Hides / shows the water |
| `Hotkeys` | `Clouds` | `F6` | Hides / shows the clouds |
| `Hotkeys` | `Pick` | `F9` | Hides / shows the object under the cursor |
| `Hotkeys` | `Category modifier` | `LeftControl` | Hold together with `Pick` to act on the whole category |
| `Selection` | `Max distance` | `100` | Maximum picking distance (meters) |
| `Logging` | `Log cursor info` | `false` | Logs the panel's "Under the cursor" text, only while the panel is on screen |

### Logging the cursor info

With `Logging → Log cursor info = true`, the mod writes to `BepInEx\LogOutput.log`
exactly what the panel shows (element, category, hidden state, game types,
hit path, coordinates, distance). The text is sampled every 0.4 s and logged **only while
the panel is on screen — nothing is written once it is closed — and only when
it changes**, as a single line (the panel's line breaks become ` | `). The
logged line is the very text displayed by the panel at that moment. Closing
and then reopening the panel logs the current target again. Handy to find
which game types back a given prefab:

```
[Info : XRays] Under the cursor: Birch1  ·  Category: Trees  ·  hidden  |  Game types: TreeBase, TreeLog  ·  match: TreeBase  |  Hit: Trees(Clone)  |  Pos (1234.5, 67.8, -910.2)  ·  42.3 m
[Info : XRays] Under the cursor: FirTree_small(Clone)  ·  Category: Trees  |  Game types: TreeBase, TreeLog + names  ·  match: name ("FirTree_small(Clone)")  |  Hit: _Zone(Clone)/Terrain  |  Pos (199.7, 36.5, -549.0)  ·  8.6 m
```

## Development

* Build via `XRays.sln` (the post-build step publishes the DLL into the game profile).
* Jötunn API: <https://valheim-modding.github.io/Jotunn/>
