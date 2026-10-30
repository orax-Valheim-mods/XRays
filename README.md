Hides the terrain, the water, the clouds, the mist or world elements so you can see through them:
trees, rocks, resources, build pieces, creatures, ground items, etc.

# Demo

# Images

![](https://i.imgur.com/NN8l7RU.jpeg)

![](https://i.imgur.com/PEPsAvw.jpeg)

![](https://i.imgur.com/LcCmlct.jpeg)

![](https://i.imgur.com/q2rP0xt.jpeg)

## Video

https://youtu.be/JYvS0azG44U

# Usage

Open the GUI with `F8` (configurable) or click on `XRAYS` on the new Pause menu entry.

Notes: The categories (Build pieces, Creatures, Crops, Grass, etc.) aim to classify in-game
elements. However, some items may end up in the wrong category or might not be assigned to any
category at all.

## Installation

**Client-side only** — nothing to install on the server.

### With a mod manager (recommended)

Install the mod with Vortex or Gale.

### Manually

1. Install BepInExPack for Valheim and Jötunn.
2. Copy `XRays.dll` and `orax.XRays.names.yaml` into
   `Steam\steamapps\common\Valheim\BepInEx\plugins\`.

## Configuration

All options live in `BepInEx\config\orax.XRays.cfg` (created on the first launch, also editable in
game through the Configuration Manager — a separate mod, its window opens with `F1`).

### The name rules file

What a category claims by name — the substrings, the prefixes and the regular expressions matched
against the name of a game object, and the names it gives away to another category — lives in a file
of its own **and in that file only**: the code holds no name rule at all. The mod ships it as
`BepInEx\plugins\orax.XRays.names.yaml`, looked for there, then beside its own assembly, then in
`BepInEx\scripts`, and copies it into `BepInEx\config` at launch; the copy of `BepInEx\config` is
the file read, and read again every time it changes while the game runs. Which file it is, is the
`Files` → `Name rules file` option of the table above.

A file holding a couple of categories could look like this:

```yaml
version: 0.1.0
ores:
  nameContains:
    - "_copper"
    - "silver"
  nameMatches:
    - "^giant_(ribs|skull|sword)"
rocks:
  nameExcludes:
    - "_copper"
    - "silver"
```

The five lists of a category, every one of them matched against the object name:

| List                  | What it does                                                                                                     |
| --------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `nameContains`        | case-insensitive substrings the category claims                                                                  |
| `nameStartsWith`      | case-insensitive prefixes it claims                                                                              |
| `nameMatches`         | regular expressions it claims, **searched** in the name (`^` and `$` anchor a pattern covering the whole name)   |
| `nameExcludes`        | case-insensitive substrings given away to another category, which is how the ore veins are kept out of the rocks |
| `nameExcludesMatches` | regular expressions given away the same way                                                                      |

The file is the **only source**: a list it does not carry is empty, and a category it does not carry
has no name rule at all. Categories are named by their key — `trees`, `ores`, `rocks`, `pickable`,
`build`, `creatures`, `items`, `plants`, `spawners`, `clutter`, `locations` — and a key naming none
of them is written to the log, it is usually a typo.

A list belongs to one category only: telling `ores` what it claims does not keep `rocks` from
claiming the very same names, so both sides have to be written — which is what `nameExcludes` is
for.

Write the values between **single quotes**: nothing is escaped in them, so a regular expression
keeps its backslash, `'^Rock_\d+$'` written once. Between double quotes a backslash has to be
written twice (`"^Rock_\\d+$"`), and `"\d"` is a parse error; a value written without quotes at all
is read literally too, but a ` #` inside it starts a comment and cuts it short. The name matched is
the raw name of the game object, `Rock_3(Clone)` included when the game clones one.

# Misc.

GitHub repositories of my Valheim mods: https://github.com/orax-Valheim-mods/
