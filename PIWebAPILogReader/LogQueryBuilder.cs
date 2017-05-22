using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPI.LogReader
{
	public static class LogQueryBuilder
	{
		/// <summary>
		/// Build a query that be passed to the LogReader class
		/// </summary>
		/// <param name="levels"></param>
		/// <param name="eventids"></param>
		/// <param name="startTime"></param>
		/// <param name="endTime"></param>
		/// <returns></returns>
		public static string Build(	IList<int> levels = null,
									IList<int> eventids = null, 
									DateTime startTime = default(DateTime), 
									DateTime endTime = default(DateTime)) {

			StringBuilder sb = new StringBuilder("*[System[");

			if(levels == null) {
				sb.Append("(Level=4) ");
			}
			else {
				sb.Append("(");
				for(int i = 0; i < levels.Count;) {
					sb.Append("Level=" + levels[i]);
					if(++i < levels.Count) {
						sb.Append(" or ");
					}
				}
				sb.Append(")");
			}

			if(eventids != null) {
				sb.Append(" and (");
				for (int i = 0; i < eventids.Count;)
				{
					sb.Append("EventID=" + eventids[i]);
					if (++i < eventids.Count)
					{
						sb.Append(" or ");
					}
				}
				sb.Append(")");
			}

			if(startTime != default(DateTime)) {
				sb.Append(" and TimeCreated[@SystemTime >= '"+startTime.ToString("s")+"Z'");
				if(endTime == default(DateTime)){
					sb.Append("]");
				}
			}

			if(endTime != default(DateTime)){
				if(startTime != default(DateTime)) {
					sb.Append(" and ");
				}
				else {
					sb.Append(" and TimeCreated[");
				}
				sb.Append("@SystemTime <= '" + endTime.ToString("s") + "Z']");
			}
			sb.Append("]]");

			return sb.ToString();
		}
	}
}
