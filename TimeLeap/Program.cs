using System;
using System.IO;

namespace TimeLeap
{
    public class Program
    {
        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  TimeLeap.exe <mode> <version: xbox|vn> <input.dat> [output_folder]");
            Console.WriteLine();
            Console.WriteLine("Modes:");
            Console.WriteLine("  -u    Unpack .dat/.pak file");
            Console.WriteLine("  -c    Pack folder into .dat/.pak file");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  TimeLeap.exe -u xbox text.pak extracted\\");
            Console.WriteLine("  TimeLeap.exe -u vn text.dat extracted\\");
            Console.WriteLine("  TimeLeap.exe -c xbox extracted\\ text.pak");
            Console.WriteLine("  TimeLeap.exe -c vn extracted\\ text.dat");
            Console.WriteLine();
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("=================================");
            Console.WriteLine("  TimeLeap DAT/PAK File Tool");
            Console.WriteLine("  For Xbox 360 and JP Visual Novel Versions");
            Console.WriteLine("=================================\n");

            if (args.Length < 3)
            {
                ShowHelp();
                return;
            }

            string mode = args[0];
            string version = args[1].ToLowerInvariant();
            string inputPath = args[2].Trim('"', '\'');

            string outputPath;
            if (args.Length >= 4)
                outputPath = args[3].Trim('"', '\'');
            else
                outputPath = mode.Equals("-u", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(inputPath)
                    : inputPath + (version == "xbox" ? ".pak" : ".dat");

            Console.WriteLine($"[+] Mode       : {mode}");
            Console.WriteLine($"[+] Version    : {version}");
            Console.WriteLine($"[+] Input      : {inputPath}");
            Console.WriteLine($"[+] Output     : {outputPath}");
            Console.WriteLine();

            try
            {
                ITimeLeapTool tool = version switch
                {
                    "xbox" => new TimeLeapPak(),
                    "vn" => new TimeLeapDat(),
                    _ => throw new ArgumentException("Invalid version specified. Use 'xbox' or 'vn'.")
                };

                if (mode.Equals("-u", StringComparison.OrdinalIgnoreCase))
                {
                    tool.Unpack(inputPath, outputPath);
                    Console.WriteLine("\n[✓] Extraction completed successfully.");
                }
                else if (mode.Equals("-c", StringComparison.OrdinalIgnoreCase))
                {
                    tool.Pack(inputPath, outputPath);
                    Console.WriteLine("\n[✓] Packing completed successfully.");
                }
                else
                {
                    Console.WriteLine("[!] Invalid mode specified.\n");
                    ShowHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[!] Error: {ex.Message}");
            }
        }
    }
}
