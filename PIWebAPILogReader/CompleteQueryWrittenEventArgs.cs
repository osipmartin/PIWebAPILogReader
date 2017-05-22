using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPI.LogReader
{
	public class CompleteQueryWrittenEventArgs : EventArgs
	{
		public Query query{ get; set; }
	}
}
