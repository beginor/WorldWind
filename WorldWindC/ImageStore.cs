using System;
using System.IO;
using Microsoft.DirectX.Direct3D;
using WorldWind.Renderable;

namespace WorldWind {
	/// <summary>
	/// ����ͼƬ�ı���·�������ص�ַ�Ļ���
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
		/// ���ͼƬ����Ķ�����Ĭ����36�ȡ�WW���õ��Ƕ��ַ���������1����ȼ��롣
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
		/// ͼƬ�Ĳ���
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
		/// ͼƬ�ļ��ĺ�׺��png��jpeg�ȣ�����������.����
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
		/// ͼ���ڻ����е���Ŀ¼
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
		/// ͼ�������Ŀ¼������Logo�����ô洢��ͼƬ�ȡ���
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
		/// �ظ�ͼƬ��·��(��ַ)������һֱ�Ǻ���ʱ������ʹ����ͬ��ͼƬ
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
		/// ��ͼ���ܷ�����
		/// </summary>
		public virtual bool IsDownloadableLayer {
			get {
				return false;
			}
		}
		#endregion

		/// <summary>
		/// ��ȡ���ش洢��·��
		/// </summary>
		/// <param name="qt">ͼ���е�һ��ͼƬ</param>
		/// <returns>ͼƬӦ�ñ���ı���·��</returns>
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
		/// ����ͼƬ������ص�ַ�������ͼ���������ظ�ͼƬ·��������ֱ�ӵ�������������أ�������Ҫ��д�÷�����
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
		/// ɾ��ͼƬ��ı��ذ汾
		/// </summary>
		/// <param name="qt"></param>
		public virtual void DeleteLocalCopy(QuadTile qt) {
			string filename = GetLocalPath(qt);
			if (File.Exists(filename)) {
				File.Delete(filename);
			}
		}

		/// <summary>
		/// ��ͼƬת��ΪDDS��ʽ
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
		/// ����һ��QuadTile������һ��Texture
		/// </summary>
		/// <param name="qt"></param>
		/// <returns></returns>
		public Texture LoadFile(QuadTile qt) {
			string filePath = GetLocalPath(qt);
			qt.ImageFilePath = filePath;
			if (!File.Exists(filePath)) { // �ļ�����ǣ�
				string badFlag = filePath + ".txt";
				if (File.Exists(badFlag)) {
					FileInfo fi = new FileInfo(badFlag);
					if (DateTime.Now - fi.LastWriteTime < TimeSpan.FromDays(1)) { // δ���ڣ����ܻ������ض�����
						return null;
					}
					// Timeout period elapsed, retry
					File.Delete(badFlag);
				}

				if (IsDownloadableLayer) { // ���ͼ���ܹ������أ������¿�ʼ����
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