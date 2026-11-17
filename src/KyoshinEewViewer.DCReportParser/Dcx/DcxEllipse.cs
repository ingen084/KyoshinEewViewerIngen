namespace KyoshinEewViewer.DCReportParser.Dcx;

/// <summary>
/// 対象地域を示す楕円 (WGS84)
/// </summary>
/// <param name="Latitude">中心の緯度 [度]</param>
/// <param name="Longitude">中心の経度 [度]</param>
/// <param name="SemiMajorAxis">長半径 [km]</param>
/// <param name="SemiMinorAxis">短半径 [km]</param>
/// <param name="Azimuth">方位角 [度]。東から長軸までの角度で、北向きを正とする</param>
public readonly record struct DcxEllipse(double Latitude, double Longitude, double SemiMajorAxis, double SemiMinorAxis, double Azimuth)
{
	// WGS84
	private const double EarthSemiMajorAxis = 6378137;
	private const double EarthFlattening = 1 / 298.257223563;
	private const double EarthSemiMinorAxis = EarthSemiMajorAxis * (1 - EarthFlattening);
	private const double EccentricitySquared = 2 * EarthFlattening - EarthFlattening * EarthFlattening;

	/// <summary>
	/// A12 の 1 コードあたりの緯度 [度]
	/// </summary>
	public const double MainLatitudeInterval = 180.0 / ((1 << 16) - 1);
	/// <summary>
	/// A13 の 1 コードあたりの経度 [度]
	/// </summary>
	public const double MainLongitudeInterval = 360.0 / ((1 << 17) - 1);

	/// <summary>
	/// 半径の最小値 [km]。緯度の格子間隔 (赤道上で約 0.305km) の格子を包含できる長さとして定義されている
	/// </summary>
	public static readonly double MinAxisLength = Math.PI * (EarthSemiMajorAxis / 1000) / ((1 << 16) - 1) / Math.Sqrt(2);
	/// <summary>
	/// 半径の最大値 [km]
	/// </summary>
	public const double MaxAxisLength = 2500;

	/// <summary>
	/// A12 の緯度コードから緯度を求める
	/// </summary>
	public static double GetMainLatitude(int code)
		=> -90 + code * MainLatitudeInterval;

	/// <summary>
	/// A13 の経度コードから経度を求める
	/// </summary>
	public static double GetMainLongitude(long code)
		=> -180 + code * MainLongitudeInterval;

	/// <summary>
	/// A14/A15/EX5/EX6 の半径コードから半径を求める
	/// </summary>
	public static double GetAxisLength(int code)
		=> Math.Pow(10, Math.Log10(MinAxisLength) + code / 31.0 * (Math.Log10(MaxAxisLength) - Math.Log10(MinAxisLength)));

	/// <summary>
	/// B1 の C3/C4 で補正した半径を求める
	/// </summary>
	/// <param name="code">A14/A15 の半径コード</param>
	/// <param name="refinement">C3/C4 の補正コード (0〜7)</param>
	public static double GetRefinedAxisLength(int code, int refinement)
	{
		var length = GetAxisLength(code);
		// 1つ小さいコードの半径との間 (コード 0 の場合は 0 との間) を 8 等分した値を差し引く
		var delta = code > 0 ? length - GetAxisLength(code - 1) : length;
		return length - refinement / 8.0 * delta;
	}

	/// <summary>
	/// A16 の方位角コードから方位角を求める
	/// </summary>
	public static double GetMainAzimuth(int code)
		=> -90 + code * 180.0 / (1 << 6);

	/// <summary>
	/// EX3 の緯度コードから緯度を求める
	/// </summary>
	public static double GetAdditionalLatitude(long code)
		=> -90 + code * 180.0 / ((1 << 17) - 1);

	/// <summary>
	/// EX4 の経度コードから経度を求める<br/>
	/// EX4 は 45〜225度を表すため、180度を超える場合は西経に変換する
	/// </summary>
	public static double GetAdditionalLongitude(long code)
	{
		var longitude = 45 + code * 180.0 / ((1 << 17) - 1);
		return longitude > 180 ? longitude - 360 : longitude;
	}

	/// <summary>
	/// EX7 の方位角コードから方位角を求める
	/// </summary>
	public static double GetAdditionalAzimuth(int code)
		=> -90 + code * 180.0 / (1 << 7);

	/// <summary>
	/// 楕円の境界を緯度経度の点列で求める<br/>
	/// 仕様書の内外判定 (中心を原点とする ENU 座標の水平成分) と一致する境界を返す
	/// </summary>
	/// <param name="segments">分割数</param>
	/// <returns>境界の点列。経度は中心から連続するように、±180度を超える値になることがある</returns>
	public (double Latitude, double Longitude)[] ToPolygon(int segments = 90)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);

		var (east, north, up) = GetEnuAxes(Latitude, Longitude);
		var center = ToEcef(Latitude, Longitude);
		var azimuth = Azimuth * Math.PI / 180;
		var cosAzimuth = Math.Cos(azimuth);
		var sinAzimuth = Math.Sin(azimuth);

		var result = new (double Latitude, double Longitude)[segments];
		for (var i = 0; i < segments; i++)
		{
			var theta = 2 * Math.PI * i / segments;
			var x = SemiMajorAxis * 1000 * Math.Cos(theta);
			var y = SemiMinorAxis * 1000 * Math.Sin(theta);
			var e = x * cosAzimuth - y * sinAzimuth;
			var n = x * sinAzimuth + y * cosAzimuth;

			// 接平面上の点から鉛直下向きに進み、楕円体面と交わる点を求める
			var q = center + e * east + n * north;
			var a = (up.X * up.X + up.Y * up.Y) / (EarthSemiMajorAxis * EarthSemiMajorAxis) + up.Z * up.Z / (EarthSemiMinorAxis * EarthSemiMinorAxis);
			var b = 2 * ((q.X * up.X + q.Y * up.Y) / (EarthSemiMajorAxis * EarthSemiMajorAxis) + q.Z * up.Z / (EarthSemiMinorAxis * EarthSemiMinorAxis));
			var c = (q.X * q.X + q.Y * q.Y) / (EarthSemiMajorAxis * EarthSemiMajorAxis) + q.Z * q.Z / (EarthSemiMinorAxis * EarthSemiMinorAxis) - 1;
			var discriminant = Math.Max(0, b * b - 4 * a * c);
			var u = (-b + Math.Sqrt(discriminant)) / (2 * a);

			var (latitude, longitude) = ToGeodetic(q + u * up);
			// 日付変更線をまたいでも点列が連続するよう、中心からの差で表す
			var deltaLongitude = (longitude - Longitude + 540) % 360 - 180;
			result[i] = (latitude, Longitude + deltaLongitude);
		}
		return result;
	}

	/// <summary>
	/// 指定した地点が楕円の内側にあるかを判定する (仕様書 5.9 の方法)
	/// </summary>
	public bool Contains(double latitude, double longitude)
		=> EvaluateEllipseEquation(latitude, longitude) < 1;

	/// <summary>
	/// 指定した地点を楕円の標準形の方程式に当てはめた値を求める。1 未満なら内側、1 なら楕円上
	/// </summary>
	/// <returns>楕円の値。中心から地平線より遠い (地球の裏側の) 地点は正の無限大</returns>
	public double EvaluateEllipseEquation(double latitude, double longitude)
	{
		var (east, north, up) = GetEnuAxes(Latitude, Longitude);
		var vector = ToEcef(latitude, longitude) - ToEcef(Latitude, Longitude);

		// 接平面へ正射影すると地球の裏側の地点も楕円内に入りうるため除外する
		if (Dot(GetEnuAxes(latitude, longitude).Up, up) <= 0)
			return double.PositiveInfinity;

		var e = Dot(vector, east);
		var n = Dot(vector, north);
		// 方位角だけ逆回転して楕円の軸に合わせる
		var azimuth = -Azimuth * Math.PI / 180;
		var x = e * Math.Cos(azimuth) - n * Math.Sin(azimuth);
		var y = e * Math.Sin(azimuth) + n * Math.Cos(azimuth);
		var a = SemiMajorAxis * 1000;
		var b = SemiMinorAxis * 1000;
		return x * x / (a * a) + y * y / (b * b);
	}

	private readonly record struct Vector3(double X, double Y, double Z)
	{
		public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		public static Vector3 operator *(double s, Vector3 v) => new(s * v.X, s * v.Y, s * v.Z);
	}

	private static double Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

	private static (Vector3 East, Vector3 North, Vector3 Up) GetEnuAxes(double latitude, double longitude)
	{
		var phi = latitude * Math.PI / 180;
		var lambda = longitude * Math.PI / 180;
		return (
			new(-Math.Sin(lambda), Math.Cos(lambda), 0),
			new(-Math.Sin(phi) * Math.Cos(lambda), -Math.Sin(phi) * Math.Sin(lambda), Math.Cos(phi)),
			new(Math.Cos(phi) * Math.Cos(lambda), Math.Cos(phi) * Math.Sin(lambda), Math.Sin(phi))
		);
	}

	// 楕円体高 0 として ECEF 座標に変換する
	private static Vector3 ToEcef(double latitude, double longitude)
	{
		var phi = latitude * Math.PI / 180;
		var lambda = longitude * Math.PI / 180;
		var n = EarthSemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * Math.Sin(phi) * Math.Sin(phi));
		return new(n * Math.Cos(phi) * Math.Cos(lambda), n * Math.Cos(phi) * Math.Sin(lambda), n * (1 - EccentricitySquared) * Math.Sin(phi));
	}

	private static (double Latitude, double Longitude) ToGeodetic(Vector3 v)
	{
		var p = Math.Sqrt(v.X * v.X + v.Y * v.Y);
		var phi = Math.Atan2(v.Z, p * (1 - EccentricitySquared));
		for (var i = 0; i < 5; i++)
		{
			var n = EarthSemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * Math.Sin(phi) * Math.Sin(phi));
			var h = p / Math.Cos(phi) - n;
			phi = Math.Atan2(v.Z, p * (1 - EccentricitySquared * n / (n + h)));
		}
		return (phi * 180 / Math.PI, Math.Atan2(v.Y, v.X) * 180 / Math.PI);
	}
}
