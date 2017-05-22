using PIWebAPI.LogReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPILogClient
{
	public interface IOutputWriter
	{
		void WriteQuery(Query q);
		void WriteAllQuery(Dictionary<string, Query> results);
	}
}
