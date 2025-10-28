using System;
using System.IO;

namespace TimeLeap
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=================================");
            Console.WriteLine("  TimeLeap DAT File Extractor");
            Console.WriteLine("=================================\n");

            if (args.Length < 1)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  TimeLeap.exe <input.dat> [output_folder]\n");
                Console.WriteLine("Example:");
                Console.WriteLine("  TimeLeap.exe text.dat extracted\\\n");
                return;
            }

            string datFile = args[0].Trim('"', '\'');
            string outputDir;

            if (args.Length >= 2)
            {
                outputDir = args[1].Trim('"', '\'');
            }
            else
            {
                // Default output folder: filename + "_out"
                string nameWithoutExt = Path.GetFileNameWithoutExtension(datFile);
                outputDir = nameWithoutExt;
            }

            Console.WriteLine($"[+] Input File : {datFile}");
            Console.WriteLine($"[+] Output Dir : {outputDir}");
            Console.WriteLine();

            try
            {
                var unpacker = new TimeLeap();
                unpacker.Unpack(datFile, outputDir);

                Console.WriteLine("\n[✓] Extraction completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[!] Error: {ex.Message}");
            }
        }
    }
}
