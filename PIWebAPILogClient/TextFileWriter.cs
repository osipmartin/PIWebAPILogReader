using PIWebAPI.LogReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPILogClient 
{
	class TextFileWriter : IOutputWriter
	{
		StreamWriter output;

		public TextFileWriter(string filename)
		{
			if (File.Exists(filename))
			{
				output = new StreamWriter(filename);
			}
			else
			{
				throw new ArgumentException("Must specify a valid filename");
			}
		}

		public void WriteAllQuery(Dictionary<string, Query> results)
		{
			foreach (Query q in results.Values)
			{
				WriteQuery(q);
			}
		}

		public void WriteQuery(Query q)
		{
			output.WriteLine(q);
		}	

		public void Dispose()
		{
			output.Dispose();
		}
	}
}
