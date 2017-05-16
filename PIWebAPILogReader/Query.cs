using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPI.LogReader
{
	public class Query
	{
		public string id { get; set; }
		public DateTime StartTime { get; set; }
		public DateTime EndTime { get; set; }

		public TimeSpan Duration { get { return EndTime - StartTime; } }

		public Query(string qid, DateTime start)
		{
			id = qid;
			StartTime = start;
			EndTime = start;
		}

		public override string ToString()
		{
			return $"{id} : {StartTime:yy-MMM-dd:hh:mm:ss}  {EndTime:yy-MMM-dd:hh:mm:ss} ({Duration.Seconds})";
		}
	}
}
