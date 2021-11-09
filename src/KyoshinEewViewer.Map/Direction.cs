using KyoshinEewViewer.Map;

namespace KyoshinEewViewer.Map;

public enum Direction
{
	North,
	Northeast,
	East,
	Southeast,
	South,
	Southwest,
	West,
	Northwest,
	None,
}
public static class DirectionExtensions
{
	public static PointD GetVector(this Direction direction)
		=> direction switch
		{
			Direction.North => new PointD(0, -1),
			Direction.Northeast => new PointD(1, -1).Normalize(),
			Direction.East => new PointD(1, 0),
			Direction.Southeast => new PointD(1, 1).Normalize(),
			Direction.South => new PointD(0, 1),
			Direction.Southwest => new PointD(-1, 1).Normalize(),
			Direction.West => new PointD(-1, 0),
			Direction.Northwest => new PointD(-1, -1).Normalize(),
			Direction.None => new PointD(),
			_ => throw new System.InvalidOperationException(),
		};
}
