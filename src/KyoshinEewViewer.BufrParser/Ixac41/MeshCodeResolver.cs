namespace KyoshinEewViewer.BufrParser.Ixac41;

/// <summary>
/// 地域メッシュの階層番号（1次〜4分の1メッシュ）から南西端の緯度経度を求める。
/// </summary>
public static class MeshCodeResolver
{
	/// <summary>
	/// 最終セル（4分の1地域メッシュ）1マスあたりの緯度方向の大きさ（度）。約250m。
	/// </summary>
	public const double CellLatitudeDegrees = 7.5 / 3600.0;

	/// <summary>
	/// 最終セル（4分の1地域メッシュ）1マスあたりの経度方向の大きさ（度）。約250m。
	/// </summary>
	public const double CellLongitudeDegrees = 11.25 / 3600.0;

	/// <summary>
	/// 1次〜4分の1メッシュの階層番号から、4分の1地域メッシュの南西端座標を求める。
	/// </summary>
	/// <param name="primaryMeshLatitude">1次メッシュ緯度番号（0-05-240）。</param>
	/// <param name="primaryMeshLongitude">1次メッシュ経度番号（0-06-240）。</param>
	/// <param name="secondaryMeshLatitude">2次メッシュ緯度番号（0-05-241、0〜7）。</param>
	/// <param name="secondaryMeshLongitude">2次メッシュ経度番号（0-06-241、0〜7）。</param>
	/// <param name="tertiaryMeshLatitude">3次メッシュ緯度番号（0-05-242、0〜9）。</param>
	/// <param name="tertiaryMeshLongitude">3次メッシュ経度番号（0-06-242、0〜9）。</param>
	/// <param name="halfMeshNumber">2分の1地域メッシュ番号（0-05-243、1〜4）。</param>
	/// <param name="quarterMeshNumber">4分の1地域メッシュ番号（0-06-243、1〜4）。</param>
	public static (double Latitude, double Longitude) ResolveSouthWest(
		int primaryMeshLatitude, int primaryMeshLongitude,
		int secondaryMeshLatitude, int secondaryMeshLongitude,
		int tertiaryMeshLatitude, int tertiaryMeshLongitude,
		int halfMeshNumber, int quarterMeshNumber)
	{
		var lat0 = primaryMeshLatitude * (2.0 / 3.0);
		var lon0 = primaryMeshLongitude + 100.0;

		var lat1 = lat0 + secondaryMeshLatitude * (5.0 / 60.0);
		var lon1 = lon0 + secondaryMeshLongitude * (7.5 / 60.0);

		var lat2 = lat1 + tertiaryMeshLatitude * (0.5 / 60.0);
		var lon2 = lon1 + tertiaryMeshLongitude * (0.75 / 60.0);

		// 2分の1・4分の1メッシュ番号(1〜4)は南西=1,南東=2,北西=3,北東=4の順で採番される
		var halfCol = (halfMeshNumber - 1) % 2;
		var halfRow = (halfMeshNumber - 1) / 2;
		var quarterCol = (quarterMeshNumber - 1) % 2;
		var quarterRow = (quarterMeshNumber - 1) / 2;

		var latitude = lat2 + halfRow * (15.0 / 3600.0) + quarterRow * (7.5 / 3600.0);
		var longitude = lon2 + halfCol * (22.5 / 3600.0) + quarterCol * (11.25 / 3600.0);

		return (latitude, longitude);
	}
}
