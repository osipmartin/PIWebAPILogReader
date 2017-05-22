using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PIWebAPI.LogReader
{
	public class LogReader : IDisposable
	{
		private EventLogQuery elq;
		private EventLogWatcher watcher;
		public event EventHandler<CompleteQueryWrittenEventArgs> CompleteQueryWrittenEvent;

		private LogReader() { }

		internal LogReader(bool staticfile, string path, string query, string server = ".") {
			if (staticfile) {
				elq = new EventLogQuery(path, PathType.FilePath, query);
			}
			else {
				elq = new EventLogQuery(path, PathType.LogName, query);
				server = server.Length < 1 ? "." : server; 
				if (server != ".") {
					EventLogSession session = new EventLogSession(server);
					elq.Session = session;
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns>Did the watch start successfully?</returns>
		public bool StartWatch() {
			
			Dictionary<string,Query> result = new Dictionary<string, Query>();
			try {
				watcher = new EventLogWatcher(elq);
				watcher.EventRecordWritten += (obj, arg) =>
				{
					ParseEventRecord(arg.EventRecord, result);
				};
			
				watcher.Enabled = true;
				return true;
			}
			catch( EventLogReadingException e) {
				watcher = null;
				Console.WriteLine("Error reading the log: {0}", e.Message);
				return false;
			}
			catch( EventLogException e) {
				watcher = null;
				Console.WriteLine("Error reading the log: {0}", e.Message);
				return false;
			}
			catch( Exception e) {
				watcher = null;
				Console.WriteLine("Error reading the log: {0}", e.Message);
				return false;
			}
		}

		public void EndWatch() {
			watcher.Dispose();
		}

		/// <summary>
		/// Get entries that match the LogReader query parmaters from an unwatched log 
		/// </summary>
		/// <returns></returns>
		public void ReadLog(Dictionary<string, Query> result, CancellationTokenSource ct = null) {			
			EventLogReader logReader = new EventLogReader(elq);
			EventRecord entry = logReader.ReadEvent();
			while (entry != null)
			{
				if( ct != null && ct.Token.IsCancellationRequested ){
					break;
				}

				ParseEventRecord(entry, result);
				entry = logReader.ReadEvent();
			}				
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <param name="result"></param>
		/// <returns>returns a query when a complete record has just been written, otherwise returns null</returns>
		private void ParseEventRecord(EventRecord e, Dictionary<string, Query> result) {
			int msgid = e.Id;

			DateTime? nullable_d = e.TimeCreated;
			if (nullable_d == null)
				return;
			DateTime d = ((DateTime)nullable_d).ToLocalTime();

			//11 is begin query
			//12 is end query

			//parse log into XML doc in order to get the id for the request
			XDocument doc = XDocument.Parse(e.ToXml());
			var s = from n in doc.Descendants()
					where (string)n.LastAttribute == "requestId"
					select n.Value;
			string id = s.First().Trim(new [] {'{','}' });

			if (msgid == 11)
			{
				if (result.ContainsKey(id))
				{
					result[id].StartTime = d;
					CompleteQueryWrittenEvent?.Invoke(this, new CompleteQueryWrittenEventArgs { query = result[id] });
				}
				else
				{
					result.Add(id, new Query(id, d));
				}
			}
			else
			{
				if (result.ContainsKey(id))
				{
					result[id].EndTime = d;
					CompleteQueryWrittenEvent?.Invoke(this, new CompleteQueryWrittenEventArgs { query = result[id] });
				}
				else
				{
					result.Add(id, new Query(id, d));
				}
			}			
		}

		public void Dispose()
		{
			CompleteQueryWrittenEvent = null;
			((IDisposable)watcher)?.Dispose();
		}
	}
}
