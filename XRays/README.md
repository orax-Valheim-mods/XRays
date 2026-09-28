# XRays — X-Ray vision

Hides the terrain, the water, the clouds, the mist or world elements so you
can see through them: trees, rocks, resources, build pieces, creatures,
ground items, etc.

Two ways to use it:

* **The control panel** (vanilla woodpanel via Jötunn): one single list of
  options — the clouds, the HUD, the mist, your own character, the terrain
  and the water share one compact line (a second one when their labels do
  not fit side by side), then a checkbox per category, in alphabetical
  order — with loaded-object counters,
  a row that keeps the whole setup for the next launch, and three global
  buttons: **Reveal** (shows everything the checked options
  were hiding, the next press turns it into **Restore** and brings that exact
  setup back — with nothing checked the first press checks every option
  instead), **Hide all** (everything in that list at once) and **Show all**
  (back to the normal world). Staying on any row or button with the mouse
  for a moment brings up a little box describing it: the box follows the
  pointer and goes away as soon as the pointer leaves. The title and the
  **Close** button stay on top
  of the panel while the list scrolls under them, with the mouse wheel or
  the scrollbar on the right; the height of the panel itself is the
  `Display` → `Panel height` option. The line above those buttons sums the
  panel up: `Hidden objects: N` is the number of object roots currently
  invisible — ticked categories, objects picked one by one, **Hide all** —
  while `Other: M` counts what is hidden without a row of its own in the
  list (the objects picked one by one and the categories created on the fly).
* **The keyboard hotkeys**: they act directly on the element under the
  cursor.

## Hotkeys (changeable in the config)

| Key | Action |
| --- | --- |
| `F8` | Opens / closes the XRays panel |
| `Escape` | Closes the panel while it is on screen (`Close control panel` in the config, the game pause menu stays closed) |
| `Not set` | Shows every checked option at once; the next press brings that exact setup back |
| `Not set` | Hides / shows the **terrain** |
| `Not set` | Hides / shows the **water** (zones and the sea on the horizon) |
| `Not set` | Hides / shows the **clouds** of the sky (planes, horizon cylinder, particle clouds) |
| `Not set` | Hides / shows the **mist** and the fog (ground mist, weather fog, distant fog planes, distance fog) |
| `Not set` | Hides / shows **your own character** (body, armor and held item) |
| `Not set` | Hides / shows the **HUD** (bars, hot bar, mini map, crosshair, hints) |
| `Not set` | Hides / shows **the object under the cursor** |
| `Ctrl` + `F9` | Hides / shows the **whole category** of that object |

* The eight `Not set` rows come unbound: a fresh install presses nothing for
  them, each one gets the combination recorded for it in the config window
  (`F1`), by clicking the hotkey and pressing it. The help line of the panel
  only lists the hotkeys that have a key.

* Picking is a ray cast from the camera (screen center when the pointer is
  locked, otherwise the real mouse position), up to a maximum distance of
  100 m (configurable). The pick hotkeys are ignored while the panel is
  open — the pointer belongs to the interface then — so pick with the panel
  closed and the counters show the result when it reopens.
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
* **Clouds** takes away everything the environment calls a cloud, wherever it
  sits in its tree: the big planes and the horizon cylinder at its top level,
  but also the particle clouds and the distant mist clouds, which live
  deeper inside it.
* **Mist** takes the fog away: the layers of the environment named after a
  mist or a fog (ground mist, weather fog, distant fog planes, ocean mist),
  the mist systems the game runs itself — the Mistlands mist included — and
  the distance fog of the game, which stays off while the option is on and
  comes back exactly as it was when it is unticked. A few layers carry both
  names (`FogClouds`, `mistcloud`): the **Clouds** option and the **Mist**
  option both take them, so they disappear with either of the two.
* The reveal / restore toggle (its button and its hotkey) only deals with the
  options of the list: the objects hidden one by one with the **Pick** hotkey
  and the categories only known under their name keep their state, and anything
  ticked by hand while the reveal is on is kept when the setup comes back. **Hide
  all** and **Show all** both forget that setup: they are global commands, so
  after either of them the reveal starts again from what the list holds.
