namespace Snap.Engine.Inputs;

internal static class SdlControllerDbParser
{
	public static List<SdlControllerEntry> LoadAll()
	{
		byte[] raw = EmbeddedResources.GetSdlDatabase();
		string text = Encoding.UTF8.GetString(raw);
		var list = new List<SdlControllerEntry>();

		using var reader = new StringReader(text);
		string line;
		while ((line = reader.ReadLine()) != null)
		{
			line = line.Trim();
			if (line.Length == 0 || line[0] == '#') continue;

			var parts = line.Split([','], 3);
			if (parts.Length < 3) continue;

			var map = ParseMap(parts[2]);
			list.Add(new SdlControllerEntry(parts[0], parts[1], map));
		}
		return list;
	}

	private static Dictionary<string, SdlBinding> ParseMap(string mapping)
	{
		var dict = new Dictionary<string, SdlBinding>();

		foreach (var token in mapping.Split(','))
		{
			var kv = token.Split(':', 2);
			if (kv.Length != 2) continue;

			string key = kv[0];
			string val = kv[1];

			if (val.StartsWith('b') && int.TryParse(val.AsSpan(1), out var btnIdx))
			{
				dict[key] = new SdlBinding(SdlBindingType.Button, btnIdx);
			}
			else if (val.StartsWith('a') && int.TryParse(val.AsSpan(1), out var axisIdx))
			{
				dict[key] = new SdlBinding(SdlBindingType.Axis, axisIdx);
			}
			else if (val.StartsWith('h'))
			{
				var parts = val[1..].Split('.');
				if (parts.Length == 2 && int.TryParse(parts[0], out var hatIdx) && int.TryParse(parts[1], out var hatMask))
				{
					dict[key] = new SdlBinding(SdlBindingType.Hat, hatIdx, hatMask);
				}
			}
		}

		return dict;
	}
}
