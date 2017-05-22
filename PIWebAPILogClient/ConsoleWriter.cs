using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PIWebAPI.LogReader;

namespace PIWebAPILogClient
{
	class ConsoleWriter : IOutputWriter
	{
		Console c;

		public ConsoleWriter () {
			c = new Console();
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
			c.listBox.Items.Add(q);
			c.Show();
		}
	}
}
