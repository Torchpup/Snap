namespace Snap.Engine.Assets.LDTKImporter;

/// <summary>
/// Represents a generic setting or custom field value attached to a level or entity.
/// Stores untyped data internally and provides typed accessors for retrieving values.
/// </summary>
public class MapSetting
{
	/// <summary>
	/// Internal object backing the actual setting value.
	/// </summary>
	protected object Value { get; set; }

	/// <summary>
	/// Casts the stored value to the specified type.
	/// </summary>
	/// <typeparam name="T">The expected return type.</typeparam>
	/// <returns>The setting value cast to type <typeparamref name="T"/>.</returns>
	/// <exception cref="InvalidCastException">Thrown if the stored value is not compatible with the requested type.</exception>
	public T ValueAs<T>() => (T)Value;





	public static bool Contains(Dictionary<uint, MapSetting> settings, string name)
		=> settings.ContainsKey(HashHelpers.Cache32(name));




	/// <summary>
	/// Retrieves a boolean setting by name from a dictionary of typed map settings.
	/// </summary>
	/// <param name="settings">The collection of field settings indexed by hash.</param>
	/// <param name="name">The original string name of the setting to retrieve.</param>
	/// <returns>The associated <see cref="bool"/> value if found and valid.</returns>
	/// <exception cref="Exception">
	/// Thrown when:
	/// <list type="bullet">
	///   <item><description>The named setting does not exist in the dictionary.</description></item>
	///   <item><description>The setting exists but is not a boolean type.</description></item>
	/// </list>
	/// </exception>
	public static bool GetBoolSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not bool)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(bool)}'.");

		return result.ValueAs<bool>();
	}
	public static bool TryGetBoolSetting(Dictionary<uint, MapSetting> settings, string name, out bool setting)
	{
		try
		{
			setting = GetBoolSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves an integer setting by name from a collection of map settings.
	/// </summary>
	/// <param name="settings">The dictionary of field values keyed by hash.</param>
	/// <param name="name">The human-readable name of the setting field.</param>
	/// <returns>The associated <see cref="int"/> value.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is not found or is not an integer.
	/// </exception>
	public static int GetIntSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not int)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(int)}'.");

		return result.ValueAs<int>();
	}
	public static bool TryGetIntSetting(Dictionary<uint, MapSetting> settings, string name, out int setting)
	{
		try
		{
			setting = GetIntSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a floating-point setting by name.
	/// </summary>
	/// <param name="settings">The source dictionary of parsed field values.</param>
	/// <param name="name">The display name of the setting.</param>
	/// <returns>The associated <see cref="float"/> value.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is not found or cannot be cast to a float.
	/// </exception>
	public static float GetFloatSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not float)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(float)}'.");

		return result.ValueAs<float>();
	}
	public static bool TryGetFloatSetting(Dictionary<uint, MapSetting> settings, string name, out float setting)
	{
		try
		{
			setting = GetFloatSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a 2D vector (point) setting by name.
	/// </summary>
	/// <param name="settings">Field metadata dictionary keyed by hashed names.</param>
	/// <param name="name">The user-facing name of the desired setting.</param>
	/// <returns>The <see cref="Vect2"/> value if found and correctly typed.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is not found or is not a <see cref="Vect2"/>.
	/// </exception>
	public static Vect2 GetPointSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not Vect2)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(Vect2)}'.");

		return result.ValueAs<Vect2>();
	}
	public static bool TryGetPointSetting(Dictionary<uint, MapSetting> settings, string name, out Vect2 setting)
	{
		try
		{
			setting = GetPointSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a color setting by name, typically used for tints or markers.
	/// </summary>
	/// <param name="settings">The field collection parsed from entity or level metadata.</param>
	/// <param name="name">The setting’s name in source data.</param>
	/// <returns>The corresponding <see cref="Color"/> value.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is missing or its value is not a color.
	/// </exception>
	public static Color GetColorSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not Color)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(Color)}'.");

		return result.ValueAs<Color>();
	}
	public static bool TryGetColorSetting(Dictionary<uint, MapSetting> settings, string name, out Color setting)
	{
		try
		{
			setting = GetColorSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a string setting by name, often used for labels, scripts, or tags.
	/// </summary>
	/// <param name="settings">The parsed setting dictionary keyed by hashed names.</param>
	/// <param name="name">The name of the setting field to retrieve.</param>
	/// <returns>The <see cref="string"/> content of the setting.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting cannot be found or is not a string.
	/// </exception>
	public static string GetStringSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not string)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(string)}'.");

		return result.ValueAs<string>();
	}
	public static bool TryGetStringSetting(Dictionary<uint, MapSetting> settings, string name, out string setting)
	{
		try
		{
			setting = GetStringSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a file path string from map metadata, typically used for referencing external assets.
	/// </summary>
	/// <param name="settings">The dictionary of settings stored by hashed name.</param>
	/// <param name="name">The string name of the file path field.</param>
	/// <returns>The setting value as a <see cref="string"/> file path.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting doesn't exist or is not a string.
	/// </exception>
	public static string GetFilePathSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not string)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(string)}'.");

		return result.ValueAs<string>();
	}
	public static bool TryGetFilePathSetting(Dictionary<uint, MapSetting> settings, string name, out string setting)
	{
		try
		{
			setting = GetFilePathSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a tile setting that refers to a graphic frame or marker tile.
	/// </summary>
	/// <param name="settings">The dictionary of metadata settings parsed from source.</param>
	/// <param name="name">The key name of the tile field.</param>
	/// <returns>The associated <see cref="MapTile"/> instance.</returns>
	/// <exception cref="Exception">
	/// Thrown if the tile value is missing or incompatible.
	/// </exception>
	public static MapTile GetTileSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not MapTile)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(MapTile)}'.");

		return result.ValueAs<MapTile>();
	}
	public static bool TryGetTileSetting(Dictionary<uint, MapSetting> settings, string name, out MapTile setting)
	{
		try
		{
			setting = GetTileSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a reference to another entity from metadata settings.
	/// Useful for linking objects across layers or levels.
	/// </summary>
	/// <param name="settings">Settings dictionary keyed by hashed field names.</param>
	/// <param name="name">The name of the reference field.</param>
	/// <returns>The corresponding <see cref="MapEntityRef"/> value.</returns>
	/// <exception cref="Exception">
	/// Thrown if no entity reference is found or the type is incorrect.
	/// </exception>
	public static MapEntityRef GetEntityRefSetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not MapEntityRef)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(MapEntityRef)}'.");

		return result.ValueAs<MapEntityRef>();
	}
	public static bool TryGetEntityRefSetting(Dictionary<uint, MapSetting> settings, string name, out MapEntityRef setting)
	{
		try
		{
			setting = GetEntityRefSetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves and parses an enum value from a string-based setting field.
	/// </summary>
	/// <typeparam name="TEnum">The enum type to convert to.</typeparam>
	/// <param name="settings">The map settings containing the enum field.</param>
	/// <param name="name">The original name of the enum field.</param>
	/// <returns>The enum value parsed from the string setting.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting doesn't exist or is not a string compatible with the target enum.
	/// </exception>
	public static TEnum GetEnumSetting<TEnum>(Dictionary<uint, MapSetting> settings, string name) where TEnum : Enum
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not string)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(TEnum)}'.");

		return (TEnum)Enum.Parse(typeof(TEnum), result.ValueAs<string>(), true);
	}
	public static bool TryGetEnumSetting<TEnum>(Dictionary<uint, MapSetting> settings, string name, out TEnum setting) where TEnum : Enum
	{
		try
		{
			setting = GetEnumSetting<TEnum>(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}



	/// <summary>
	/// Retrieves a list of boolean values from a field setting by name.
	/// </summary>
	/// <param name="settings">The dictionary containing hashed map settings.</param>
	/// <param name="name">The name of the field to look up.</param>
	/// <returns>A read-only list of <see cref="bool"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown when:
	/// <list type="bullet">
	///   <item><description>The setting is not found by the given name.</description></item>
	///   <item><description>The value is not a <see cref="List{T}"/> of <see cref="bool"/>.</description></item>
	/// </list>
	/// </exception>
	public static IReadOnlyList<bool> GetBoolArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<bool>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<bool>)}'.");

		return result.ValueAs<List<bool>>();
	}
	public static bool TryGetBoolArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<bool> setting)
	{
		try
		{
			setting = GetBoolArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}



	/// <summary>
	/// Retrieves a list of integer values from a setting field.
	/// </summary>
	/// <param name="settings">The collection of hashed map setting entries.</param>
	/// <param name="name">The readable name of the field.</param>
	/// <returns>A read-only list of <see cref="int"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown if the field is missing or not a <see cref="List{Int32}"/>.
	/// </exception>
	public static IReadOnlyList<int> GetIntArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<int>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<int>)}'.");

		return result.ValueAs<List<int>>();
	}
	public static bool TryGetIntArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<int> setting)
	{
		try
		{
			setting = GetIntArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of floating-point values from a map field.
	/// </summary>
	/// <param name="settings">The parsed setting dictionary from level or entity metadata.</param>
	/// <param name="name">The string name of the field.</param>
	/// <returns>A read-only list of <see cref="float"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is not found or has an incompatible type.
	/// </exception>
	public static IReadOnlyList<float> GetFloatArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<float>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<float>)}'.");

		return result.ValueAs<List<float>>();
	}
	public static bool TryGetFloatArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<float> setting)
	{
		try
		{
			setting = GetFloatArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of 2D points from a vector-type field.
	/// </summary>
	/// <param name="settings">The field data dictionary containing hashed keys.</param>
	/// <param name="name">The source name of the point array field.</param>
	/// <returns>A read-only list of <see cref="Vect2"/> positions.</returns>
	/// <exception cref="Exception">
	/// Thrown if the field is not present or is not a list of <see cref="Vect2"/>.
	/// </exception>
	public static IReadOnlyList<Vect2> GetPointArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<Vect2>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<Vect2>)}'.");

		return result.ValueAs<List<Vect2>>();
	}
	public static bool TryGetPointArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<Vect2> setting)
	{
		try
		{
			setting = GetPointArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of colors from a setting field.
	/// </summary>
	/// <param name="settings">Dictionary of parsed metadata fields.</param>
	/// <param name="name">The name of the color field being accessed.</param>
	/// <returns>A read-only list of <see cref="Color"/> entries.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is not found or contains invalid types.
	/// </exception>
	public static IReadOnlyList<Color> GetColorArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<Color>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<Color>)}'.");

		return result.ValueAs<List<Color>>();
	}
	public static bool TryGetColorArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<Color> setting)
	{
		try
		{
			setting = GetColorArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of string values from a field entry.
	/// </summary>
	/// <param name="settings">The dictionary of hashed field names and values.</param>
	/// <param name="name">The human-friendly field identifier.</param>
	/// <returns>A read-only list of <see cref="string"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown if the setting is absent or not a <see cref="List{String}"/>.
	/// </exception>
	public static IReadOnlyList<string> GetStringArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<string>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<string>)}'.");

		return result.ValueAs<List<string>>();
	}
	public static bool TryGetStringArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<string> setting)
	{
		try
		{
			setting = GetStringArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of file path strings from the setting metadata.
	/// </summary>
	/// <param name="settings">The dictionary of settings parsed from fields.</param>
	/// <param name="name">The name of the file path array field.</param>
	/// <returns>A read-only list of file paths as <see cref="string"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown when the field is missing or not a list of <see cref="string"/>.
	/// </exception>
	public static IReadOnlyList<string> GetFilePathArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<string>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<string>)}'.");

		return result.ValueAs<List<string>>();
	}
	public static bool TryGetFilePathArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<string> setting)
	{
		try
		{
			setting = GetFilePathArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of tile references from the specified setting field.
	/// </summary>
	/// <param name="settings">The collection of parsed setting data.</param>
	/// <param name="name">The name of the tile array field.</param>
	/// <returns>A read-only list of <see cref="MapTile"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown if the field is not found or is not a list of <see cref="MapTile"/>.
	/// </exception>
	public static IReadOnlyList<MapTile> GetTileArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<MapTile>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<MapTile>)}'.");

		return result.ValueAs<List<MapTile>>();
	}
	public static bool TryGetTileArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<MapTile> setting)
	{
		try
		{
			setting = GetTileArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of entity references from a relational field.
	/// </summary>
	/// <param name="settings">The dictionary of field metadata.</param>
	/// <param name="name">The name of the field containing entity links.</param>
	/// <returns>A read-only list of <see cref="MapEntityRef"/> values.</returns>
	/// <exception cref="Exception">
	/// Thrown if the field is not valid or not a list of <see cref="MapEntityRef"/>.
	/// </exception>
	public static IReadOnlyList<MapEntityRef> GetEntityRefArraySetting(Dictionary<uint, MapSetting> settings, string name)
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<MapEntityRef>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<MapEntityRef>)}'.");

		return result.ValueAs<List<MapEntityRef>>();
	}
	public static bool TryGetEntityRefArraySetting(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<MapEntityRef> setting)
	{
		try
		{
			setting = GetEntityRefArraySetting(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}

	/// <summary>
	/// Retrieves a list of enum values from a setting stored as strings.
	/// </summary>
	/// <typeparam name="TEnum">The enum type to convert each entry into.</typeparam>
	/// <param name="settings">The dictionary of field setting metadata.</param>
	/// <param name="name">The name of the enum list field.</param>
	/// <returns>A read-only list of <typeparamref name="TEnum"/> values parsed from string entries.</returns>
	/// <exception cref="Exception">
	/// Thrown if the field is missing or does not contain a list of strings.
	/// </exception>
	public static IReadOnlyList<TEnum> GetEnumArraySetting<TEnum>(Dictionary<uint, MapSetting> settings, string name) where TEnum : Enum
	{
		if (!settings.TryGetValue(HashHelpers.Cache32(name), out var result))
			throw new Exception($"Unable to find setting with the name '{name}'.");
		if (result.Value is not List<string>)
			throw new Exception($"This setting '{name}' isn't '{result.Value.GetType()}', it is  '{typeof(List<TEnum>)}'.");

		var items = result.ValueAs<List<string>>();
		var enumResult = new List<TEnum>(items.Count);

		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			if (!Enum.TryParse(typeof(TEnum), item, true, out var @enum))
				continue;

			enumResult.Add((TEnum)@enum);
		}

		return enumResult;
	}
	public static bool TryGetEnumArraySetting<TEnum>(Dictionary<uint, MapSetting> settings, string name, out IReadOnlyList<TEnum> setting) where TEnum : Enum
	{
		try
		{
			setting = GetEnumArraySetting<TEnum>(settings, name);
			return true;
		}
		catch
		{
			setting = default;
			return false;
		}
	}
}
