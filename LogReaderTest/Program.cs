using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PIWebAPI.LogReader;
using System.Diagnostics.Eventing.Reader;

// Very simple example of how to read a static log file.

namespace LogReaderTest
{
	class Program
	{
		static void Main(string[] args)
		{
			string s = LogQueryBuilder.Build(
				new List<int> { 4 },
				new List<int> { 11, 12 },
				DateTime.Now.AddDays(-1)
				);

			LogReader log = LogReaderFactory.CreateSavedLogReader(@"C:\WebAPILog\Analytic.evtx", s);
			var result = new Dictionary<string,Query>();
			log.ReadLog(result);
			foreach (Query q in result.Values)
			{
				Console.WriteLine(q);
			}

			Console.WriteLine("<ENTER> to EXIT");
			Console.ReadLine();
		}
	}
}
