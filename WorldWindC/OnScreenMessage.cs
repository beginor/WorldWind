using System;
using System.Collections.Generic;
using System.Text;

namespace WorldWind {

	public sealed class OnScreenMessage {
		private String message;
		private double x;
		private double y;

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.OnScreenMessage"/> class.
		/// </summary>
		public OnScreenMessage() {
		}

		/// <summary>
		/// Initializes a new instance of the <see cref= "T:WorldWind.OnScreenMessage"/> class.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="message"></param>
		public OnScreenMessage(double x, double y, String message) {
			this.x = x;
			this.y = y;
			this.message = message;
		}

		public String Message {
			get {
				return this.message;
			}
			set {
				this.message = value;
			}
		}

		public double X {
			get {
				return this.x;
			}
			set {
				this.x = value;
			}
		}

		public double Y {
			get {
				return this.y;
			}
			set {
				this.y = value;
			}
		}
	}

}
