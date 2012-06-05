using System;
using System.Collections;
using System.IO;
using System.Threading;
using WorldWind.Utilities;

namespace WorldWind {
	/// <summary>
	/// ��������ϵĻ���,ʹ֮������һ�����޶�֮��.
	/// </summary>
	public class Cache : IDisposable {
		///<summary>
		/// ����Ĭ�����ֵΪ2G
		/// </summary>
		public long CacheUpperLimit = 2L*1024L*1024L*1024L;
		///<summary>
		/// ����Ĭ�Ͽ�ʼ����ֵΪ1.5G
		/// </summary>
		public long CacheLowerLimit = 1536L*1024L*1024L;
		/// <summary>
		/// ����Ĵ��Ŀ¼
		/// </summary>
		public string CacheDirectory;
		/// <summary>
		/// �������ʱ����
		/// </summary>
		public TimeSpan CleanupFrequency;
		/// <summary>
		/// ���������ʱ��
		/// </summary>
		private Timer _timer;

		/// <summary>
		/// ����һ��<see cref= "T:WorldWind.Cache"/>��ʵ��.
		/// </summary>
		/// <param name="cacheDirectory">����Ĵ��Ŀ¼</param>
		/// <param name="cleanupFrequencyInterval">���������ʱ����</param>
		/// <param name="totalRunTime">�������������ڵ�ȫ������ʱ��</param>
		public Cache(string cacheDirectory, TimeSpan cleanupFrequencyInterval, TimeSpan totalRunTime) {
			this.CleanupFrequency = cleanupFrequencyInterval;
			this.CacheDirectory = cacheDirectory;
			Directory.CreateDirectory(this.CacheDirectory);
			// Start the timer
			double firstDueSeconds = cleanupFrequencyInterval.TotalSeconds - totalRunTime.TotalSeconds % cleanupFrequencyInterval.TotalSeconds;
			_timer = new Timer(new TimerCallback(OnTimer), null, (long) (firstDueSeconds*1000), (long) cleanupFrequencyInterval.TotalMilliseconds);
		}

		/// <summary>
		/// ���캯��
		/// </summary>
		/// <param name="cacheDirectory">�����·��</param>
		/// <param name="cacheLowerLimit">��������</param>
		/// <param name="cacheUpperLimit">��������</param>
		/// <param name="cleanupFrequencyInterval">���������ʱ����</param>
		/// <param name="totalRunTime">�������������ڵ�ȫ������ʱ��</param>
		public Cache(string cacheDirectory, long cacheLowerLimit, long cacheUpperLimit, TimeSpan cleanupFrequencyInterval, TimeSpan totalRunTime) : this(cacheDirectory, cleanupFrequencyInterval, totalRunTime) {
			this.CacheLowerLimit = cacheLowerLimit;
			this.CacheUpperLimit = cacheUpperLimit;
		}

		/// <summary>
		/// ���ӻ���, ȷ�ϱ�����һ�����޶�֮��
		/// </summary>
		private void OnTimer(object state) {
			try {
				// ����ά������һ���������߳�������, ��˽��̵߳����ȼ�����Ϊ�ϵ�, ������ǰ̨�̵߳�Ӱ���С.
				Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

				// dirSize is reported as the total of the file sizes, in bytes
				// TODO: use the on-disk filesize, not FileInfo.Length, to calculate dirSize
				long dirSize = GetDirectorySize(new DirectoryInfo(this.CacheDirectory));
				if (dirSize < this.CacheUpperLimit) {
					return;
				}

				ArrayList fileInfoList = GetDirectoryFileInfoList(new DirectoryInfo(this.CacheDirectory));
				while (dirSize > this.CacheLowerLimit) {
					if (fileInfoList.Count <= 100) {
						break;
					}

					FileInfo oldestFile = null;
					foreach (FileInfo curFile in fileInfoList) {
						if (oldestFile == null) {
							oldestFile = curFile;
							continue;
						}

						if (curFile.LastAccessTimeUtc
						    < oldestFile.LastAccessTimeUtc) {
							oldestFile = curFile;
						}
					}

					fileInfoList.Remove(oldestFile);
					dirSize -= oldestFile.Length;
					try {
						File.Delete(oldestFile.FullName);

						// Recursively remove empty directories
						string directory = oldestFile.Directory.FullName;
						while (Directory.GetFileSystemEntries(directory).Length == 0) {
							Directory.Delete(directory);
							directory = Path.GetDirectoryName(directory);
						}
					}
					catch (IOException) {
						// Ignore non-removable file - move on to next
					}
				}
			}
			catch (Exception caught) {
				Log.Write("CACH", caught.Message);
			}
		}

		private static ArrayList GetDirectoryFileInfoList(DirectoryInfo inDir) {
			ArrayList returnList = new ArrayList();
			foreach (DirectoryInfo subDir in inDir.GetDirectories()) {
				returnList.AddRange(GetDirectoryFileInfoList(subDir));
			}
			foreach (FileInfo fi in inDir.GetFiles()) {
				returnList.Add(fi);
			}
			return returnList;
		}

		private static long GetDirectorySize(DirectoryInfo inDir) {
			long returnBytes = 0;
			foreach (DirectoryInfo subDir in inDir.GetDirectories()) {
				returnBytes += GetDirectorySize(subDir);
			}
			foreach (FileInfo fi in inDir.GetFiles()) {
				try {
					returnBytes += fi.Length;
				}
				catch (IOException) {
					// Ignore files that may have disappeared since we started scanning.
				}
			}
			return returnBytes;
		}

		public override string ToString() {
			return CacheDirectory;
		}

		#region IDisposable Members
		public void Dispose() {
			if (_timer != null) {
				_timer.Dispose();
				_timer = null;
			}
		}
		#endregion
	}
}