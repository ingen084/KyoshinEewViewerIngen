namespace TopoJsonConverter
{
	public static class Extensions
	{
		public static DoubleVector[] ToLocations(this IntVector[] points, TopologyMap map)
		{
			var result = new DoubleVector[points.Length];
			double x = 0;
			double y = 0;
			for (var i = 0; i < result.Length; i++)
				result[i] = new DoubleVector((x += points[i].X) * map.Scale.X + map.Translate.X, (y += points[i].Y) * map.Scale.Y + map.Translate.Y);
			return result;
		}
	}
}
