# 🕒 TimeLeap Tools

Tools and utilities for working with the visual novel **[Time Leap](https://vndb.org/v759)**  
(PC / Xbox 360 versions)

This project enables **extraction**, **modification**, and **repacking** of the game’s archive formats for research, translation, and modding purposes.  
Includes both **Python** and **C# (.NET)** implementations.

---

## ✨ Features

- ✅ Extract and repack the game’s archive formats (`.dat`, `.pak`)
- ✅ Supports both **PC (Japanese Visual Novel)** and **Xbox 360** versions
- ✅ Preserves original archive structure and file layout
- ✅ Detects common file types (PNG, WAV, OGG, etc.)
- ✅ Identifies and labels **XMA2** audio files (`.xma`)
- ✅ Deterministic packing (sorted files, stable offsets)
- ✅ 2048-byte alignment for Xbox archives
- ✅ Unified **C# executable (`TimeLeap.exe`)** for both versions

---

## 📂 Supported Versions

| Platform | Format | Tools | Notes |
|-----------|---------|--------|--------|
| **PC (Japanese VN)** | `.dat` | `extract_dat.py`, `pack_dat.py`, `TimeLeap.exe` | Full unpack/repack support |
| **Xbox 360** | `.pak` | `extract_pak.py`, `pack_pak.py`, `TimeLeap.exe` | Full support with XMA2 detection |

---

## ⚙️ Usage

### 🐍 Python Tools

#### Extract
```bash
# PC (Japanese)
python extract_dat.py GAME.DAT ./output/

# Xbox 360
python extract_pak.py GAME.PAK ./output/

# PC (Japanese)
python pack_dat.py ./output/ new_GAME.DAT

# Xbox 360
python pack_pak.py ./output/ new_GAME.PAK
