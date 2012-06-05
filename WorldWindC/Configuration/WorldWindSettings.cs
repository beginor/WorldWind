using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using WorldWind.Configuration;
using WorldWind.Net;

namespace WorldWind.Configuration {
	/// <summary>
	/// World Wind persisted settings.
	/// </summary>
	public class WorldWindSettings : SettingsBase {
		public WorldWindSettings() : base() {}

		#region Cache
		// Cache settings
		private string cachePath = "Cache";
		private int cacheSizeMegaBytes = 10000;
		private TimeSpan cacheCleanupInterval = TimeSpan.FromMinutes(60);
		public static readonly DateTime ApplicationStartTime = DateTime.Now;
		private TimeSpan totalRunTime = TimeSpan.Zero;

		[Browsable(true), Category("Cache")]
		[Description("Directory to use for caching Image and Terrain files.")]
		public string CachePath {
			get {
				if (!Path.IsPathRooted(cachePath)) {
					return Path.Combine(WorldWindDirectory, cachePath);
				}
				return cachePath;
			}
			set {
				cachePath = value;
			}
		}

		[Browsable(true), Category("Cache")]
		[Description("Upper limit for amount of disk space to allow cache to use (MegaBytes).")]
		public int CacheSizeMegaBytes {
			get {
				return cacheSizeMegaBytes;
			}
			set {
				cacheSizeMegaBytes = value;
			}
		}

		[Browsable(true), Category("Cache")]
		[Description("Controls the frequency of cache cleanup.")]
		[XmlIgnore]
		public TimeSpan CacheCleanupInterval {
			get {
				return cacheCleanupInterval;
			}
			set {
				TimeSpan minimum = TimeSpan.FromMinutes(1);
				if (value < minimum) {
					value = minimum;
				}
				cacheCleanupInterval = value;
			}
		}

		/// <summary>
		/// Because Microsoft forgot to implement TimeSpan in their xml serializer.
		/// </summary>
		[Browsable(false)]
		[XmlElement("CacheCleanupInterval", DataType="duration")]
		public string CacheCleanupIntervalXml {
			get {
				if (cacheCleanupInterval < TimeSpan.FromSeconds(1)) {
					return null;
				}

				return XmlConvert.ToString(cacheCleanupInterval);
			}
			set {
				if (value == null
				    || value == string.Empty) {
					return;
				}

				cacheCleanupInterval = XmlConvert.ToTimeSpan(value);
			}
		}

		[Browsable(true), Category("Cache")]
		[Description("Total amount of time the application has been running.")]
		[XmlIgnore]
		public TimeSpan TotalRunTime {
			get {
				return totalRunTime + DateTime.Now.Subtract(ApplicationStartTime);
			}
			set {
				value = totalRunTime;
			}
		}

		/// <summary>
		/// Because Microsoft forgot to implement TimeSpan in their xml serializer.
		/// </summary>
		[Browsable(false)]
		[XmlElement("TotalRunTime", DataType="duration")]
		public string TotalRunTimeXml {
			get {
				if (TotalRunTime < TimeSpan.FromSeconds(1)) {
					return null;
				}

				return XmlConvert.ToString(TotalRunTime);
			}
			set {
				if (value == null
				    || value == string.Empty) {
					return;
				}

				totalRunTime = XmlConvert.ToTimeSpan(value);
			}
		}
		#endregion

		#region Plugin
		private ArrayList pluginsLoadedOnStartup = new ArrayList();

		[Browsable(true), Category("Plugin")]
		[Description("List of plugins loaded at startup.")]
		public ArrayList PluginsLoadedOnStartup {
			get {
				return pluginsLoadedOnStartup;
			}
		}
		#endregion

		#region Miscellaneous settings
		// Misc
		private string defaultWorld = "Earth";
		// default is to show the Configuration Wizard at startup
		private bool configurationWizardAtStartup = false;

		[Browsable(true), Category("Miscellaneous")]
		[Description("World to load on startup.")]
		public string DefaultWorld {
			get {
				return defaultWorld;
			}
			set {
				defaultWorld = value;
			}
		}

		[Browsable(true), Category("Miscellaneous")]
		[Description("Show Configuration Wizard on program startup.")]
		public bool ConfigurationWizardAtStartup {
			get {
				return configurationWizardAtStartup;
			}
			set {
				configurationWizardAtStartup = value;
			}
		}
		#endregion

		#region File System Settings
		// File system settings
		private string configPath = "Config";
		private string dataPath = "Data";

		[Browsable(true), Category("FileSystem")]
		[Description("Location where configuration files are stored.")]
		public string ConfigPath {
			get {
				if (!Path.IsPathRooted(configPath)) {
					return Path.Combine(WorldWindDirectory, configPath);
				}
				return configPath;
			}
			set {
				configPath = value;
			}
		}

		[Browsable(true), Category("FileSystem")]
		[Description("Location where data files are stored.")]
		public string DataPath {
			get {
				if (!Path.IsPathRooted(dataPath)) {
					return Path.Combine(WorldWindDirectory, dataPath);
				}
				return dataPath;
			}
			set {
				dataPath = value;
			}
		}

		/// <summary>
		/// World Wind application base directory ("C:\Program Files\NASA\Worldwind v1.2\") 
		/// </summary>
		public readonly string WorldWindDirectory = Path.GetDirectoryName(Application.ExecutablePath);
		#endregion

		// comment out ToString() to have namespace+class name being used as filename
		public override string ToString() {
			return "WorldWind";
		}
	}
}