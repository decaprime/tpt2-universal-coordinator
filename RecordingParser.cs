using System.Numerics;

public class RecordingParser
{
	private record Recording(Vector2 Size, List<Vector2> Points);

	public record Result(UniversalCoordinates? Output = null, string? Warning = null, string? Error = null);

	public record UniversalCoordinates(List<UniversalCoord> Coordinates)
	{
		public string AsScript => string.Join(Environment.NewLine, Coordinates);

		private string tc = "\t";
		public string TabCharacter { get { return tc; } set { tc = value; } }

		private const string DEFAULT_INDEX = "_ci";
		public string IndexVariable { get; set; } = DEFAULT_INDEX;

		private bool IsDefaultIndex => IndexVariable == DEFAULT_INDEX;

		public string AsScriptWithMacro => $@"; Generated with https://universal-coordinator.deca.gg
; Use by setting {(IsDefaultIndex ? "RENAME_ME_INDEX" : IndexVariable)} and calling this script or jumping to the label 
{(IsDefaultIndex ? @$"global int RENAME_ME_INDEX
:local int {IndexVariable}
{IndexVariable} = RENAME_ME_INDEX" :
$":global int {IndexVariable}")}

#rvec(wx,hx,wy,hy) vec(width.d()* {{wx}}+height.d()* {{hx}},width.d()* {{wy}}+height.d()* {{hy}})

universal_click:
{tc}click(\
{string.Join(Environment.NewLine, Coordinates.Select((c, i) => $"{tc}{tc}if({IndexVariable} == {i,2}, {c.RVec}, \\"))}
{tc}{tc}{tc}vec(0.0,0.0){new(')', Coordinates.Count)} \
{tc})
";
	}

	public record UniversalCoord(Vector2 W, Vector2 H, Vector2 Error)
	{
		public string RVec => $"{{rvec({W.X,6:F3}, {H.X,6:F3}, {W.Y,6:F3}, {H.Y,6:F3})}}";

		public override string ToString() =>
			$"click(vec(width.d()* {W.X,6:F3}+height.d()* {H.X,6:F3},width.d()* {W.Y,6:F3}+height.d()* {H.Y,6:F3}))";
	}

	public static Result Parse(string bunchOfLines)
	{
		if (string.IsNullOrWhiteSpace(bunchOfLines))
			return new() { Error = "Input empty" };
		IEnumerable<string> recordings = bunchOfLines.Split(Environment.NewLine).Where(s => !string.IsNullOrWhiteSpace(s));

		if (recordings.Count() < 3) return new() { Error = "Requires 3 or more entries" };

		try
		{
			var parsedRecordings = recordings.Select(ParseRecording);
			var coords = DetermineCoords(parsedRecordings);
			return coords;
		}
		catch (Exception)
		{
			// TODO should suggest reporting github issue if it's reasonably sized Base64 at least, maybe missed compatability
			return new() { Error = "Failed to parse input as list of base64 recordings" };
		}
	}

	private static Result DetermineCoords(IEnumerable<Recording> entries)
	{
		string? warning = null;
		var numPoints = entries.First().Points.Count;
		var coordinates = new List<UniversalCoord>();
		if (entries.Any(r => r.Points.Count != numPoints))
			return new() { Error = "Not all recordings have the same number of points" };

		for (var i = 0; i < numPoints; i++)
		{
			Vector2 a1 = default, a2 = default, b = default, c = default;
			float ratio = 0;
			foreach (var recording in entries)
			{
				var size = recording.Size;
				var point = recording.Points[i];

				ratio += size.X * size.Y;
				c += size * size;

				a1 += point * size.X;
				a2 += point * size.Y;
				b += point * point;
			}

			var determinant = (c.X * c.Y) - (ratio * ratio);
			var w = ((c.Y * a1) - (ratio * a2)) / determinant;
			var h = ((c.X * a2) - (ratio * a1)) / determinant;
			var errSq = ((b - (a1 * w)) - (a2 * h)) / (i + 1);
			var err = new Vector2(MathF.Sqrt(errSq.X), MathF.Sqrt(errSq.Y));

			if (err.X > 8) warning = (warning ?? "") + $"Point {i + 1}'s X margin of error ± {err.X:F2}px{Environment.NewLine}";
			if (err.Y > 8) warning = (warning ?? "") + $"Point {i + 1}'s Y margin of error ± {err.Y:F2}px{Environment.NewLine}";
			coordinates.Add(new UniversalCoord(w, h, err));
		}

		return new Result(new UniversalCoordinates(coordinates), warning);
	}

	static Recording ParseRecording(string base64)
	{
		var buffer = Convert.FromBase64String(base64);
		using var memStream = new MemoryStream(buffer);
		using var reader = new BinaryReader(memStream);

		var scriptName = reader.ReadString();
		reader.ReadInt32();
		reader.ReadInt32();
		var numLines = reader.ReadInt32();

		Vector2 readCoord(BinaryReader br)
		{
			var genClick = br.ReadString();         // generic.click
			var nextOp = br.ReadString();           //vec.fromCoords or constant
			if (nextOp == "vec.fromCoords")
			{
				var constants = br.ReadString();    // constant
				var constType = br.ReadByte();      // 3 (double)
				var x = br.ReadDouble();            // x coord
				var constants2 = br.ReadString();   // constant
				var constType2 = br.ReadByte();     // 3 (double)
				var y = br.ReadDouble();            // y coord

				return new Vector2((float)x, (float)y);
			}
			else if (nextOp == "constant")
			{
				var constType = br.ReadByte();      // 5 (vec)
				var x = br.ReadSingle();            // x
				var y = br.ReadSingle();            // y

				return new Vector2(x, y);
			}
			else throw new InvalidDataException($"Expected to encounter vec.FromCoords or constant but was parsed as {nextOp}");
		}

		List<Vector2> points = new();
		var size = readCoord(reader); // should be first
		for (var i = 1; i < numLines; i++)
		{
			points.Add(readCoord(reader));
		}

		return new Recording(size, points);
	}
}