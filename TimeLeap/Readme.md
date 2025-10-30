# TimeLeap Tools

Comprehensive extraction and repacking utilities for the visual novel **[Time Leap](https://vndb.org/v759)**.  
Supports both **PC (Japanese)** and **Xbox 360** releases.

---

## 📦 Overview

Time Leap uses custom archive formats to store game resources including graphics, scripts, and audio:
- **PC version**: `.dat` archives with end-of-file index tables
- **Xbox 360 version**: `.pak` archives with header-aligned structures

These tools enable full extraction, inspection, and repacking while preserving original file structures and compatibility.

---

## 🎮 Platform Support

### PC (Japanese VN)
- Archive format: `.dat`
- Index location: End of file
- Obfuscation: Nibble-swapped with XOR encoding
- Audio: Standard WAV/OGG

### Xbox 360
- Archive format: `.pak`
- Index location: Header section (2048-byte aligned)
- Obfuscation: None
- Audio: XMA2 (Xbox 360 native codec)

---

## ✨ Features

### Common Features
- ✅ Full extraction and repacking of archives
- ✅ Maintains original file order and offsets
- ✅ Automatic file type detection
- ✅ Deterministic builds for reproducibility
- ✅ Cross-platform Python and C# implementations

### PC-Specific
- ✅ Handles nibble-swapped obfuscation
- ✅ Decodes XOR-protected index entries
- ✅ Preserves 80-byte entry format

### Xbox 360-Specific
- ✅ 2048-byte header alignment
- ✅ Identifies XMA2 audio streams
- ✅ Maintains engine-required padding

---

## 📁 Archive Structures

### PC `.dat` Format

```
┌─────────────────────┐
│   Data Section      │  ← File blobs (sequential)
│   (File 1, 2, 3...) │
├─────────────────────┤
│   Index Table       │  ← 80-byte entries (obfuscated)
│   (Entry 1, 2, 3...)│
├─────────────────────┤
│   File Count        │  ← 4 bytes at EOF
└─────────────────────┘
```

**Index Entry (80 bytes, deobfuscated):**
- Filename (null-padded string up to 48 bytes)
- Offset (32-bit unsigned integer)
- Reserved fields
- Size (32-bit unsigned integer)
- Additional reserved fields

**Obfuscation Process:**
1. Nibble-swap each byte (swap high/low 4 bits)
2. Apply XOR with hardcoded key table
3. Result is stored in archive

### Xbox 360 `.pak` Format

```
┌─────────────────────┐
│   Header Section    │  ← Aligned to 2048 bytes
│   + File Index      │
├─────────────────────┤
│   Data Section      │  ← Sequentially packed files
│   (File 1, 2, 3...) │
└─────────────────────┘
```

**Structure:**
- Header and index aligned to 2048-byte boundaries
- No obfuscation
- File data follows header section
- Preserves original offsets for binary compatibility

---

## 🔍 File Format Notes

### PC Assets
- **PNG**: Standard format with embedded metadata (EXIF, Photoshop chunks)
- **JPG**: JPEG with JFIF headers and comment metadata
- **Audio**: Standard WAV/OGG formats

### Xbox 360 Assets
- **PNG**: Photoshop-generated with embedded metadata
- **JPG**: JPEG with JFIF headers and nonstandard EXIF fields
- **WAV**: XMA2-encoded audio (WAV container with XMA2 data)

### XMA2 Audio (Xbox 360)

XMA2 is Microsoft's proprietary audio codec derived from WMA Pro, native to Xbox 360.

**Conversion Tools:**

| Tool | Direction | Description |
|------|-----------|-------------|
| `xmaencode.exe` | WAV → XMA2 | Official Microsoft encoder (XDK) |
| `towav.exe` | XMA2 → WAV | Decodes XMA to standard PCM WAV |
| `ffmpeg` | XMA2 ↔ WAV | Requires `xma` support (testing only) |

> ⚠️ **Note**: Extracted `.wav` files are currently preserved as-is. Future versions will support automatic re-encoding via `xmaencode`.

---

## ⚙️ Usage

### Python Tools

#### Extraction

```bash
# PC (Japanese) - Extract .dat archive
python extract_dat.py GAME.DAT ./output/

# Xbox 360 - Extract .pak archive
python extract_pak.py GAME.PAK ./output/
```

#### Repacking

```bash
# PC (Japanese) - Repack .dat archive
python pack_dat.py ./output/ new_GAME.DAT

# Xbox 360 - Repack .pak archive
python pack_pak.py ./output/ new_GAME.PAK
```

### Unified C# Tool

```bash
# Extract
TimeLeap.exe extract GAME.DAT ./output/
TimeLeap.exe extract GAME.PAK ./output/

# Repack
TimeLeap.exe pack ./output/ new_GAME.DAT
TimeLeap.exe pack ./output/ new_GAME.PAK
```

The tool automatically detects archive type based on file extension and structure.

---

## 🔧 Implementation Details

### PC `.dat` Decoding Process

1. **Read file count** from the last 4 bytes of the archive
2. **Seek to index table** (located before file count)
3. **Read index entries** (80 bytes × file count)
4. **Deobfuscate entries**:
   - Reverse nibble-swap on each byte
   - Apply XOR decoder with embedded key table
5. **Parse fields**: filename, offset, size, reserved
6. **Extract files** using parsed offsets and sizes

### PC `.dat` Encoding Process

1. **Sort files** for deterministic ordering
2. **Write data section** (sequential file blobs)
3. **Construct index entries** (80 bytes each):
   - Null-pad filenames to fit
   - Calculate offsets and sizes
   - Fill reserved fields
4. **Obfuscate index**:
   - Apply XOR encoding
   - Apply nibble-swap
5. **Write index table** after data section
6. **Write file count** at EOF
7. **Validate** by re-reading index

### Xbox 360 `.pak` Process

**Extraction:**
1. Read header and parse file index
2. Extract files using offsets from index
3. Detect file types (PNG, JPG, XMA2)

**Repacking:**
1. Sort entries for deterministic builds
2. Calculate header size with 2048-byte alignment
3. Write header section with file index
4. Pad to alignment boundary
5. Write file data sequentially
6. Preserve original offsets where possible

---

## 🧪 Technical Caveats

### PC `.dat` Format
- **Nibble-swapping is critical** — incorrect order corrupts names and offsets
- **XOR table is hardcoded** — do not modify for compatibility
- **80-byte entries are fixed** — filenames must fit or be truncated
- **Reserved fields must be preserved** — engine may depend on them

### Xbox 360 `.pak` Format
- **2048-byte alignment is required** — engine will not load unaligned archives
- **File order matters** — maintain extraction order for repacking
- **XMA2 audio requires special handling** — standard tools may not process correctly

---

## 🛠️ Tool Compatibility

Both Python and C# implementations:
- Use identical decoding tables
- Produce interchangeable archives
- Support bidirectional extraction/repacking
- Maintain deterministic output

You can extract with Python and repack with C#, or vice versa.

---

## 📜 License & Purpose

These tools are provided for:
- **Research** — understanding proprietary formats
- **Preservation** — archiving game assets
- **Localization** — translating and modding

**You must own a legitimate copy of the game to use these tools.**

---

## 🤝 Contributing

When contributing:
- Maintain compatibility with existing archives
- Document format discoveries
- Preserve obfuscation algorithms
- Test with both PC and Xbox 360 versions

---

## 📚 Resources

- [VNDB Entry for Time Leap](https://vndb.org/v759)
- XMA2 documentation (Microsoft XDK)
- Original game (PC/Xbox 360)

---

*Last updated: 2025*