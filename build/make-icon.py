#!/usr/bin/env python3
"""Write a multi-resolution PNG-in-ICO icon for FolderScope."""

import struct
import zlib
from pathlib import Path


def chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def png(width: int, height: int, rgba: bytes) -> bytes:
    raw = bytearray()
    stride = width * 4
    for y in range(height):
        raw.append(0)
        raw.extend(rgba[y * stride : (y + 1) * stride])
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")


def blend(dst: bytearray, index: int, color: tuple[int, int, int, int]) -> None:
    sr, sg, sb, sa = color
    if sa <= 0:
        return
    if sa >= 255:
        dst[index : index + 4] = bytes(color)
        return
    dr, dg, db, da = dst[index : index + 4]
    inv = 255 - sa
    dst[index] = (sr * sa + dr * inv) // 255
    dst[index + 1] = (sg * sa + dg * inv) // 255
    dst[index + 2] = (sb * sa + db * inv) // 255
    dst[index + 3] = min(255, sa + da * inv // 255)


def draw(size: int) -> bytes:
    bg = (18, 53, 47, 255)
    bars = (
        (244, 201, 93, 255),
        (127, 207, 182, 255),
        (232, 245, 241, 255),
    )
    buf = bytearray(size * size * 4)
    radius = max(2, size // 6)
    for y in range(size):
        for x in range(size):
            dx = 0
            if x < radius:
                dx = radius - x
            elif x >= size - radius:
                dx = x - (size - radius - 1)
            dy = 0
            if y < radius:
                dy = radius - y
            elif y >= size - radius:
                dy = y - (size - radius - 1)
            if dx * dx + dy * dy > radius * radius:
                continue
            blend(buf, (y * size + x) * 4, bg)

    margin = max(2, int(size * 0.18))
    gap = max(1, int(size * 0.07))
    bar_h = max(2, int(size * 0.12))
    widths = (0.78, 0.46, 0.62)
    inner = size - margin * 2
    group = bar_h * 3 + gap * 2
    top = (size - group) // 2
    for i, (width_ratio, color) in enumerate(zip(widths, bars)):
        w = max(2, int(inner * width_ratio))
        y = top + i * (bar_h + gap)
        for yy in range(y, min(size, y + bar_h)):
            for xx in range(margin, min(size, margin + w)):
                blend(buf, (yy * size + xx) * 4, color)
    return png(size, size, bytes(buf))


def ico(images: list[tuple[int, bytes]]) -> bytes:
    header = struct.pack("<HHH", 0, 1, len(images))
    entries = bytearray()
    payload = bytearray()
    offset = 6 + 16 * len(images)
    for size, data in images:
        stored = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", stored, stored, 0, 0, 1, 32, len(data), offset)
        payload += data
        offset += len(data)
    return header + bytes(entries) + bytes(payload)


def main() -> None:
    sizes = (16, 24, 32, 48, 64, 256)
    images = [(size, draw(size)) for size in sizes]
    destination = Path(__file__).resolve().parents[1] / "src" / "FolderScope.App" / "app.ico"
    destination.write_bytes(ico(images))
    print(f"wrote {destination} ({destination.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
