using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WorldWind.Utilities {
	/// <summary>
	/// Debug log functionality
	/// </summary>
	public sealed class Log {
		private static StreamWriter logWriter;
		private static string logPath;
		private static string logFilePath;

		/// <summary>
		/// Static class (Only static members)
		/// </summary>
		private Log() {}

		static Log() {
			try {
				logPath = DefaultSettingsDirectory();
				Directory.CreateDirectory(logPath);

				// TODO: do not hardcode logfile name?
				logFilePath = Path.Combine(logPath, "error.txt");

				logWriter = new StreamWriter(logFilePath, true);
				logWriter.AutoFlush = true;
			}
			catch (Exception caught) {
				throw new ApplicationException(String.Format("Unexpected logfile error: {0}", logFilePath), caught);
			}
		}

		// Return the full default directory path to be used for storing settings files,
		// which is also where logfiles will be stored.
		public static string DefaultSettingsDirectory() {
			// Example for Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData):
			// @"C:\Documents and Settings\<user>\Application Data"
			return Application.StartupPath + @"\Logs";
		}

		// Return the program-specifc part of the full default directory path to be used
		// for storing settings files, which is also where logfiles will be stored.
		public static string DefaultSettingsDirectorySuffix() {
			// Application.ProductName is set by AssemblyProduct in \WorldWind\AssembyInfo.cs
			Version ver = new Version(Application.ProductVersion);
			return string.Format(@"\{0}\{1}\{2}.{3}.{4}.{5}", Application.CompanyName, Application.ProductName, ver.Major, ver.Minor, ver.Build, ver.Revision);
		}

		/// <summary>
		/// Logs a message to the system log
		/// </summary>
		/// <param name="category">1 to 4 character long tag for categorizing the log entries.
		/// If the category is longer than 4 characters it will be clipped.</param>
		/// <param name="message">The actual log messages to be written.</param>
		public static void Write(string category, string message) {
			try {
				lock (logWriter) {
					string logLine = string.Format("{0} {1} {2}", DateTime.Now.ToString("u"), category.PadRight(4, ' ').Substring(0, 4), message);
					logWriter.WriteLine(logLine);
				}
			}
			catch (Exception caught) {
				throw new ApplicationException(String.Format("Unexpected logging error on write(1)"), caught);
			}
		}

		/// <summary>
		/// Logs a message to the system log only in debug builds.
		/// </summary>
		/// <param name="category">1 to 4 character long tag for categorizing the log entries.
		/// If the category is longer than 4 characters it will be clipped.</param>
		/// <param name="message">The actual log messages to be written.</param>
		[Conditional("DEBUG")]
		public static void DebugWrite(string category, string message) {
			Debug.Write(category, message);
		}

		/// <summary>
		/// Logs a message to the system log
		/// </summary>
		public static void Write(string message) {
			Write("", message);
		}

		/// <summary>
		/// Logs a message to the system log only in debug builds.
		/// </summary>
		[Conditional("DEBUG")]
		public static void DebugWrite(string message) {
			Write("", message);
		}

		/// <summary>
		/// Writes a log of an exception.
		/// </summary>
		/// <param name="caught"></param>
		public static void Write(Exception caught) {
			try {
				if (caught is ThreadAbortException) {
					return;
				}

				string functionName = "Unknown";
				if (caught.StackTrace != null) {
					string firstStackTraceLine = caught.StackTrace.Split('\n')[0];
					functionName = firstStackTraceLine.Trim().Split(" (".ToCharArray())[1];
				}
				string logFileName = string.Format("DEBUG_{0}.txt", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
				string logFullPath = Path.Combine(logPath, logFileName);
				using (StreamWriter sw = new StreamWriter(logFullPath, false)) {
					sw.WriteLine(caught.ToString());
				}
			}
			catch (Exception caught2) {
				throw new ApplicationException(String.Format("{0}\nUnexpected logging error on write(2)", caught.Message), caught2);
			}
		}

		/// <summary>
		/// Writes a debug log of an exception.
		/// Only executed in debug builds.
		/// </summary>
		[Conditional("DEBUG")]
		public static void DebugWrite(Exception caught) {
			try {
				if (caught is ThreadAbortException) {
					return;
				}

				string functionName = "Unknown";
				if (caught.StackTrace != null) {
					string firstStackTraceLine = caught.StackTrace.Split('\n')[0];
					functionName = firstStackTraceLine.Trim().Split(" (".ToCharArray())[1];
				}
				string logFileName = string.Format("DEBUG_{0}.txt", DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss"));
				string logFullPath = Path.Combine(logPath, logFileName);
				using (StreamWriter sw = new StreamWriter(logFullPath, false)) {
					sw.WriteLine(caught.ToString());
				}
			}
			catch (Exception caught2) {
				throw new ApplicationException(String.Format("{0}\nUnexpected logging error on write(3)", caught.Message), caught2);
			}
		}
	}
}