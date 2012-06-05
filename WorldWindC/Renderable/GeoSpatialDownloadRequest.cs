using System;
using System.IO;
using System.Net;
using WorldWind.Net;

namespace WorldWind.Renderable {
	public class GeoSpatialDownloadRequest : IDisposable {
		public float ProgressPercent;
		private WebDownload download;
		private string m_localFilePath;
		private string m_url;
		private QuadTile m_quadTile;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.Renderable.GeoSpatialDownloadRequest"/> class.
		/// </summary>
		/// <param name="quadTile"></param>
		public GeoSpatialDownloadRequest(QuadTile quadTile, string localFilePath, string downloadUrl) {
			m_quadTile = quadTile;
			m_url = downloadUrl;
			m_localFilePath = localFilePath;
		}

		/// <summary>
		/// Whether the request is currently being downloaded
		/// </summary>
		public bool IsDownloading {
			get {
				return (download != null);
			}
		}

		public bool IsComplete {
			get {
				if (download == null) {
					return true;
				}
				return download.IsComplete;
			}
		}

		public QuadTile QuadTile {
			get {
				return m_quadTile;
			}
		}

		public double TileWidth {
			get {
				return m_quadTile.East - m_quadTile.West;
			}
		}

		private void DownloadComplete(WebDownload downloadInfo) {
			try {
				downloadInfo.Verify();

				m_quadTile.QuadTileSet.NumberRetries = 0;

				// Rename temp file to real name
				File.Delete(m_localFilePath);
				File.Move(downloadInfo.SavedFilePath, m_localFilePath);

				// Make the quad tile reload the new image
				m_quadTile.DownloadRequest = null;
				m_quadTile.Initialize();
			}
			catch (WebException caught) {
				HttpWebResponse response = caught.Response as HttpWebResponse;
				if (response != null
				    && response.StatusCode == HttpStatusCode.NotFound) {
					using (File.Create(m_localFilePath + ".txt")) {}
					return;
				}
				m_quadTile.QuadTileSet.NumberRetries++;
			}
			catch {
				using (File.Create(m_localFilePath + ".txt")) {}
				if (File.Exists(downloadInfo.SavedFilePath)) {
					File.Delete(downloadInfo.SavedFilePath);
				}
			}
			finally {
				#region by zzm
				if (download != null) {
					download.IsComplete = true;
				}
				#endregion

				m_quadTile.QuadTileSet.RemoveFromDownloadQueue(this);

				//Immediately queue next download
				m_quadTile.QuadTileSet.ServiceDownloadQueue();
			}
		}

		public virtual void StartDownload() {
			QuadTile.IsDownloadingImage = true;
			download = new WebDownload(m_url);
			download.DownloadType = DownloadType.Wms;
			download.SavedFilePath = m_localFilePath + ".tmp";
			download.ProgressCallback += new DownloadProgressHandler(UpdateProgress);
			download.CompleteCallback += new DownloadCompleteHandler(DownloadComplete);
			download.BackgroundDownloadFile();
		}

		private void UpdateProgress(int pos, int total) {
			if (total == 0) {
				// When server doesn't provide content-length, use this dummy value to at least show some progress.
				total = 50*1024;
			}
			pos = pos%(total + 1);
			ProgressPercent = (float) pos/total;
		}

		public virtual void Cancel() {
			if (download != null) {
				download.Cancel();
			}
		}

		public override string ToString() {
			return QuadTile.QuadTileSet.ImageStore.GetLocalPath(QuadTile);
		}

		#region IDisposable Members
		public virtual void Dispose() {
			if (download != null) {
				download.Dispose();
				download = null;
			}
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}