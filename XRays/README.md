# XRays

Hides the terrain, the water, the clouds, the mist or world elements so you
can see through them: trees, rocks, resources, build pieces, creatures,
ground items, etc.

Two ways to use it:

* **The control panel** (vanilla woodpanel via Jötunn): one single list of
  options — the clouds, the HUD, the mist, your own character, the terrain
  and the water share one compact line (a second one when their labels do
  not fit side by side), then a checkbox per category, in alphabetical
  order — with loaded-object counters,
  a row that keeps the whole setup for the next launch, a row that turns
  the little boxes on and off, and three global buttons: **Reveal** (shows
  everything the checked options
  were hiding, the next press turns it into **Restore** and brings that exact
  setup back — with nothing checked the first press checks every option
  instead), **Hide all** (everything in that list at once) and **Show all**
  (back to the normal world). Staying on any row or button with the mouse
  for a moment brings up a little box describing it: the box follows the
  pointer and goes away a moment after the pointer has left; the **Show
  tooltips** row (`Display` → `Show tooltips`) takes every box away. The
  title and the
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
| `Not set` | Hides / shows the **mist** and the fog (ground mist, weather fog, distant fog planes, ocean mist, Mistlands mist, distance fog) |
| `Not set` | Hides / shows **your own character** (body, armor and held item) |
| `Not set` | Hides / shows the **HUD** (bars, hot bar, mini map, crosshair, hints) |
| `Not set` | Hides / shows **the object under the cursor** |
| `Ctrl` + `F9` | Hides / shows the **whole category** of that object |

* The eight `Not set` rows come unbound: a fresh install presses nothing for
  them, each one gets the combination recorded for it in the config window
  (`F1`), by clicking the hotkey and pressing it. The help line of the panel
  only lists the hotkeys that have a key.
* The pause menu also carries an **XRays** line, right under **Settings**:
  clicking it closes the menu and opens the panel, exactly as the `F8`
  hotkey does (with a gamepad, `Down` from Settings reaches it). The line
  follows the `General` → `Enable` option: switched off, the pause menu
  does not show it.

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
  aimed point for the terrain / water) and the distance in meters. While the
  pointer rests on the panel itself, no new ray cast runs: the four lines
  keep what they last saw in the world.
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
* **Save setup** and **Restore setup** (the two buttons under the counters,
  `Setup` → `Saved setup` in the config) deal with the very list of the
  panel — the world options and the ticked categories. **Save setup** writes
  the options ticked at that moment (a message confirms it), and **Restore
  setup** ticks them back right away. Nothing is applied by itself: the
  saved setup is never loaded when the game starts, the only way it comes
  back is a press on **Restore setup**. Like the reveal toggle the two
  buttons only deal with the options of the list: the objects hidden one by
  one and the categories created on the fly are left out of them.
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
also matches object names — the substrings live in *The name rules file*
below — because the small decorative trees, the
stubs, the roots and the branches only carry a `Destructible`: without that
name match they would never be hidden with the rest of the forest. An object
holding a known component is claimed through that component, never by a name
rule, which keeps the panel label and the hidden group in sync — unless the
category hands the name over to another one, which is what Rocks does for the
ore veins (below). An ancestor holding a known component follows the same
rule from the other end: it stays with its own category and never takes the
objects of the same name below it, so the rocks sitting inside a stone
structure are hidden with the rocks while the structure stays with its own
category.

The roots a category finds on its own are off limits to the name rules as
well: `root` also matches `grassroot`, the object the game builds to hold
every clutter patch, so hiding the Trees would have hidden the whole grass,
the bushes and the isolated plants with the trees.

**Rocks** follows the same pattern: the mineable rocks carry `MineRock5` /
`MineRock`, while a meadow boulder (`Rock_3`, `Rock_4`, `rock4_forest`, ...), a
Mistlands rock (`rock_mistlands1`) or a cliff (`cliff_mistlands2`) only carries
a `Destructible`, and `HugeStone1` / `rockformation1` no component the mod
knows at all — so the category claims them by name too, with the substrings
of *The name rules file* below.

