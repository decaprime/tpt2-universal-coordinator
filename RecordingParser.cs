using System.Numerics;

public class RecordingParser
{
	public static (string code, string error) Parse(string bunchOfLines)
	{
		if (string.IsNullOrWhiteSpace(bunchOfLines))
			return (null, error: "Input empty");
		IEnumerable<string> recordings = bunchOfLines.Split(Environment.NewLine).Where(s => !string.IsNullOrWhiteSpace(s));

		if (recordings.Count() < 3) return (null, error: "Requires 3 or more entries");

		try
		{
			var parsedRecordings = recordings.Select(ParseRecording);
			var coords = DetermineCoords(parsedRecordings);
			return coords;
		}
		catch (Exception)
		{
			return (null, error: "Failed to parse input as list of base64 recordings");
		}
	}

	static (string code, string error) DetermineCoords(IEnumerable<(Vector2 size, List<Vector2> points)> entries)
	{
		string result = "", errors = "";
		var numPoints = entries.First().points.Count;

		if (entries.Any(r => r.points.Count != numPoints))
			return (code: null, error: "Not all recordings have the same number of points");

		for (var i = 0; i < numPoints; i++)
		{
			Vector2 a1 = default, a2 = default, b = default, c = default, sol1 = default, sol2 = default, err = default;
			float ratio = 0, determinant = 0;
			foreach (var recording in entries)
			{
				var size = recording.size;
				var point = recording.points[i];

				ratio += size.X * size.Y;
				c += size * size;

				a1 += point * size.X;
				a2 += point * size.Y;
				b += point * point;
			}

			determinant = (c.X * c.Y) - (ratio * ratio);
			sol1 = ((c.Y * a1) - (ratio * a2)) / determinant;
			sol2 = ((c.X * a2) - (ratio * a1)) / determinant;
			err = ((b - (a1 * sol1)) - (a2 * sol2)) / (i + 1);

			result += $"click(vec(width.d()*{sol1.X:F3}+height.d()*{sol2.X:F3},width.d()*{sol1.Y:F3}+height.d()*{sol2.Y:F3})){Environment.NewLine}";
			if (err.X > 8.0) errors += $"Point {i + 1} X error = {err.X:F2}{Environment.NewLine}";
			if (err.Y > 8.0) errors += $"Point {i + 1} Y error = {err.Y:F2}{Environment.NewLine}";
		}
		return (result, errors);
	}

	static (Vector2 size, List<Vector2> points) ParseRecording(string base64)
	{
		var s = Convert.FromBase64String(base64);
		using var ms = new MemoryStream(s);
		using var br = new System.IO.BinaryReader(ms);

		var name = br.ReadString();
		br.ReadInt32();
		br.ReadInt32();
		var lines = br.ReadInt32();

		Vector2 readCoord(BinaryReader br)
		{
			br.ReadString(); // generic.click
			br.ReadString(); //vec.fromCoords
			br.ReadString(); // constants
			br.ReadByte(); // 3 (double)
			var x = br.ReadDouble(); // x coord
			br.ReadString(); // constant
			br.ReadByte(); // 3 (double)
			var y = br.ReadDouble(); // y coord
			return new Vector2((float)x, (float)y); // technically losing precision here, I don't think it matters.
		}

		List<Vector2> points = new();
		var size = readCoord(br); // should be first
		for (var i = 1; i < lines; i++)
		{
			points.Add(readCoord(br));
		}

		return (size, points);
	}
}