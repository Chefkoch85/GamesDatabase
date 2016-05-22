using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CKTGamesDB.Classes
{
	class Util
	{
		public static void ShowCallInfo()
		{
			Debug.WriteLine(Common.Util.CurrentMethodName(2));
		}
	}
}