**Ores** takes the veins themselves, plus the pieces of the giant corpses of
the Mistlands (petrified bones, ancient armour, swords, soft tissue). The game
marks an ore and a plain stone with the same components, so only the name tells
them apart — which names the category claims, and which ones Rocks hands over
to it, is written in *The name rules file* below. A family token there claims
the whole family and nothing else: the `Morkhalla_giant_railing` build pieces
and the `GiantBloodSack` item stay out of its reach. The giant pieces need
that name the same way: the intact one only carries a `Destructible`, the
broken one a `MineRock5` like a plain stone. Rocks hands every one of those
names over to Ores, which is what keeps a copper vein out of the rocks while
`MineRock_Stone` and the broken rock pieces stay in.

The name rules can also be regular expressions: `NameMatches` claims the names
a pattern describes, `NameExcludesMatches` gives them away to another
category — the pattern twin of `NameContains` and `NameExcludes`. Both are
case insensitive and **searched** in the name, so `^` and `$` anchor a pattern
that has to cover the whole name, and they are compiled once and reused: the
name rules are asked for every object of the scene, on every scan. A pattern
that does not compile is written to the BepInEx log and matches nothing.

**Spawners** is backed by the game's own spawner components: `CreatureSpawner`
for the spawners, `SpawnArea` for the nests, the piles and the evil
hearts, `TriggerSpawner` for the holes that open when you come close. A modded
spawner carrying none of them is caught by its name instead: the file gives
the category a prefix, taken from the start of the name, so the model pieces
and the effects whose name merely contains that word are never
claimed. `SpawnSystem` is deliberately left out — it sits on the zone
controller, not on a spawner.

Any object that does not belong to those groups automatically creates a
"name" category (every object sharing the same name).

**Grass / vegetation** works the same way: the game draws the instanced grass
with `InstanceRenderer` (the `Renderer`s of the patches are inactive), so the
category is backed by that component; the bushes, ferns, mushrooms and
isolated plants are caught by name — the substrings live in *The name rules
file* below; and everything the clutter system spawns is
grouped under its own `grassroot` object. That root matters: the
`ClutterSystem` component itself lives on the root of the scene, hiding it
would take the clouds, the sea and the weather down with the grass.

**Locations** covers the structures the game places on its own: a dungeon
entrance lives below a `LocationProxy` — the object that spawns the location
when you come close — and that proxy carries none of the components above, so
the entrance survives every other category, **Hide all** included; the
locations themselves (huts, ruins, boss rooms, ...) carry `Location`. Both
components belong to this category, which is listed last: a known component
decides the category, so the trees, the rocks and the build pieces still
claim their own objects first.

## Installation

**Client-side only** — nothing to install on the server.

### With a mod manager (recommended)
Vortex, r2modman or Gale.

