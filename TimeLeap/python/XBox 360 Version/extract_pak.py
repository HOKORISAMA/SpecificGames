#!/usr/bin/env python3
"""
PAK Extractor Tool (Xbox)
-----------------------------------------

Enhancements:
-------------
✅ Detects embedded file types via magic bytes or PAK basename.
✅ Recognizes WAV files that use Xbox Media Audio 2 (XMA2 codec).
✅ Marks XMA2 files with .xma extension for clarity.
✅ Includes strong safety checks and clear logs.

Dependencies:
-------------
- Python 3.8+
"""

import os
import sys
import struct
from dataclasses import dataclass


# ---------------------------------------------------------------------------
# 🧩 File entry data structure
# ---------------------------------------------------------------------------
@dataclass
class Entry:
    offset: int
    size: int


# ---------------------------------------------------------------------------
# 🧩 Common magic byte signatures
# ---------------------------------------------------------------------------
SIGNATURES = {
    b"\x89PNG\r\n\x1a\n": ".png",
    b"BM": ".bmp",
    b"RIFF": ".wav",
    b"OggS": ".ogg",
    b"\xFF\xD8\xFF": ".jpg"
}


# ---------------------------------------------------------------------------
# 🧠 Detect file type from header and context
# ---------------------------------------------------------------------------
def detect_file_type(data, pak_name):
    """Detect type using header first, then PAK name context."""
    for magic, ext in SIGNATURES.items():
        if data.startswith(magic):
            return ext

    # Try guessing from pak filename if header detection fails
    lower_name = pak_name.lower()
    if "cg" in lower_name:
        return ".png"
    elif "se" in lower_name or "snd" in lower_name:
        return ".wav"
    elif "text" in lower_name or "txt" in lower_name or "scr" in lower_name:
        return ".txt"
    else:
        return ".bin"


# ---------------------------------------------------------------------------
# 🧩 Detect if a WAV file contains XMA2 audio
# ---------------------------------------------------------------------------
def is_xma2_wave(data):
    """
    Checks for codec_tag=0x0166 ("f[1][0][0]" in ffprobe)
    XMA2 typically uses WAVE format with WAVEFORMATEX wFormatTag = 0x0166.
    """
    if not data.startswith(b"RIFF"):
        return False

    # Search for "fmt " chunk and read format tag
    idx = data.find(b"fmt ")
    if idx == -1:
        return False

    try:
        w_format_tag = struct.unpack_from("<H", data, idx + 8)[0]
        return w_format_tag == 0x0166
    except struct.error:
        return False


# ---------------------------------------------------------------------------
# 🧩 Parse PAK index table
# ---------------------------------------------------------------------------
def parse_index(f):
    """Read file count and offsets from start of .pak"""
    f.seek(0)
    header = f.read(4)
    if len(header) < 4:
        raise ValueError("Invalid PAK: too small for header")

    file_count = struct.unpack("<I", header)[0]
    if file_count <= 0 or file_count > 100000:
        raise ValueError(f"Invalid file count: {file_count}")

    offsets = [struct.unpack("<I", f.read(4))[0] for _ in range(file_count)]
    offsets.append(os.path.getsize(f.name))

    entries = []
    for i in range(file_count):
        start, end = offsets[i], offsets[i + 1]
        size = end - start
        if size < 0:
            raise ValueError(f"Negative size at entry {i}: start={start}, end={end}")
        entries.append(Entry(start, size))
    return entries


# ---------------------------------------------------------------------------
# 📦 Extract and process all files
# ---------------------------------------------------------------------------
def extract_pak(pak_path, out_dir):
    pak_name = os.path.basename(pak_path)
    base_name, _ = os.path.splitext(pak_name)
    os.makedirs(out_dir, exist_ok=True)

    with open(pak_path, "rb") as f:
        entries = parse_index(f)

        for i, entry in enumerate(entries):
            f.seek(entry.offset)
            data = f.read(entry.size)

            ext = detect_file_type(data[:64], pak_name)
            
            # Check if it's an XMA2 file and adjust extension
            if ext == ".wav" and is_xma2_wave(data):
                ext = ".xma"
                print(f"ℹ️ XMA2 audio detected at entry {i}")
            
            out_name = f"{base_name}_{i:04d}{ext}"
            out_path = os.path.join(out_dir, out_name)

            # Write extracted data as-is
            with open(out_path, "wb") as out_f:
                out_f.write(data)
            print(f"✅ {out_name}: {entry.size} bytes")

    print(f"\n✅ Done! Extracted {len(entries)} entries from {pak_name}")


# ---------------------------------------------------------------------------
# 🚀 CLI
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: extract_pak.py <pakfile> <outdir>")
        sys.exit(1)

    pak_path, out_dir = sys.argv[1], sys.argv[2]
    if not os.path.isfile(pak_path):
        print(f"Error: File not found -> {pak_path}")
        sys.exit(1)

    extract_pak(pak_path, out_dir)