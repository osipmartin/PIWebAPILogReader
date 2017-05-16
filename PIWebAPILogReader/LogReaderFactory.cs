using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPI.LogReader
{
	public static class LogReaderFactory
    {
		public static LogReader CreateSavedLogReader(string path, string query = "*[System/Level=4]") {
			return new LogReader(true, path, query);
		}

		public static LogReader CreateLiveLogReader(string logpath = "PIWebAPI/Analytic", string query = "*[System/Level=4]", string server = ".") {
			return new LogReader(false, logpath, query, server);
		}
    }
}