### Manually
1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
   and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Copy `XRays.dll`, `YamlDotNet.dll` and `orax.XRays.names.yaml` into
   `Steam\steamapps\common\Valheim\BepInEx\plugins\`.

## Configuration

All options live in `BepInEx\config\orax.XRays.cfg` (created on the first
launch, also editable in game through the Configuration Manager — a separate
mod, its window opens with `F1`):

The name rules of the categories have a file of their own: it ships in
`BepInEx\plugins`, is copied into `BepInEx\config` at launch, and which file it
is, is the `Files` option of the table below — described at the end of this
section:

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
| `Files` | `Name rules file` | `orax.XRays.names.yaml` | Name of the YAML file holding the name rules of the categories: the mod looks for it in `BepInEx\plugins`, beside its own assembly and in `BepInEx\scripts`, and copies it into `BepInEx\config` when it is missing there or written for another version of the mod (the copy of `BepInEx\config` is the file read) |
| `General` | `Enable` | `true` | Master switch: unchecked, everything hidden is shown again, the panel closes and the mod stops completely (hotkeys and periodic work included); checked back, the panel options come back as they were — the objects picked one by one and the categories taken by name are forgotten |
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
| `Display` | `Panel height` | `750` | Vertical size of the panel (360 to 1200 pixels); the rows scroll when they do not fit in it |
| `Display` | `Show tooltips` | `true` | Shows the little box describing a row while the pointer rests on it; off, no box ever appears (same thing as the **Show tooltips** row of the panel) |
| `Refresh` | `Update interval` | `2` | Seconds between two passes recomputing everything that must stay hidden (the periodic sweep of the scene); a longer wait costs less CPU, and objects appearing later (a spawn, a new construction) are picked up later. The renderers below a hidden root are collected again once every ten passes, so a renderer the game adds below a root is hidden at the next collection |
| `Refresh` | `Name scan interval` | `4` | Seconds the result of an object-name scan is kept before the scene is swept again for the name rules; a longer wait costs less CPU, and the objects a name rule catches later are picked up at the next scan |
| `Logging` | `Log cursor info` | `false` | Logs the panel's "Under the cursor" text, only while the panel is on screen |
| `Logging` | `Log reconcile timing` | `false` | Times the phases of the periodic hiding pass: one line per second while options are hidden, with the average, the smallest and the biggest value (milliseconds) |
| `Setup` | `Saved setup` | _empty_ | The options written with the **Save setup** button, as a comma separated list of names (`terrain`, `water`, `clouds`, `mist`, `player`, `hud` and the category keys), brought back with **Restore setup** (never applied at startup). Edited by hand only to force a setup |

### The name rules file

What a category claims by name — the substrings, the prefixes and the
regular expressions matched against the name of a game object, and the names
it gives away to another category — lives in a file of its own **and in that
file only**: the code holds no name rule at all. The mod ships it as
`BepInEx\plugins\orax.XRays.names.yaml`, looked for there, then beside its own
assembly, then in `BepInEx\scripts`, and copies it into `BepInEx\config` at
launch; the copy of `BepInEx\config` is the file read, and read again every
time it changes while the game runs. Which file it is, is the `Files` →
`Name rules file` option of the table above.

The mod writes its own version on the `version` line of the file, so the copy
is **replaced by the shipped one** when it is missing or written for another
version of the mod. Keep that line as it is when the file is edited — an
edited file has to be copied aside before the mod is updated, or it goes with
it — and deleting the copy simply brings the shipped file back. A file
holding a couple of categories could look like this:

```yaml
version: 0.1.0
ores:
  nameContains:
    - '_copper'
    - 'silver'
  nameMatches:
    - '^giant_(ribs|skull|sword)'
rocks:
  nameExcludes:
    - '_copper'
    - 'silver'
```

The five lists of a category, every one of them matched against the object
name:

| List | What it does |
| --- | --- |
| `nameContains` | case-insensitive substrings the category claims |
| `nameStartsWith` | case-insensitive prefixes it claims |
| `nameMatches` | regular expressions it claims, **searched** in the name (`^` and `$` anchor a pattern covering the whole name) |
| `nameExcludes` | case-insensitive substrings given away to another category, which is how the ore veins are kept out of the rocks |
| `nameExcludesMatches` | regular expressions given away the same way |

The file is the **only source**: a list it does not carry is empty, and a
category it does not carry has no name rule at all. Categories are named by
their key — `trees`, `ores`, `rocks`, `pickable`, `build`, `creatures`,
`items`, `plants`, `spawners`, `clutter`, `locations` — and a key naming none
of them is written to the log, it is usually a typo.

A list belongs to one category only: telling `ores` what it claims does not
keep `rocks` from claiming the very same names, so both sides have to be
written — which is what `nameExcludes` is for.

Write the values between **single quotes**: nothing is escaped in them, so a
regular expression keeps its backslash, `'^Rock_\d+$'` written once. Between
double quotes a backslash has to be written twice (`"^Rock_\\d+$"`), and
`"\d"` is a parse error; a value written without quotes at all is read
literally too, but a ` #` inside it starts a comment and cuts it short. The
name matched is the raw name of the game object, `Rock_3(Clone)` included
when the game clones one.

A change is applied **while the game runs** (the date of the file is looked
at twice a second): the categories keep a name scan for a couple of seconds,
then the objects follow at the next reconciliation — what the checked
categories claim is hidden, the rest comes back, and the objects picked one
by one are left alone. A file that does not parse is written to the log and
changes nothing, the rules already in force stay.

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

---

*This mod was generated by AI: its code, its documentation and its in-game
text were written by a large language model at the request of its author.*
