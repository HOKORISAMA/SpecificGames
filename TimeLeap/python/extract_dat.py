from pathlib import Path
from dataclasses import dataclass
import sys

# --- nibble-swap used for index ---
def nibble_swap_byte(b: int) -> int:
    return ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)

def nibble_swap(data: bytes) -> bytes:
    return bytes(nibble_swap_byte(b) for b in data)

# --- decode routine (sub_477E90) ---
def decode_buffer(buf: bytearray, start_offset: int):
    """Decode buffer using the game's proprietary encoding scheme."""
    # v12 table (signed in C; we handle as unsigned bytes)
    v12 = [0xFF, 0xFF, 0xFF, 0x01, 0x9C, 0xAA, 0xA5, 0x00, 0x30, 0xFF]
    
    a4 = start_offset
    a2 = len(buf)
    
    # choose mode exactly like the C routine:
    if a4:
        mode = 1
    elif buf[0] < 0x80:   # C `(signed char)buf[0] >= 0` -> unsigned < 128
        mode = 2
    else:
        mode = 1
    
    end = a4 + a2
    
    if mode == 1:
        # Transform 1: Negate bytes at positions 4n+1
        i = 4 * (a4 >> 2) + 1
        while i < end:
            if i >= a4:
                buf[i - a4] = (-((buf[i - a4] if buf[i - a4] < 128 else buf[i - a4]-256))) & 0xFF
            i += 4
        
        # Transform 2: XOR with lookup table at positions 3n
        j = 3 * (a4 // 3)
        while j < end:
            if j >= a4:
                idx = (j % 6) + ((j // 5) % 5)
                buf[j - a4] ^= v12[idx] & 0xFF
            j += 3
        
        # Transform 3: Nibble swap at positions 6n+2
        k = 6 * (a4 // 6) + 2
        while k < end:
            if k >= a4:
                b = buf[k - a4]
                buf[k - a4] = ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)
            k += 6
            
    elif mode == 2:
        # Same transforms but in reverse order
        k = 6 * (a4 // 6) + 2
        while k < end:
            if k >= a4:
                b = buf[k - a4]
                buf[k - a4] = ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)
            k += 6
        
        j = 3 * (a4 // 3)
        while j < end:
            if j >= a4:
                idx = (j % 6) + ((j // 5) % 5)
                buf[j - a4] ^= v12[idx] & 0xFF
            j += 3
        
        i = 4 * (a4 >> 2) + 1
        while i < end:
            if i >= a4:
                buf[i - a4] = (-((buf[i - a4] if buf[i - a4] < 128 else buf[i - a4]-256))) & 0xFF
            i += 4

@dataclass
class Entry:
    name: str
    packed_size: int
    unpacked_size: int
    offset: int

def parse_dat_index(dat_bytes: bytes):
    """Parse the file index from the end of the .dat archive."""
    file_count = int.from_bytes(dat_bytes[-4:], "little")
    index_size = file_count * 80
    index_start = len(dat_bytes) - 4 - index_size
    
    # Decode the nibble-swapped index
    index = nibble_swap(dat_bytes[index_start:index_start + index_size])
    
    entries = []
    for i in range(file_count):
        ent = index[i*80:(i+1)*80]
        name = ent[:64].split(b"\x00", 1)[0].decode("utf-8", errors="ignore")
        offset = int.from_bytes(ent[64:68], "little")
        unpacked = int.from_bytes(ent[68:72], "little")
        packed = int.from_bytes(ent[72:76], "little")
        entries.append(Entry(name, packed, unpacked, offset))
    
    return entries

def extract_and_decode(dat_path: str, output_dir: str = None):
    """Extract and decode all files from a .dat archive."""
    dat_file = Path(dat_path)
    
    if not dat_file.exists():
        print(f"[!] Error: File '{dat_path}' not found")
        return
    
    print(f"[*] Reading {dat_file.name}...")
    dat = dat_file.read_bytes()
    
    try:
        entries = parse_dat_index(dat)
    except Exception as e:
        print(f"[!] Error parsing index: {e}")
        return
    
    print(f"[+] Found {len(entries)} entries")
    
    # Use custom output directory or default
    out_dir = Path(output_dir) if output_dir else Path(str(dat_path).split(".")[0])
    out_dir.mkdir(exist_ok=True)
    
    success_count = 0
    for e in entries:
        try:
            # Extract raw bytes
            raw = bytearray(dat[e.offset:e.offset + e.packed_size])
            
            # Decode the buffer
            decode_buffer(raw, start_offset=0)
            
            # Create subdirectories if needed
            out_file = out_dir / e.name
            out_file.parent.mkdir(parents=True, exist_ok=True)
            
            # Write decoded file
            out_file.write_bytes(raw)
            print(f"  ✓ {e.name} ({len(raw)} bytes)")
            success_count += 1
            
        except Exception as ex:
            print(f"  ✗ {e.name} - Error: {ex}")
    
    print(f"\n[+] Successfully extracted {success_count}/{len(entries)} files → {out_dir}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        dat_file = sys.argv[1]
        output = sys.argv[2] if len(sys.argv) > 2 else None
        extract_and_decode(dat_file, output)
    else:
        extract_and_decode("chip.dat")