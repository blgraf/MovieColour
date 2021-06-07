using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieColour
{
	public static class Logger
	{
		public static void WriteLogMessage(string message, int ThreadID = 0)
		{
			string TimeStamp = "[" + DateTime.Now.ToString() + "] ";

			TimeStamp += string.Format("[Thread-{0:00}] ", ThreadID);

			Console.WriteLine(TimeStamp + message);
		}

		public static void WriteElapsedTime(string task, TimeSpan ts, int ThreadID = 0)
		{
			string TimeStamp = "[" + DateTime.Now.ToString() + "] ";

			TimeStamp += string.Format("[Thread-{0:00}] ", ThreadID);

			Console.WriteLine(TimeStamp + "Time elapsed " + task + ": {0:00}:{1:00}:{2:00}.{3}",
							ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
		}

	}
}
