using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace TimeLeap
{
    public class Entry
    {
        public string? Name { get; set; }
        public uint PackedSize { get; set; }
        public uint UnpackedSize { get; set; }
        public uint Offset { get; set; }
    }

    public class TimeLeap
    {
        // Nibble swap functions
        private static byte NibbleSwapByte(byte b)
        {
            return (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
        }

        private static byte[] NibbleSwap(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = NibbleSwapByte(data[i]);
            }
            return result;
        }

        // Decode buffer routine
        private static void DecodeBuffer(byte[] buf, int startOffset)
        {
            // v12 table
            byte[] v12 = { 0xFF, 0xFF, 0xFF, 0x01, 0x9C, 0xAA, 0xA5, 0x00, 0x30, 0xFF };

            int a4 = startOffset;
            int a2 = buf.Length;
            int mode;

            // Choose mode
            if (a4 != 0)
            {
                mode = 1;
            }
            else if (buf[0] < 0x80)
            {
                mode = 2;
            }
            else
            {
                mode = 1;
            }

            int end = a4 + a2;

            if (mode == 1)
            {
                // Transform 1: Negate bytes at positions 4n+1
                int i = 4 * (a4 >> 2) + 1;
                while (i < end)
                {
                    if (i >= a4)
                    {
                        sbyte signed = (sbyte)buf[i - a4];
                        buf[i - a4] = (byte)(-signed);
                    }
                    i += 4;
                }

                // Transform 2: XOR with lookup table at positions 3n
                int j = 3 * (a4 / 3);
                while (j < end)
                {
                    if (j >= a4)
                    {
                        int idx = (j % 6) + ((j / 5) % 5);
                        buf[j - a4] ^= v12[idx];
                    }
                    j += 3;
                }

                // Transform 3: Nibble swap at positions 6n+2
                int k = 6 * (a4 / 6) + 2;
                while (k < end)
                {
                    if (k >= a4)
                    {
                        byte b = buf[k - a4];
                        buf[k - a4] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
                    }
                    k += 6;
                }
            }
            else if (mode == 2)
            {
                // Same transforms but in reverse order
                int k = 6 * (a4 / 6) + 2;
                while (k < end)
                {
                    if (k >= a4)
                    {
                        byte b = buf[k - a4];
                        buf[k - a4] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
                    }
                    k += 6;
                }

                int j = 3 * (a4 / 3);
                while (j < end)
                {
                    if (j >= a4)
                    {
                        int idx = (j % 6) + ((j / 5) % 5);
                        buf[j - a4] ^= v12[idx];
                    }
                    j += 3;
                }

                int i = 4 * (a4 >> 2) + 1;
                while (i < end)
                {
                    if (i >= a4)
                    {
                        sbyte signed = (sbyte)buf[i - a4];
                        buf[i - a4] = (byte)(-signed);
                    }
                    i += 4;
                }
            }
        }

        // Parse DAT index
        private static List<Entry> ParseDatIndex(byte[] datBytes)
        {
            int fileCount = BitConverter.ToInt32(datBytes, datBytes.Length - 4);
            int indexSize = fileCount * 80;
            int indexStart = datBytes.Length - 4 - indexSize;

            // Decode the nibble-swapped index
            byte[] indexData = new byte[indexSize];
            Array.Copy(datBytes, indexStart, indexData, 0, indexSize);
            byte[] index = NibbleSwap(indexData);

            List<Entry> entries = new List<Entry>();
            for (int i = 0; i < fileCount; i++)
            {
                int entOffset = i * 80;
                
                // Extract name (first 64 bytes)
                int nameEnd = Array.IndexOf(index, (byte)0, entOffset, 64);
                if (nameEnd == -1) nameEnd = entOffset + 64;
                string name = Encoding.UTF8.GetString(index, entOffset, nameEnd - entOffset);

                uint offset = BitConverter.ToUInt32(index, entOffset + 64);
                uint unpacked = BitConverter.ToUInt32(index, entOffset + 68);
                uint packed = BitConverter.ToUInt32(index, entOffset + 72);

                entries.Add(new Entry
                {
                    Name = name,
                    PackedSize = packed,
                    UnpackedSize = unpacked,
                    Offset = offset
                });
            }

            return entries;
        }

        // Main unpack method
        public void Unpack(string filename, string? outputDir = null)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"[!] Error: File '{filename}' not found");
                return;
            }

            Console.WriteLine($"[*] Reading {Path.GetFileName(filename)}...");
            byte[] dat = File.ReadAllBytes(filename);

            List<Entry> entries;
            try
            {
                entries = ParseDatIndex(dat);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[!] Error parsing index: {e.Message}");
                return;
            }

            Console.WriteLine($"[+] Found {entries.Count} entries");

            // Use custom output directory or default
            string outDir = outputDir ?? Path.GetFileNameWithoutExtension(filename);
            Directory.CreateDirectory(outDir);

            int successCount = 0;
            foreach (var e in entries)
            {
                try
                {
                    // Extract raw bytes
                    byte[] raw = new byte[e.PackedSize];
                    Array.Copy(dat, e.Offset, raw, 0, e.PackedSize);

                    // Decode the buffer
                    DecodeBuffer(raw, 0);

                    // Create subdirectories if needed
                    string outFile = Path.Combine(outDir, e.Name ?? "unnamed");
                    string? directory = Path.GetDirectoryName(outFile);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write decoded file
                    File.WriteAllBytes(outFile, raw);
                    Console.WriteLine($"  ✓ {e.Name} ({raw.Length} bytes)");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ {e.Name} - Error: {ex.Message}");
                }
            }

            Console.WriteLine($"\n[+] Successfully extracted {successCount}/{entries.Count} files → {outDir}");
        }
    }
}