* **Remember this setup** (the row under the counters, `Setup` → `Remember
  setup` in the config) keeps the very list of the panel — the world
  options and the ticked categories — and checks those options again the
  next time the game starts. While the toggle is on the setup is written
  back on every change, so the panel, the hotkeys and **Hide all** / **Show
  all** are all part of it; turning it off stops the saving and the next
  launch starts with nothing checked. Like the reveal toggle it only deals
  with the options of the list: the objects hidden one by one and the
  categories created on the fly are left out of it.
* **Player** takes your own character out of the picture — body, armor
  and held item — so it never blocks the view. It only disables renderers,
  the colliders and the camera stay untouched; the **Creatures** category
  never includes your own character, and the ray cast skips it as well (you
  never pick yourself).
* **HUD** takes the whole in-game HUD away in one go: health, stamina,
  eitr and adrenaline bars, hot bar, mini map, crosshair, status effects,
  build / key hints, ship and mount panels — everything the game itself
  groups under its HUD root. Chat, notifications and damage numbers are
  separate overlays and stay on screen, and so does the pause menu (the
  game drags it off screen along with the HUD; the mod keeps it in view,
  otherwise Escape would open an invisible menu). Showing the HUD again
  also undoes the game's own `Ctrl` + `F3` toggle, so the HUD always comes
  back exactly where it belongs.
* The bars floating over the creatures (name, health, levels, boss bar) are
  drawn by `EnemyHud`, an overlay of their own, so no renderer pass ever
  sees them. The **Creatures** category takes them away together with the
  creatures, and the HUD option takes them as well. They are faded out with
  a group put on the EnemyHud root — the game activates that root again
  from its own update, a deactivation would not hold — and that group also
  covers the bars appearing later on, with nothing to do per creature.

## Recognized categories

Build pieces · Creatures · Crops · Grass / vegetation · Ground items ·
Locations · Ores · Pickable resources · Rocks · Spawners · Trees.

A category is normally backed by a game component (`TreeBase` / `TreeLog` for
Trees, `WearNTear` / `Piece` for Build pieces, ...). **Creatures** follows
`Character` — the animals and the monsters — plus `Fish`, because a swimming
fish carries no `Character` at all (it is `Fish` + `ItemDrop`) and would be
reported as a ground item otherwise, plus `RandomFlyingBird`: the birds
circling overhead (`Crow`, `Seagal`, `AshCrow`) carry no `Character` either,
only a `Destructible`, and without that component they would stay in sight
while every other animal is hidden. The category also takes the bars the
game floats over those creatures (bar and name included): they are no
renderers either, they get faded out — see the bullet above. The **Trees**
category
also matches object names — `tree`, `birch`, `beech`, `oak`, `stump`, `stub`,
`root`, `branch` (case insensitive) — because the small decorative trees, the
stubs, the roots and the branches only carry a `Destructible`: without that
name match they would never be hidden with the rest of the forest. An object
holding a known component is never claimed by the name rule: that component
decides its category, which keeps the panel label and the hidden group in
sync. An ancestor holding a known component follows the same rule from the
other end: it stays with its own category and never takes the objects of the
same name below it, so the rocks sitting inside a stone structure are hidden
with the rocks while the structure stays with its own category.

The roots a category finds on its own are off limits to the name rules as
well: `root` also matches `grassroot`, the object the game builds to hold
every clutter patch, so hiding the Trees would have hidden the whole grass,
the bushes and the isolated plants with the trees.

**Rocks** follows the same pattern: the mineable rocks carry `MineRock5` /
`MineRock`, while a meadow boulder (`Rock_3`, `Rock_4`, `rock4_forest`, ...), a
Mistlands rock (`rock_mistlands1`) or a cliff (`cliff_mistlands2`) only carries
a `Destructible`, and `HugeStone1` / `rockformation1` no component the mod
knows at all — so the category also matches `rock`, `boulder`, `stone` and
`cliff` in the object name.

**Ores** takes the veins themselves. The game marks an ore and a plain stone
with the same components, so only the name tells them apart: `copper`, `tin`,
`iron`, `silver`, `obsidian`, `meteor`, `flametal`, `sulfur`, `gold` and
`coal`. Rocks hands those names over to Ores, which is what keeps a copper vein
out of the rocks while `MineRock_Stone` and the broken rock pieces stay in.

