using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PIWebAPI.LogReader;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.EventFrame;

namespace PIWebAPILogClient
{
	/// <summary>
	/// Write the queries and their duration to Event Frames
	/// </summary>
	class AFWriter : IOutputWriter
	{
		public AFDatabase db;
		public AFElementTemplate eftemplate;

		public AFWriter(AFDatabase database) {
			db = database;
			if ( db != null ) {
				//check if template exists
				//if it doesn't -- create it
				eftemplate = db.ElementTemplates["PIWebAPI_QueryResults"];
				if (eftemplate == null)
				{
					eftemplate = new AFElementTemplate("PIWebAPI_QueryResults");
					eftemplate.AttributeTemplates.Add("ID");
					eftemplate.InstanceType = typeof(AFEventFrame);
					db.ElementTemplates.Add(eftemplate);
					db.CheckIn();
				}
			}
		}

		public void WriteAllQuery(Dictionary<string, Query> results)
		{
			foreach (Query q in results.Values)
			{
				WriteQuery(q);
			}
			db.CheckIn();
		}

		public void WriteQuery(Query q)
		{
			AFEventFrame ef = new AFEventFrame(db, $"{eftemplate.Name}_{q.StartTime.ToShortDateString()}", eftemplate);
			ef.SetStartTime(q.StartTime);
			ef.SetEndTime(q.EndTime);
			ef.Attributes["ID"].SetValue(new AFValue(q.id));

			db.CheckIn();
		}
	}
}
