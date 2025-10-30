using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TimeLeap
{
    /// <summary>
    /// PAK Extractor/Packer Tool (Xbox 360)
    /// 
    /// Features:
    /// - Detects embedded file types via magic bytes or PAK basename
    /// - Recognizes WAV files with XMA2 codec
    /// - Marks XMA2 files with .xma extension for clarity
    /// - Packs files back into PAK archives preserving original formats
    /// </summary>
    public class TimeLeapPak : ITimeLeapTool
    {
        // Common file signatures (magic bytes → extension)
        private static readonly Dictionary<string, string> Signatures = new()
        {
            ["89504E470D0A1A0A"] = ".png",
            ["424D"] = ".bmp",
            ["52494646"] = ".wav",
            ["4F676753"] = ".ogg",
            ["FFD8FF"] = ".jpg"
        };

        /// <summary>
        /// Detect file type using header first, then PAK name context
        /// </summary>
        private static string DetectFileType(byte[] data, string pakName)
        {
            foreach (var kvp in Signatures)
            {
                var bytes = Convert.FromHexString(kvp.Key);
                if (data.Length >= bytes.Length && data.AsSpan(0, bytes.Length).SequenceEqual(bytes))
                    return kvp.Value;
            }

            // Fallback to filename heuristics
            var name = pakName.ToLowerInvariant();
            if (name.Contains("cg")) return ".png";
            if (name.Contains("se") || name.Contains("snd")) return ".wav";
            if (name.Contains("text") || name.Contains("txt") || name.Contains("scr")) return ".txt";
            return ".bin";
        }

        /// <summary>
        /// Check if a WAV file contains XMA2 audio (codec tag 0x0166)
        /// </summary>
        private static bool IsXma2Wave(byte[] data)
        {
            if (data.Length < 20 || !data.AsSpan(0, 4).SequenceEqual(Encoding.ASCII.GetBytes("RIFF")))
                return false;

            for (int i = 0; i < data.Length - 10; i++)
            {
                if (data.AsSpan(i, 4).SequenceEqual(Encoding.ASCII.GetBytes("fmt ")))
                {
                    ushort formatTag = BitConverter.ToUInt16(data, i + 8);
                    return formatTag == 0x0166;
                }
            }
            return false;
        }

        /// <summary>
        /// Parse PAK index table to extract file offsets and sizes
        /// </summary>
        private static List<Entry> ParsePakIndex(BinaryReader reader)
        {
            reader.BaseStream.Position = 0;

            int fileCount = reader.ReadInt32();
            if (fileCount <= 0 || fileCount > 100000)
                throw new InvalidDataException($"Invalid file count: {fileCount}");

            var offsets = new List<int>(fileCount + 1);
            for (int i = 0; i < fileCount; i++)
                offsets.Add(reader.ReadInt32());

            offsets.Add((int)reader.BaseStream.Length);

            var entries = new List<Entry>(fileCount);
            for (int i = 0; i < fileCount; i++)
            {
                int start = offsets[i];
                int end = offsets[i + 1];
                int size = Math.Max(0, end - start);

                entries.Add(new Entry
                {
                    Offset = (uint)start,
                    Size = (uint)size
                });
            }

            return entries;
        }

        /// <summary>
        /// Extract all files from PAK archive
        /// </summary>
        public void Unpack(string pakPath, string outputDir)
        {
            if (!File.Exists(pakPath))
                throw new FileNotFoundException($"PAK file not found: {pakPath}");

            var pakName = Path.GetFileName(pakPath);
            var baseName = Path.GetFileNameWithoutExtension(pakPath);
            Directory.CreateDirectory(outputDir);

            using var fs = File.OpenRead(pakPath);
            using var reader = new BinaryReader(fs);

            var entries = ParsePakIndex(reader);

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                reader.BaseStream.Position = entry.Offset;
                var data = reader.ReadBytes((int)entry.Size);

                var ext = DetectFileType(data, pakName);
                
                // Check if it's an XMA2 file and adjust extension
                if (ext == ".wav" && IsXma2Wave(data))
                {
                    ext = ".xma";
                    Console.WriteLine($"ℹ️ XMA2 audio detected at entry {i}");
                }

                var fileName = $"{baseName}_{i:D4}{ext}";
                var filePath = Path.Combine(outputDir, fileName);

                File.WriteAllBytes(filePath, data);
                Console.WriteLine($"✅ {fileName}: {entry.Size:N0} bytes");
            }

            Console.WriteLine($"\n✅ Extracted {entries.Count} files from {pakName}");
        }

        /// <summary>
        /// Pack files from a folder into a PAK archive
        /// </summary>
		public void Pack(string inputFolder, string outputFile)
		{
			if (!Directory.Exists(inputFolder))
				throw new DirectoryNotFoundException($"Input folder not found: {inputFolder}");

			var allFiles = Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories)
				.OrderBy(f => f)
				.ToList();

			if (allFiles.Count == 0)
				throw new InvalidOperationException($"No files found in {inputFolder}");

			Console.WriteLine($"\n📦 Packing {allFiles.Count} files into {Path.GetFileName(outputFile)}...\n");

			using var fs = File.Create(outputFile);
			using var writer = new BinaryWriter(fs);

			const int FIRST_FILE_OFFSET = 0x800; // first file starts at 2048 bytes

			var offsets = new List<int>();
			var fileData = new List<byte[]>();

			// Calculate all offsets
			int currentOffset = FIRST_FILE_OFFSET;
			foreach (var file in allFiles)
			{
				var data = File.ReadAllBytes(file);
				fileData.Add(data);
				offsets.Add(currentOffset);
				currentOffset += data.Length;

				Console.WriteLine($"{Path.GetFileName(file)}: {data.Length:N0} bytes");
			}

			// Add the final "end offset" after last file
			offsets.Add(currentOffset);

			// ---- Write Header ----
			writer.Write(allFiles.Count);       // file count
			foreach (var offset in offsets)     // includes end offset
				writer.Write(offset);

			// ---- Pad Header up to FIRST_FILE_OFFSET ----
			long padSize = FIRST_FILE_OFFSET - writer.BaseStream.Position;
			if (padSize > 0)
				writer.Write(new byte[padSize]);

			// ---- Write File Data ----
			foreach (var data in fileData)
				writer.Write(data);

			Console.WriteLine($"\n✅ Created {Path.GetFileName(outputFile)} ({writer.BaseStream.Length:N0} bytes)");
		}


    }
}