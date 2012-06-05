using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind.Renderable {

	internal enum DownloadState {
		Pending,
		Downloading,
		Converting,
		Cancelled,
	}
}
