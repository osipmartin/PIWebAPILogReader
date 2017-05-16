using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PIWebAPI.LogReader
{
	public class LogReader : IDisposable
	{
		private EventLogQuery elq;
		private EventLogWatcher watcher;
		private Dictionary<string, Query> results;

		private LogReader() { }

		internal LogReader(bool staticfile, string path, string query, string server = ".") {
			if (staticfile) {
				elq = new EventLogQuery(path, PathType.FilePath, query);
			}
			else {
				elq = new EventLogQuery(path, PathType.LogName, query);
				if (server != ".") {
					EventLogSession session = new EventLogSession(server);
					elq.Session = session;
				}
			}
			results = new Dictionary<string, Query>();
		}

		/// <summary>
		/// Start watching the log for new entries. Results of the watch can be retrieved via GetCurrentWatchResults() or EndWatch()
		/// </summary>
		/// <param name="callback">Specify a second callback</param>
		public void StartWatch(EventHandler<EventRecordWrittenEventArgs> callback = null) {
			results.Clear();

			watcher = new EventLogWatcher(elq);
			watcher.EventRecordWritten += (obj, arg) =>
			{
				ParseEventRecord(arg.EventRecord);
			};
			
			if(callback != null) {
				watcher.EventRecordWritten += callback;
			}

			watcher.Enabled = true;
		}

		public Dictionary<string, Query> GetCurrentWatchResults() {
			return results;
		}

		public Dictionary<string, Query> EndWatch() {
			watcher.Dispose();
			return GetCurrentWatchResults();
		}

		/// <summary>
		/// Get entries that match the LogReader query parmaters from an unwatched log 
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, Query> ReadLog() {
			results.Clear();
			EventLogReader logReader = new EventLogReader(elq);
			EventRecord entry = logReader.ReadEvent();
			while (entry != null)
			{
				ParseEventRecord(entry);
				entry = logReader.ReadEvent();
			}

			return results;
		}

		private void ParseEventRecord(EventRecord e) {
			int msgid = e.Id;

			DateTime? nullable_d = e.TimeCreated;
			if (nullable_d == null)
				return;
			DateTime d = ((DateTime)nullable_d).ToLocalTime();

			//11 is begin query
			//12 is end query

			XDocument doc = XDocument.Parse(e.ToXml());
			var s = from n in doc.Descendants()
					where (string)n.LastAttribute == "requestId"
					select n.Value;
			string id = s.First().Trim(new [] {'{','}' });

			if (msgid == 11)
			{
				if (results.ContainsKey(id))
				{
					results[id].StartTime = d;
				}
				else
				{
					results.Add(id, new Query(id, d));
				}
			}
			else
			{
				if (results.ContainsKey(id))
				{
					results[id].EndTime = d;
				}
				else
				{
					results.Add(id, new Query(id, d));
				}
			}			
		}

		public void Dispose()
		{
			((IDisposable)watcher)?.Dispose();
		}
	}
}
