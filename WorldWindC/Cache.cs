using System;
using System.Collections;
using System.IO;
using System.Threading;
using WorldWind.Utilities;

namespace WorldWind {
	/// <summary>
	/// 管理磁盘上的缓存,使之保持在一定的限额之内.
	/// </summary>
	public class Cache : IDisposable {
		///<summary>
		/// 缓存默认最大值为2G
		/// </summary>
		public long CacheUpperLimit = 2L*1024L*1024L*1024L;
		///<summary>
		/// 缓存默认开始清理值为1.5G
		/// </summary>
		public long CacheLowerLimit = 1536L*1024L*1024L;
		/// <summary>
		/// 缓存的存放目录
		/// </summary>
		public string CacheDirectory;
		/// <summary>
		/// 清理缓存的时间间隔
		/// </summary>
		public TimeSpan CleanupFrequency;
		/// <summary>
		/// 缓存清理计时器
		/// </summary>
		private Timer _timer;

		/// <summary>
		/// 创建一个<see cref= "T:WorldWind.Cache"/>新实例.
		/// </summary>
		/// <param name="cacheDirectory">缓存的存放目录</param>
		/// <param name="cleanupFrequencyInterval">进行清理的时间间隔</param>
		/// <param name="totalRunTime">程序运行至现在的全部运行时间</param>
		public Cache(string cacheDirectory, TimeSpan cleanupFrequencyInterval, TimeSpan totalRunTime) {
			this.CleanupFrequency = cleanupFrequencyInterval;
			this.CacheDirectory = cacheDirectory;
			Directory.CreateDirectory(this.CacheDirectory);
			// Start the timer
			double firstDueSeconds = cleanupFrequencyInterval.TotalSeconds - totalRunTime.TotalSeconds % cleanupFrequencyInterval.TotalSeconds;
			_timer = new Timer(new TimerCallback(OnTimer), null, (long) (firstDueSeconds*1000), (long) cleanupFrequencyInterval.TotalMilliseconds);
		}

		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="cacheDirectory">缓存的路径</param>
		/// <param name="cacheLowerLimit">缓存下限</param>
		/// <param name="cacheUpperLimit">缓存上限</param>
		/// <param name="cleanupFrequencyInterval">进行清理的时间间隔</param>
		/// <param name="totalRunTime">程序运行至现在的全部运行时间</param>
		public Cache(string cacheDirectory, long cacheLowerLimit, long cacheUpperLimit, TimeSpan cleanupFrequencyInterval, TimeSpan totalRunTime) : this(cacheDirectory, cleanupFrequencyInterval, totalRunTime) {
			this.CacheLowerLimit = cacheLowerLimit;
			this.CacheUpperLimit = cacheUpperLimit;
		}

		/// <summary>
		/// 监视缓存, 确认保持在一定的限额之内
		/// </summary>
		private void OnTimer(object state) {
			try {
				// 缓存维护会在一个独立的线程中运行, 因此将线程的优先级设置为较低, 这样对前台线程的影响较小.
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