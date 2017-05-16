using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PIWebAPI.LogReader;

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

			string sstatic = LogQueryBuilder.Build(
				new List<int> { 4 },
				new List<int> { 11, 12 },
				new DateTime(2017, 04, 30),
				new DateTime(2017, 05, 02)
				);

			LogReader log = LogReaderFactory.CreateSavedLogReader(@"C:\WebAPILog\Analytic.evtx", sstatic);
			foreach (Query q in log.ReadLog().Values)
			{
				Console.WriteLine(q);
			}

			//LogReader livelog = LogReaderFactory.CreateLiveLogReader(server: "pmartin-web.dev.osisoft.int");
			//foreach (Query q in livelog.ReadLog().Values)
			//{
			//	Console.WriteLine(q);
			//}

			Console.WriteLine("<ENTER> to EXIT");
			Console.ReadLine();
		}
	}
}
