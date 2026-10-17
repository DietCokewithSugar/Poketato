#!/usr/bin/env python3
"""Import the MVP roster's PMD sprite sheets from a poke-rouge checkout into the Unity project.

poke-rouge keeps one PNG per animation (columns = frames, rows = 8 directions) plus the frame
metadata in src/client/gfx/spriteMeta.ts. Unity draws every instance of a species with one
material, so each species is packed into a single atlas and the frame rectangles are written to
Assets/Resources/pokemon_sprites.json.

Usage: python3 tools/import_sprites.py /path/to/poke-rouge
"""
import json
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
UNITY = ROOT / "unity"
ART_DIR = UNITY / "Assets" / "Art" / "Pokemon"
META_OUT = UNITY / "Assets" / "Resources" / "pokemon_sprites.json"
CREDITS_OUT = ART_DIR / "CREDITS.md"

# dex -> id. Four playable lines from poke-rouge's hand-tuned starters, six wild Pokemon from waves 1-5.
ROSTER = {
    4: "charmander",
    7: "squirtle",
    1: "bulbasaur",
    25: "pikachu",
    10: "caterpie",
    19: "rattata",
    16: "pidgey",
    41: "zubat",
    43: "oddish",
    15: "beedrill",
}
ANIMS = ["Idle", "Walk", "Attack", "Shoot", "Hurt", "Faint"]
MAX_W = 2048


def round4(v: int) -> int:
    return (v + 3) // 4 * 4


def load_meta(src: Path) -> dict:
    text = (src / "src/client/gfx/spriteMeta.ts").read_text(encoding="utf-8")
    m = re.search(r"SPRITE_META: Record<number, SpriteMeta> = (\{.*?\});\s*$", text, re.S | re.M)
    if not m:
        sys.exit("could not find SPRITE_META in spriteMeta.ts")
    return json.loads(m.group(1))


def pack(blocks):
    """Shelf-pack (name, image) blocks; returns positions, width, height."""
    blocks = sorted(blocks, key=lambda b: -b[1].height)
    x = y = shelf_h = 0
    width = 0
    pos = {}
    for name, im in blocks:
        if x + im.width > MAX_W:
            x, y, shelf_h = 0, y + shelf_h, 0
        pos[name] = (x, y)
        x += im.width
        shelf_h = max(shelf_h, im.height)
        width = max(width, x)
    return pos, round4(width), round4(y + shelf_h)


def credits_for(src: Path, dexes) -> str:
    rows = []
    for line in (src / "CREDITS.md").read_text(encoding="utf-8").splitlines():
        if not line.startswith("| ") or line.startswith("| 画师") or line.startswith("|---"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if len(cells) < 3:
            continue
        used = {int(n) for n in re.findall(r"\d+", cells[2])}
        hit = sorted(used & set(dexes))
        if hit:
            rows.append(f"| {cells[0]} | {cells[1]} | {', '.join(map(str, hit))} |")
    head = (
        "# Pokemon sprite credits\n\n"
        "Sprites come from [PMDCollab / SpriteCollab](https://github.com/PMDCollab/SpriteCollab), "
        "licensed [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) (non-commercial, attribution required). "
        "They were copied from poke-rouge's `public/sprites/` and repacked into one atlas per species by "
        "`tools/import_sprites.py`. Pokémon names and characters are trademarks of Nintendo / Creatures Inc. / "
        "GAME FREAK / The Pokémon Company.\n\n"
        "| Artist | Contact | Dex numbers used here |\n|---|---|---|\n"
    )
    return head + "\n".join(rows) + "\n"


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    src = Path(sys.argv[1]).resolve()
    meta = load_meta(src)
    dex_names = {}
    for line in (src / "src/shared/dex.ts").read_text(encoding="utf-8").splitlines():
        for m in re.finditer(r"\[(\d+), '([^']+)', '([a-z/]+)'", line):
            dex_names[int(m.group(1))] = (m.group(2), m.group(3).split("/"))

    ART_DIR.mkdir(parents=True, exist_ok=True)
    META_OUT.parent.mkdir(parents=True, exist_ok=True)
    out = {"species": []}
    for dex, sid in ROSTER.items():
        m = meta[str(dex)]
        blocks = []
        for anim in ANIMS:
            if anim in m["a"]:
                blocks.append((anim, Image.open(src / f"public/sprites/{dex}/{anim}.png").convert("RGBA")))
        pos, w, h = pack(blocks)
        atlas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        anims = []
        for name, im in blocks:
            atlas.paste(im, pos[name])
            a = m["a"][name]
            anims.append({
                "name": name, "x": pos[name][0], "y": pos[name][1],
                "w": a["w"], "h": a["h"], "frames": a["n"], "rows": a["r"],
                "durations": a["d"], "ax": a["ax"], "ay": a["ay"],
            })
        fname = f"{dex:03d}_{sid}.png"
        atlas.save(ART_DIR / fname, optimize=True)
        cn, types = dex_names[dex]
        out["species"].append({
            "dex": dex, "id": sid, "nameCn": cn, "types": types, "texture": fname[:-4],
            "atlasW": w, "atlasH": h, "shadow": m["s"], "hover": bool(m["hv"]), "anims": anims,
        })
        print(f"{fname}: {w}x{h} ({len(anims)} anims)")
    META_OUT.write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    CREDITS_OUT.write_text(credits_for(src, ROSTER.keys()), encoding="utf-8")
    print(f"wrote {META_OUT.relative_to(ROOT)} and {CREDITS_OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
