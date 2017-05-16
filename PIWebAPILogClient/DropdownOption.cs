using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIWebAPILogClient
{
	public delegate void Selected();

	class DropdownOption
	{
		public Selected OnSelect;
		string Description { get; set; }

		public DropdownOption() : this("",null) { }
		public DropdownOption(string desc, Selected e) {
			Description = desc;
			OnSelect = e;
		}

		public override string ToString()
		{
			return Description;
		}
	}
}