**Spawners** is backed by the game's own spawner components: `CreatureSpawner`
for the one-shot spawners, `SpawnArea` for the nests, the piles and the evil
hearts, `TriggerSpawner` for the holes that open when you come close. A
spawner carrying none of them (the Elaking hole, a modded spawner) is caught
by its name instead: every one of those prefabs is named `Spawner...`, taken
from the start of the name, so the model pieces and the effects whose name
merely contains "spawner" are never claimed. `SpawnSystem` is deliberately
left out — it sits on the zone controller, not on a spawner.

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

**Locations** covers the structures the game places on its own: a dungeon
entrance lives below a `LocationProxy` — the object that spawns the location
when you come close — and that proxy carries none of the components above, so
the entrance survives every other category, **Hide all** included; the
locations themselves (huts, ruins, boss rooms, ...) carry `Location`. Both
components belong to this category, which is listed last: a known component
always decides the category, so the trees, the rocks and the build pieces
still claim their own objects first.

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
launch, also editable in game through BepInEx's Configuration Manager, the
window opened with `F1`):

The hotkeys are **key combinations**: in that window, click a hotkey and it
records the next combination you press (a key plus modifier keys), there is
no list to pick from, and *Clear* unsets it. A combination has to be pressed
exactly as recorded: the modifier keys must be held with the main key, and
any other key held at the same time cancels the press — except the modifier
keys themselves (`Shift`, `Ctrl`, `Alt`), which are always free. It is
written main key first, the way the window shows it (`F9 + LeftControl` for
`Ctrl` held together with `F9`).

They are listed under the `Show/hide hotkeys` section, except `Close
control panel`, `Open/close control panel` and `Pick category` which sit
together in `General hotkeys`. When a setting moves, every line it used to
have is read once on the next launch, its value is kept, and it is then
dropped from the file.

| Section | Option | Default | Description |
| --- | --- | --- | --- |
| `General hotkeys` | `Close control panel` | `Escape` | Closes the XRays panel |
| `General hotkeys` | `Open/close control panel` | `F8` | Opens / closes the XRays panel |
| `General hotkeys` | `Pick category` | `F9 + LeftControl` | Hides / shows the whole category of the object under the cursor: `Pick` pressed with that combination |
| `Show/hide hotkeys` | `Terrain` | `Not set` | Hides / shows the terrain |
| `Show/hide hotkeys` | `Water` | `Not set` | Hides / shows the water |
| `Show/hide hotkeys` | `Clouds` | `Not set` | Hides / shows the clouds (planes, horizon cylinder, particle clouds) |
| `Show/hide hotkeys` | `Mist` | `Not set` | Hides / shows the mist and the fog, and the distance fog of the game (the game itself already uses `F1`, `F2`, `F3`, `F9` and `F11`) |
| `Show/hide hotkeys` | `Player` | `Not set` | Hides / shows your own character (the Creatures category never includes it) |
| `Show/hide hotkeys` | `HUD` | `Not set` | Hides / shows the HUD (the game's own `Ctrl` + `F3` does the same) |
| `Show/hide hotkeys` | `Pick` | `Not set` | Hides / shows the object under the cursor |
| `Show/hide hotkeys` | `Reveal` | `Not set` | Shows every checked option at once, then brings that exact setup back |
| `Selection` | `Max distance` | `100` | Maximum picking distance (meters) |
| `Display` | `Panel height` | `700` | Vertical size of the panel (360 to 1200 pixels); the rows scroll when they do not fit in it |
| `Logging` | `Log cursor info` | `false` | Logs the panel's "Under the cursor" text, only while the panel is on screen |
| `Setup` | `Remember setup` | `false` | Keeps the options checked in the panel and checks them again on the next launch (same thing as the **Remember this setup** row of the panel) |
| `Setup` | `Saved setup` | _empty_ | The options kept for the next launch, as a comma separated list of names (`terrain`, `water`, `clouds`, `mist`, `player`, `hud` and the category keys). Filled in by the panel; edited by hand only to force a setup |

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
