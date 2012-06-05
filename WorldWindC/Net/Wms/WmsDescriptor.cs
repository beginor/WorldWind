using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind.Net.Wms {

	public sealed class WmsDescriptor {

		private Uri url;
		private double opacity;

		public WmsDescriptor() {
		}

		public WmsDescriptor(Uri url, double opacity) {
			this.url = url;
			this.opacity = opacity;
		}

		public Uri Url {
			get {
				return this.url;
			}
			set {
				this.url = value;
			}
		}

		public double Opacity {
			get {
				return this.opacity;
			}
			set {
				this.opacity = value;
			}
		}
	}
}