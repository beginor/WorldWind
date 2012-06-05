using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind {
	public sealed class LayerDescriptor {
		private String category;
		private String name;
		private double opacity;

		public LayerDescriptor() {
		}

		public LayerDescriptor(String category, String name, double opacity) {
			this.category = category;
			this.name = name;
			this.opacity = opacity;
		}

		public String Category {
			get {
				return this.category;
			}
			set {
				this.category = value;
			}
		}

		public String Name {
			get {
				return this.name;
			}
			set {
				this.name = value;
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
