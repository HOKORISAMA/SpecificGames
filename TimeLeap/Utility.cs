using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace TimeLeap
{
    public static class Utility
    {
        public static byte[] NibbleSwap(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                result[i] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
            }
            return result;
        }

        public static void NibbleSwapInPlace(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                data[i] = (byte)(((b & 0x0F) << 4) | ((b & 0xF0) >> 4));
            }
        }
		
		/// <summary>
        /// Check if FFmpeg is available on the system
        /// </summary>
        public static bool IsFfmpegAvailable()
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(1500);
                return process is { ExitCode: 0 };
            }
            catch
            {
                return false;
            }
        }
    }
}
