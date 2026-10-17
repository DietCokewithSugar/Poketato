#!/usr/bin/env python3
"""Subset the Fusion Pixel Font (OFL-1.1) to the characters the Unity project actually uses.

The full zh_hans TTF is ~7 MB; WebGL ships fonts inside the .data file, so we keep printable ASCII plus every
non-ASCII character found in the C# sources and the sprite metadata.

Usage: python3 tools/subset_font.py /path/to/fusion-pixel-12px-proportional-zh_hans.ttf
"""
import sys
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parent.parent
UNITY = ROOT / "unity" / "Assets"
OUT = UNITY / "Resources" / "Fonts" / "FusionPixel12.ttf"


def used_chars() -> set:
    chars = {chr(c) for c in range(0x20, 0x7F)}
    for path in list(UNITY.rglob("*.cs")) + [UNITY / "Resources" / "pokemon_sprites.json"]:
        for ch in path.read_text(encoding="utf-8"):
            if ord(ch) > 0x7F:
                chars.add(ch)
    return chars


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    src = Path(sys.argv[1])
    chars = used_chars()
    font = TTFont(src)
    cmap = font.getBestCmap()
    missing = sorted(c for c in chars if ord(c) not in cmap)
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.notdef_outline = True
    sub = subset.Subsetter(opts)
    sub.populate(text="".join(chars))
    sub.subset(font)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    font.save(OUT)
    print(f"{len(chars)} chars -> {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB)")
    if missing:
        print("not in font:", " ".join(missing))


if __name__ == "__main__":
    main()
