#!/usr/bin/env python3
"""
PAK Packer Tool (QLie / Custom Engine)
-----------------------------------------

Packs extracted raw files (including XMA2 audio) into a PAK archive.

Features:
---------
✅ Packs all files from a directory into a PAK archive.
✅ Sorts files by name for deterministic results.
✅ Sets first file offset = 2048 (0x800).
✅ Writes end offset (fileCount + 1 entries total).
✅ Simple — no audio conversion, all files packed raw.

Usage:
------
python pack_pak.py <input_dir> <output_pak>
"""

import os
import sys
import struct
from pathlib import Path


# ---------------------------------------------------------------------------
# 📦 Pack files into PAK archive
# ---------------------------------------------------------------------------
def pack_pak(input_dir, output_pak):
    """Pack all files from input_dir into a PAK archive with 2048-byte-aligned first file."""

    if not os.path.isdir(input_dir):
        raise ValueError(f"Input directory not found: {input_dir}")

    # Collect and sort files
    all_files = sorted([f for f in Path(input_dir).rglob('*') if f.is_file()])
    if not all_files:
        raise ValueError(f"No files found in {input_dir}")

    file_count = len(all_files)
    print(f"\n📦 Packing {file_count} files...")

    FIRST_FILE_OFFSET = 0x800  # 2048 bytes

    file_data = []
    offsets = []
    current_offset = FIRST_FILE_OFFSET

    # Prepare file data and offsets
    for file_path in all_files:
        with open(file_path, 'rb') as f:
            data = f.read()
        file_data.append(data)
        offsets.append(current_offset)
        current_offset += len(data)
        print(f"  {file_path.name}: {len(data)} bytes")

    # Add the final "end offset" (fileCount + 1 entries total)
    offsets.append(current_offset)

    # -----------------------------------------------------------------------
    # Write header: [count][offsets...]
    # -----------------------------------------------------------------------
    with open(output_pak, 'wb') as pak:
        pak.write(struct.pack('<I', file_count))
        for off in offsets:
            pak.write(struct.pack('<I', off))

        # Pad header up to FIRST_FILE_OFFSET (0x800)
        pad_len = FIRST_FILE_OFFSET - pak.tell()
        if pad_len < 0:
            raise RuntimeError(f"Header too large ({pak.tell()} bytes) to fit before 0x800 boundary.")
        if pad_len > 0:
            pak.write(b'\x00' * pad_len)

        # Write file data sequentially
        for data in file_data:
            pak.write(data)

    total_size = os.path.getsize(output_pak)
    print(f"\n✅ Successfully created PAK archive!")
    print(f"   Output: {output_pak}")
    print(f"   Files: {file_count}")
    print(f"   First file offset: 0x{FIRST_FILE_OFFSET:X}")
    print(f"   End offset: 0x{current_offset:X}")
    print(f"   Total size: {total_size:,} bytes")


# ---------------------------------------------------------------------------
# 🔍 Verify PAK integrity
# ---------------------------------------------------------------------------
def verify_pak(pak_path):
    """Quick verification of PAK structure."""
    try:
        with open(pak_path, 'rb') as f:
            file_count = struct.unpack('<I', f.read(4))[0]
            offsets = [struct.unpack('<I', f.read(4))[0] for _ in range(file_count + 1)]
            pak_size = os.path.getsize(pak_path)

            print(f"\n🔎 Verifying: {pak_path}")
            print(f"  File count: {file_count}")
            print(f"  First offset: 0x{offsets[0]:X}")
            print(f"  End offset: 0x{offsets[-1]:X} (should match file size ~0x{pak_size:X})")

            if offsets[0] != 0x800:
                print(f"⚠️  Warning: first offset != 0x800")
            if offsets[-1] != pak_size:
                print(f"⚠️  End offset mismatch: header says {offsets[-1]}, file size is {pak_size}")
                return False

        print("✓ PAK structure verified OK")
        return True
    except Exception as e:
        print(f"❌ Verification failed: {e}")
        return False


# ---------------------------------------------------------------------------
# 🚀 CLI Entry
# ---------------------------------------------------------------------------
if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: pack_pak.py <input_dir> <output_pak>")
        print("\nExample:")
        print("  python pack_pak.py ./extracted_files output.pak")
        sys.exit(1)

    input_dir, output_pak = sys.argv[1], sys.argv[2]
    try:
        pack_pak(input_dir, output_pak)
        verify_pak(output_pak)
    except Exception as e:
        print(f"\n❌ Error: {e}")
        sys.exit(1)
