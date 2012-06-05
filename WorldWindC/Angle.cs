using System;
using System.Globalization;

namespace WorldWind {
	/// <summary>
	/// A geometric angle
	/// </summary>
	public struct Angle {
		[NonSerialized]
		public double Radians;

		/// <summary>
		/// Creates a new angle from angle in radians.
		/// </summary>
		public static Angle FromRadians(double radians) {
			Angle res = new Angle();
			res.Radians = radians;
			return res;
		}

		/// <summary>
		/// Creates a new angle from angle in degrees.
		/// </summary>
		public static Angle FromDegrees(double degrees) {
			Angle res = new Angle();
			res.Radians = Math.PI*degrees/180.0;
			return res;
		}

		/// <summary>
		/// A zeroed angle
		/// </summary>
		public static readonly Angle Zero;

		/// <summary>
		/// Minimum value for angle
		/// </summary>
		public static readonly Angle MinValue = FromRadians(double.MinValue);

		/// <summary>
		/// Maximum value for angle
		/// </summary>
		public static readonly Angle MaxValue = FromRadians(double.MaxValue);

		/// <summary>
		/// Angle containing Not a Number
		/// </summary>
		public static readonly Angle NaN = FromRadians(double.NaN);

		public double Degrees {
			get {
				return MathEngine.RadiansToDegrees(this.Radians);
			}
			set {
				this.Radians = MathEngine.DegreesToRadians(value);
			}
		}

		/// <summary>
		/// Returns the absolute value of the specified angle
		/// </summary>
		public static Angle Abs(Angle a) {
			return FromRadians(Math.Abs(a.Radians));
		}

		/// <summary>
		/// Checks for angle containing "Not a Number"
		/// </summary>
		public static bool IsNaN(Angle a) {
			return double.IsNaN(a.Radians);
		}

		public override bool Equals(object obj) {
			if (obj == null
			    || GetType() != obj.GetType()) {
				return false;
			}
			Angle a = (Angle) obj;
			return Math.Abs(Radians - a.Radians) < Single.Epsilon;
		}

		public static bool operator ==(Angle a, Angle b) {
			return Math.Abs(a.Radians - b.Radians) < Single.Epsilon;
		}

		public static bool operator !=(Angle a, Angle b) {
			return Math.Abs(a.Radians - b.Radians) > Single.Epsilon;
		}

		public static bool operator <(Angle a, Angle b) {
			return a.Radians < b.Radians;
		}

		public static bool operator >(Angle a, Angle b) {
			return a.Radians > b.Radians;
		}

		public static Angle operator +(Angle a, Angle b) {
			double res = a.Radians + b.Radians;
			return FromRadians(res);
		}

		public static Angle operator -(Angle a, Angle b) {
			double res = a.Radians - b.Radians;
			return FromRadians(res);
		}

		public static Angle operator *(Angle a, double times) {
			return FromRadians(a.Radians*times);
		}

		public static Angle operator *(double times, Angle a) {
			return FromRadians(a.Radians*times);
		}

		public static Angle operator /(double divisor, Angle a) {
			return FromRadians(a.Radians/divisor);
		}

		public static Angle operator /(Angle a, double divisor) {
			return FromRadians(a.Radians/divisor);
		}

		public override int GetHashCode() {
			return (int) (Radians*100000);
		}

		/// <summary>
		/// Normalizes the angle so it is between 0�� and 360��.
		/// </summary>
		public void Normalize() {
			if (Radians > Math.PI*2) {
				Radians -= Math.PI*2;
			}
			if (Radians < -Math.PI*2) {
				Radians += Math.PI*2;
			}
		}

		/// <summary>
		/// Converts degrees to degrees/minutes/seconds
		/// </summary>
		/// <returns>String on format dd��mm'ss.sss"</returns>
		public string ToStringDms() {
			double decimalDegrees = this.Degrees;
			double d = Math.Abs(decimalDegrees);
			double m = (60*(d - Math.Floor(d)));
			double s = (60*(m - Math.Floor(m)));

			return String.Format("{0:D3}��{1:D2}'{2:D2}\"", (int) d*Math.Sign(decimalDegrees), (int) m, (int) s);
		}

		public override string ToString() {
			return Degrees.ToString(CultureInfo.InvariantCulture) + "��";
		}
	}
}