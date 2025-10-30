using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace TimeLeap
{    
	public class Entry
	{
		public string Name { get; set; } = string.Empty;
		public uint Size { get; set; }
		public uint Reserved1 { get; set; }
		public uint Offset { get; set; }
		public uint Reserved2 { get; set; }
	}
}