using System;
using System.IO;
using Microsoft.DirectX.Direct3D;
using WorldWind.Renderable;

namespace WorldWind {
	/// <summary>
	/// 计算图片的本地路径和下载地址的基类
	/// </summary>
	public class ImageStore {
		
		#region Private Members
		protected string _dataDirectory;
		protected double _levelZeroTileSizeDegrees = 36;
		protected int _levelCount = 1;
		protected string _imageFileExtension;
		protected string _cacheDirectory;
		protected string _duplicateTexturePath;
		#endregion
		
		protected const string ValidExtensions = ".bmp.dds.dib.hdr.jpg.jpeg.pfm.png.ppm.tga.gif.tif";

		#region Properties
		/// <summary>
		/// 零层图片所跨的度数，默认是36度。WW采用的是二分法，层数加1，跨度减半。
		/// </summary>
		public double LevelZeroTileSizeDegrees {
			get {
				return _levelZeroTileSizeDegrees;
			}
			set {
				_levelZeroTileSizeDegrees = value;
			}
		}

		/// <summary>
		/// 图片的层数
		/// </summary>
		public int LevelCount {
			get {
				return _levelCount;
			}
			set {
				_levelCount = value;
			}
		}

		/// <summary>
		/// 图片文件的后缀（png，jpeg等），不包括（.）。
		/// </summary>
		public string ImageExtension {
			get {
				return _imageFileExtension;
			}
			set {
				// Strip any leading dot
				_imageFileExtension = value.Replace(".", "");
			}
		}

		/// <summary>
		/// 图层在缓存中的子目录
		/// </summary>
		public string CacheDirectory {
			get {
				return _cacheDirectory;
			}
			set {
				_cacheDirectory = value;
			}
		}

		/// <summary>
		/// 图层的数据目录（例如Logo，永久存储的图片等。）
		/// </summary>
		public string DataDirectory {
			get {
				return _dataDirectory;
			}
			set {
				_dataDirectory = value;
			}
		}

		/// <summary>
		/// 重复图片的路径(地址)，例如一直是海洋时，可以使用相同的图片
		/// </summary>
		public string DuplicateTexturePath {
			get {
				return _duplicateTexturePath;
			}
			set {
				_duplicateTexturePath = value;
			}
		}
		/// <summary>
		/// 改图层能否被下载
		/// </summary>
		public virtual bool IsDownloadableLayer {
			get {
				return false;
			}
		}
		#endregion

		/// <summary>
		/// 获取本地存储的路径
		/// </summary>
		/// <param name="qt">图层中的一块图片</param>
		/// <returns>图片应该保存的本地路径</returns>
		public virtual string GetLocalPath(QuadTile qt) {
			if (qt.Level >= _levelCount) {
				throw new ArgumentException(string.Format("Level {0} not available.", qt.Level));
			}

			string relativePath = String.Format(@"{0}\{1:D4}\{1:D4}_{2:D4}.{3}", qt.Level, qt.Row, qt.Col, _imageFileExtension);

			if (_dataDirectory != null) {
				// Search data directory first
				string rawFullPath = Path.Combine(_dataDirectory, relativePath);
				if (File.Exists(rawFullPath)) {
					return rawFullPath;
				}
			}

			// Try cache with default file extension
			string cacheFullPath = Path.Combine(_cacheDirectory, relativePath);
			if (File.Exists(cacheFullPath)) {
				return cacheFullPath;
			}

			// Try cache but accept any valid image file extension
			string cacheSearchPath = Path.GetDirectoryName(cacheFullPath);
			if (Directory.Exists(cacheSearchPath)) {
				foreach (string imageFile in Directory.GetFiles(cacheSearchPath, Path.GetFileNameWithoutExtension(cacheFullPath) + ".*")) {
					string extension = Path.GetExtension(imageFile).ToLower();
					if (ValidExtensions.IndexOf(extension) < 0) {
						continue;
					}

					return imageFile;
				}
			}

			return cacheFullPath;
		}

		/// <summary>
		/// 计算图片块的下载地址。如果该图层设置了重复图片路径，可以直接调用这个方法返回，否则需要重写该方法。
		/// </summary>
		protected virtual string GetDownloadUrl(QuadTile qt) {
			// No local image, return our "duplicate" tile if any
			if (_duplicateTexturePath != null && File.Exists(_duplicateTexturePath)) {
				return _duplicateTexturePath;
			}

			// No image available anywhere, give up
			return string.Empty;
		}

		/// <summary>
		/// 删除图片块的本地版本
		/// </summary>
		/// <param name="qt"></param>
		public virtual void DeleteLocalCopy(QuadTile qt) {
			string filename = GetLocalPath(qt);
			if (File.Exists(filename)) {
				File.Delete(filename);
			}
		}

		/// <summary>
		/// 将图片转换为DDS格式
		/// </summary>
		protected virtual void ConvertImage(Texture texture, string filePath) {
			if (filePath.ToLower().EndsWith(".dds")) {
				// Image is already DDS
				return;
			}

			// User has selected to convert downloaded images to DDS
			string convertedPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".dds");

			TextureLoader.Save(convertedPath, ImageFileFormat.Dds, texture);

			// Delete the old file
			try {
				File.Delete(filePath);
			}
			catch {}
		}
		/// <summary>
		/// 加载一个QuadTile，返回一个Texture
		/// </summary>
		/// <param name="qt"></param>
		/// <returns></returns>
		public Texture LoadFile(QuadTile qt) {
			string filePath = GetLocalPath(qt);
			qt.ImageFilePath = filePath;
			if (!File.Exists(filePath)) { // 文件被标记，
				string badFlag = filePath + ".txt";
				if (File.Exists(badFlag)) {
					FileInfo fi = new FileInfo(badFlag);
					if (DateTime.Now - fi.LastWriteTime < TimeSpan.FromDays(1)) { // 未过期，可能还在下载队列中
						return null;
					}
					// Timeout period elapsed, retry
					File.Delete(badFlag);
				}

				if (IsDownloadableLayer) { // 如果图层能够被下载，则重新开始下载
					QueueDownload(qt, filePath);
					return null;
				}

				if (DuplicateTexturePath == null) {
					// No image available, neither local nor online.
					return null;
				}

				filePath = DuplicateTexturePath;
			}

			// Use color key
			Texture texture = null;
			if (qt.QuadTileSet.HasTransparentRange) {
				texture = ImageHelper.LoadTexture(filePath, qt.QuadTileSet.ColorKey, qt.QuadTileSet.ColorKeyMax);
			}
			else {
				texture = ImageHelper.LoadTexture(filePath, qt.QuadTileSet.ColorKey);
			}

			if (qt.QuadTileSet.CacheExpirationTime != TimeSpan.MaxValue) {
				FileInfo fi = new FileInfo(filePath);
				DateTime expiry = fi.LastWriteTimeUtc.Add(qt.QuadTileSet.CacheExpirationTime);
				if (DateTime.UtcNow > expiry) {
					QueueDownload(qt, filePath);
				}
			}

			if (World.Settings.ConvertDownloadedImagesToDds) {
				ConvertImage(texture, filePath);
			}

			return texture;
		}

		private void QueueDownload(QuadTile qt, string filePath) {
			string url = GetDownloadUrl(qt);
			qt.QuadTileSet.AddToDownloadQueue(qt.QuadTileSet.Camera, new GeoSpatialDownloadRequest(qt, filePath, url));
		}
	}
}