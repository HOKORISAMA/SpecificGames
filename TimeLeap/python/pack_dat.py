from pathlib import Path
from dataclasses import dataclass
import sys

# --- nibble-swap used for index ---
def nibble_swap_byte(b: int) -> int:
    return ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)

def nibble_swap(data: bytes) -> bytes:
    return bytes(nibble_swap_byte(b) for b in data)

# --- encode routine (reverse of decode) ---
def encode_buffer(buf: bytearray, start_offset: int):
    """Encode buffer using the game's proprietary encoding scheme (reverse of decode)."""
    v12 = [0xFF, 0xFF, 0xFF, 0x01, 0x9C, 0xAA, 0xA5, 0x00, 0x30, 0xFF]
    
    a4 = start_offset
    a2 = len(buf)
    
    # Determine mode (same logic as decoder)
    if a4:
        mode = 1
    elif buf[0] < 0x80:
        mode = 2
    else:
        mode = 1
    
    end = a4 + a2
    
    if mode == 1:
        # Apply transforms in REVERSE order compared to decode
        # Transform 3: Nibble swap at positions 6n+2
        k = 6 * (a4 // 6) + 2
        while k < end:
            if k >= a4:
                b = buf[k - a4]
                buf[k - a4] = ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)
            k += 6
        
        # Transform 2: XOR with lookup table at positions 3n
        j = 3 * (a4 // 3)
        while j < end:
            if j >= a4:
                idx = (j % 6) + ((j // 5) % 5)
                buf[j - a4] ^= v12[idx] & 0xFF
            j += 3
        
        # Transform 1: Negate bytes at positions 4n+1
        i = 4 * (a4 >> 2) + 1
        while i < end:
            if i >= a4:
                buf[i - a4] = (-((buf[i - a4] if buf[i - a4] < 128 else buf[i - a4]-256))) & 0xFF
            i += 4
            
    elif mode == 2:
        # Apply transforms in REVERSE order for mode 2
        i = 4 * (a4 >> 2) + 1
        while i < end:
            if i >= a4:
                buf[i - a4] = (-((buf[i - a4] if buf[i - a4] < 128 else buf[i - a4]-256))) & 0xFF
            i += 4
        
        j = 3 * (a4 // 3)
        while j < end:
            if j >= a4:
                idx = (j % 6) + ((j // 5) % 5)
                buf[j - a4] ^= v12[idx] & 0xFF
            j += 3
        
        k = 6 * (a4 // 6) + 2
        while k < end:
            if k >= a4:
                b = buf[k - a4]
                buf[k - a4] = ((b & 0x0F) << 4) | ((b & 0xF0) >> 4)
            k += 6

@dataclass
class FileEntry:
    name: str
    data: bytes
    offset: int = 0

def create_dat_archive(input_dir: str, output_dat: str = None):
    """Pack files from a directory into a .dat archive."""
    input_path = Path(input_dir)
    
    if not input_path.exists() or not input_path.is_dir():
        print(f"[!] Error: Directory '{input_dir}' not found")
        return
    
    print(f"[*] Scanning directory: {input_path}")
    
    # Collect all files recursively
    files = []
    for file_path in sorted(input_path.rglob("*")):
        if file_path.is_file():
            # Get relative path for the archive
            relative_name = str(file_path.relative_to(input_path)).replace("\\", "/")
            data = file_path.read_bytes()
            files.append(FileEntry(relative_name, data))
    
    if not files:
        print("[!] No files found in directory")
        return
    
    print(f"[+] Found {len(files)} files to pack")
    
    # Encode each file and calculate offsets
    current_offset = 0
    encoded_files = []
    
    for f in files:
        # Encode the data
        encoded = bytearray(f.data)
        encode_buffer(encoded, start_offset=0)
        
        # Store with offset
        encoded_files.append(FileEntry(f.name, bytes(encoded), current_offset))
        current_offset += len(encoded)
        print(f"  → {f.name} ({len(f.data)} → {len(encoded)} bytes)")
    
    # Build the archive
    archive = bytearray()
    
    # Write all encoded file data
    for f in encoded_files:
        archive.extend(f.data)
    
    # Build the index (80 bytes per entry)
    index = bytearray()
    for f in encoded_files:
        entry = bytearray(80)
        
        # Name (64 bytes, null-terminated)
        name_bytes = f.name.encode("utf-8")[:63]
        entry[:len(name_bytes)] = name_bytes
        
        # Offset (4 bytes, little-endian)
        entry[64:68] = f.offset.to_bytes(4, "little")
        
        # Unpacked size (4 bytes) - same as packed for this format
        entry[68:72] = len(f.data).to_bytes(4, "little")
        
        # Packed size (4 bytes)
        entry[72:76] = len(f.data).to_bytes(4, "little")
        
        index.extend(entry)
    
    # Nibble-swap the index
    index_swapped = nibble_swap(bytes(index))
    
    # Append swapped index to archive
    archive.extend(index_swapped)
    
    # Append file count (4 bytes, little-endian)
    archive.extend(len(files).to_bytes(4, "little"))
    
    # Write the final archive
    if output_dat is None:
        output_dat = input_path.name + ".dat"
    
    output_path = Path(output_dat)
    output_path.write_bytes(archive)
    
    print(f"\n[+] Successfully created archive: {output_path}")
    print(f"    Total size: {len(archive)} bytes")
    print(f"    Data: {current_offset} bytes")
    print(f"    Index: {len(index_swapped)} bytes")
    print(f"    Files: {len(files)}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        input_directory = sys.argv[1]
        output_file = sys.argv[2] if len(sys.argv) > 2 else None
        create_dat_archive(input_directory, output_file)
    else:
        print("Usage: python pack_dat.py <input_directory> [output.dat]")
        print("Example: python pack_dat.py chip chip.dat")