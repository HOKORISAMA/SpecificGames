using System;

namespace TimeLeap
{
    public interface ITimeLeapTool
    {
        void Unpack(string inputFile, string outputFolder);
        void Pack(string inputFolder, string outputFile);
    }
}
