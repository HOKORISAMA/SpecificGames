using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace TimeLeap
{
    public class TimeLeapDat : ITimeLeapTool
    {
        public void Unpack(string filename, string outputDir)
        {
            if (!File.Exists(filename))
            {
                Console.WriteLine($"[!] File not found: {filename}");
                return;
            }

            byte[] dat = File.ReadAllBytes(filename);
            List<Entry> entries;

            try
            {
                entries = ParseDatIndex(dat);
                Console.WriteLine($"[+] Found {entries.Count} entries\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Failed to parse index: {ex.Message}");
                return;
            }

            Directory.CreateDirectory(outputDir ??= Path.GetFileNameWithoutExtension(filename));
            int success = 0;

            foreach (var e in entries)
            {
                try
                {
                    byte[] raw = new byte[e.Size];
                    Array.Copy(dat, e.Offset, raw, 0, e.Size);
                    DecodeBuffer(raw);

                    string path = Path.Combine(outputDir, e.Name ?? "unnamed");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, raw);

                    Console.WriteLine($"  ✓ {e.Name} ({raw.Length} bytes)");
                    success++;
                }
                catch (Exception err)
                {
                    Console.WriteLine($"  ✗ {e.Name} - {err.Message}");
                }
            }

            Console.WriteLine($"\n[✓] Extracted {success}/{entries.Count} files → {outputDir}");
        }
		
		public void Pack(string inputFolder, string outputFile)
		{
            if (!Directory.Exists(inputFolder))
            {
                Console.WriteLine($"[!] Folder not found: {inputFolder}");
                return;
            }

            string[] files = Directory.GetFiles(inputFolder, "*", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                Console.WriteLine($"[!] No files found in: {inputFolder}");
                return;
            }

            Console.WriteLine($"[+] Packing {files.Length} files...\n");

            var entries = new List<Entry>();
            var dataStream = new MemoryStream();
            int success = 0;

            // Calculate index size first (it goes at the beginning)
            uint indexSize = (uint)(files.Length * 80);
            uint currentOffset = indexSize;

            foreach (string filePath in files)
            {
                try
                {
                    string relativePath = Path.GetRelativePath(inputFolder, filePath);
                    byte[] fileData = File.ReadAllBytes(filePath);                    
                    byte[] encoded = new byte[fileData.Length];
                    Array.Copy(fileData, encoded, fileData.Length);

                    // Encode the buffer (reverse of decode)
                    EncodeBuffer(encoded);

                    // Write encoded data to stream
                    dataStream.Write(encoded, 0, encoded.Length);

                    entries.Add(new Entry
                    {
                        Name = relativePath.Replace('\\', '/'),
                        Offset = currentOffset,
                        Size = (uint)encoded.Length,
                        Reserved1 = 0,
                        Reserved2 = 0
                    });

                    currentOffset += (uint)encoded.Length;

                    Console.WriteLine($"  ✓ {relativePath} ({fileData.Length} bytes)");
                    success++;
                }
                catch (Exception err)
                {
                    Console.WriteLine($"  ✗ {Path.GetFileName(filePath)} - {err.Message}");
                }
            }

            try
            {
                // Build the index
                byte[] index = BuildDatIndex(entries);
                
                // Write the complete DAT file: index, then data, then file count
                using (var output = new FileStream(outputFile, FileMode.Create))
                {
                    // Write index first
                    output.Write(index, 0, index.Length);
                    
                    // Write data
                    dataStream.Position = 0;
                    dataStream.CopyTo(output);
                    
                    // Write file count at the end
                    byte[] count = BitConverter.GetBytes(entries.Count);
                    output.Write(count, 0, 4);
                }

                Console.WriteLine($"\n[✓] Packed {success}/{files.Length} files → {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[!] Failed to write output file: {ex.Message}");
            }
		}

        private static List<Entry> ParseDatIndex(byte[] dat)
        {
            int fileCount = BitConverter.ToInt32(dat, dat.Length - 4);
            int indexStart = 0; // Index is at the beginning
            int indexSize = fileCount * 80;

            byte[] index = Utility.NibbleSwap(dat[indexStart..indexSize]);
            var list = new List<Entry>(fileCount);

            for (int i = 0; i < fileCount; i++)
            {
                int pos = i * 80;
                int nameLen = Array.IndexOf(index, (byte)0, pos, 64) - pos;
                if (nameLen < 0) nameLen = 64;

                string name = Encoding.UTF8.GetString(index, pos, nameLen);
                uint offset = BitConverter.ToUInt32(index, pos + 64);
                uint reserved1 = BitConverter.ToUInt32(index, pos + 68);
                uint packed = BitConverter.ToUInt32(index, pos + 72);
                uint reserved2 = BitConverter.ToUInt32(index, pos + 76);

                list.Add(new Entry { Name = name, Reserved1 = reserved1, Offset = offset, Size = packed, Reserved2 = reserved2 });
            }

            return list;
        }

        private static byte[] BuildDatIndex(List<Entry> entries)
        {
            byte[] index = new byte[entries.Count * 80];
            
            for (int i = 0; i < entries.Count; i++)
            {
                int pos = i * 80;
                var entry = entries[i];
                
                // Write name (max 64 bytes, null-terminated)
                byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name ?? "");
                int nameLength = Math.Min(nameBytes.Length, 63);
                Array.Copy(nameBytes, 0, index, pos, nameLength);
                // Null terminator is already present from zero-initialized array
                
                // Write offset, reserved, packed size, and reserved field
                Array.Copy(BitConverter.GetBytes(entry.Offset), 0, index, pos + 64, 4);
                Array.Copy(BitConverter.GetBytes(entry.Reserved1), 0, index, pos + 68, 4);
                Array.Copy(BitConverter.GetBytes(entry.Size), 0, index, pos + 72, 4);
                Array.Copy(BitConverter.GetBytes(entry.Reserved2), 0, index, pos + 76, 4);
            }
            
            // Apply nibble swap to the index
            return Utility.NibbleSwap(index);
        }

        private static void DecodeBuffer(byte[] buf)
        {
            byte[] v12 = { 0xFF, 0xFF, 0xFF, 0x01, 0x9C, 0xAA, 0xA5, 0x00, 0x30, 0xFF };
            int len = buf.Length;

            int mode = buf[0] < 0x80 ? 2 : 1;

            if (mode == 1)
            {
                for (int i = 1; i < len; i += 4)
                    buf[i] = (byte)(-(sbyte)buf[i]);

                for (int j = 0; j < len; j += 3)
                {
                    int idx = (j % 6) + ((j / 5) % 5);
                    buf[j] ^= v12[idx];
                }

                for (int k = 2; k < len; k += 6)
                {
                    byte b = buf[k];
                    buf[k] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
                }
            }
            else
            {
                for (int k = 2; k < len; k += 6)
                {
                    byte b = buf[k];
                    buf[k] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
                }

                for (int j = 0; j < len; j += 3)
                {
                    int idx = (j % 6) + ((j / 5) % 5);
                    buf[j] ^= v12[idx];
                }

                for (int i = 1; i < len; i += 4)
                    buf[i] = (byte)(-(sbyte)buf[i]);
            }
        }

        private static void EncodeBuffer(byte[] buf)
		{
			byte[] v12 = { 0xFF, 0xFF, 0xFF, 0x01, 0x9C, 0xAA, 0xA5, 0x00, 0x30, 0xFF };
			int len = buf.Length;

			int mode = buf[0] > 0x80 ? 2 : 1;

			if (mode == 1)
			{
				// Reverse order: nibble swap → XOR → negate
				for (int k = 2; k < len; k += 6)
				{
					byte b = buf[k];
					buf[k] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
				}

				for (int j = 0; j < len; j += 3)
				{
					int idx = (j % 6) + ((j / 5) % 5);
					buf[j] ^= v12[idx];
				}

				for (int i = 1; i < len; i += 4)
					buf[i] = (byte)(-(sbyte)buf[i]);
			}
			else // mode == 2
			{
				// Reverse order: negate → XOR → nibble swap
				for (int i = 1; i < len; i += 4)
					buf[i] = (byte)(-(sbyte)buf[i]);

				for (int j = 0; j < len; j += 3)
				{
					int idx = (j % 6) + ((j / 5) % 5);
					buf[j] ^= v12[idx];
				}

				for (int k = 2; k < len; k += 6)
				{
					byte b = buf[k];
					buf[k] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
				}
			}
		}
    }
}