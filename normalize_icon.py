"""Build a multi-resolution app.ico from the largest frame in a source .ico."""
from __future__ import annotations

import struct
import sys
from io import BytesIO
from pathlib import Path

from PIL import Image

SIZES = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def _frames_via_struct(path: Path) -> list[Image.Image]:
    data = path.read_bytes()
    if len(data) < 6:
        return []

    _, id_type, count = struct.unpack_from("<HHH", data, 0)
    if id_type != 1 or count == 0:
        return []

    frames: list[Image.Image] = []
    offset = 6
    for _ in range(count):
        width, height, _, _, _, _, size, image_offset = struct.unpack_from("<BBBBHHII", data, offset)
        offset += 16
        width = 256 if width == 0 else width
        height = 256 if height == 0 else height
        blob = data[image_offset:image_offset + size]
        frames.append(Image.open(BytesIO(blob)).convert("RGBA"))

    return frames


def largest_frame(path: Path) -> Image.Image:
    frames = _frames_via_struct(path)
    if not frames:
        img = Image.open(path)
        try:
            for i in range(getattr(img, "n_frames", 1)):
                img.seek(i)
                frames.append(img.copy())
        except EOFError:
            pass
        if not frames:
            frames = [img.copy()]

    master = max(frames, key=lambda im: im.width * im.height)
    if master.mode != "RGBA":
        master = master.convert("RGBA")
    return master


def normalize_icon(src: Path, dst: Path) -> None:
    master = largest_frame(src)
    master.save(dst, format="ICO", sizes=SIZES)
    print(f"Icon normalized: {dst} ({dst.stat().st_size} bytes) from {master.width}x{master.height}")


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: normalize_icon.py <source.ico> <dest.ico>", file=sys.stderr)
        return 2

    src = Path(sys.argv[1]).resolve()
    dst = Path(sys.argv[2]).resolve()
    if not src.is_file():
        print(f"Source introuvable: {src}", file=sys.stderr)
        return 1

    normalize_icon(src, dst)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
