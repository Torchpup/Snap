namespace System;

/// <summary>
/// Provides extension methods for <see cref="FastRandom"/> to simplify random selection,
/// shuffling, color generation, and geometric randomness.
/// </summary>
public static class RandExtentions
{
	/// <summary>
	/// Returns a randomly selected item from the specified array.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="rng">The random generator instance.</param>
	/// <param name="items">An array of items to choose from.</param>
	/// <returns>A randomly selected item.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="rng"/> is null or if <paramref name="items"/> is empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="items"/> is null.</exception>
	public static T Choice<T>(this FastRandom rng, params Span<T> items)
	{
		if (rng == null) throw new ArgumentNullException(nameof(rng));
		if (items.Length == 0) throw new ArgumentException("Must provide at least one item.", nameof(items));

		int idx = rng.NextInt(items.Length);
		return items[idx];
	}

	/// <summary>
	/// Returns a randomly selected item from the specified list.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="rng">The random generator instance.</param>
	/// <param name="list">A read-only list of items to choose from.</param>
	/// <returns>A randomly selected item.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="list"/> is empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="list"/> is null.</exception>
	public static T Choice<T>(this FastRandom rng, IReadOnlyList<T> list)
	{
		if (rng == null) throw new ArgumentException(nameof(rng));
		if (list == null) throw new ArgumentNullException(nameof(list));
		if (list.Count == 0)
			throw new ArgumentException("Must provide at least one item.", nameof(list));

		int idx = rng.NextInt(list.Count);
		return list[idx];
	}

	/// <summary>
	/// Returns a randomly selected item from an enumerable source.
	/// Uses reservoir sampling for efficient selection.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="rng">The random generator instance.</param>
	/// <param name="source">The sequence to choose from.</param>
	/// <returns>A randomly selected item.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="source"/> is empty.</exception>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
	public static T Choice<T>(this FastRandom rng, IEnumerable<T> source)
	{
		if (rng == null) throw new ArgumentException(nameof(rng));
		if (source == null) throw new ArgumentNullException(nameof(source));

		if (source is List<T> list)
		{
			if (list.Count == 0)
				throw new ArgumentException("Sequence contains no elements.", nameof(source));
			int index = rng.NextInt(list.Count);
			return list[index];
		}

		T selected = default;
		int count = 0;
		foreach (var item in source)
		{
			count++;
			if (rng.NextInt(count) == 0)
				selected = item;
		}

		if (count == 0)
			throw new ArgumentException("Sequence contains no elements", nameof(source));
		return selected;
	}

	/// <summary>
	/// Returns a random cardinal direction (up, down, left, or right).
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>A randomly selected <see cref="Vect2"/> direction vector.</returns>
	public static Vect2 RandomDirection(this FastRandom rng)
	{
		var dirs = new Vect2[]
		{
			Vect2.Up,
			Vect2.Right,
			Vect2.Down,
			Vect2.Left,
		};

		return Choice(rng, dirs);
	}

	/// <summary>
	/// Returns a randomly generated color.
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>A <see cref="Color"/> with random RGB values.</returns>
	public static Color RandomColor(this FastRandom rng) =>
		 new Color(rng.NextInt(0, 255), rng.NextInt(0, 255), rng.NextInt(0, 255));

	/// <summary>
	/// Returns a randomly selected value from an enum type.
	/// </summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>A randomly selected enum value.</returns>
	public static TEnum RandomEnum<TEnum>(this FastRandom rng) where TEnum : struct, Enum
	{
		// var vals = Enum.GetValues<TEnum>();
		// return vals[rng.NextInt(0, vals.Length)];
		var type = typeof(TEnum);
		if (!_enumCache.TryGetValue(type, out var values))
		{
			values = Enum.GetValues(type);
			_enumCache[type] = values;
		}

		return (TEnum)values.GetValue(rng.NextInt(values.Length));
	}
	private static readonly Dictionary<Type, Array> _enumCache = [];

	/// <summary>
	/// Returns either 1 or -1 randomly.
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>1 or -1 with equal probability.</returns>
	public static int NextSign(this FastRandom rng) => rng.NextBool() ? 1 : -1;

	/// <summary>
	/// Randomly shuffles the elements in the given list in-place.
	/// </summary>
	/// <typeparam name="T">The type of the items.</typeparam>
	/// <param name="rng">The random generator instance.</param>
	/// <param name="list">The list to shuffle.</param>
	public static void Shuffle<T>(this FastRandom rng, IList<T> list)
	{
		for (int i = list.Count - 1; i >= 0; i--)
		{
			int j = rng.RangeInt(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	/// <summary>
	/// Simulates rolling a number of dice and returns the total.
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <param name="diceCount">The number of dice to roll.</param>
	/// <param name="sides">The number of sides per die.</param>
	/// <returns>The sum of all dice rolls.</returns>
	public static int RollDice(this FastRandom rng, int diceCount, int sides)
	{
		int sum = 0;
		for (int i = diceCount - 1; i >= 0; i--)
			sum += rng.RangeInt(1, sides);
		return sum;
	}

	/// <summary>
	/// Returns a random 2D point inside a unit circle.
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>A random <see cref="Vect2"/> inside a unit circle.</returns>
	public static Vect2 RandomPointInCircle(this FastRandom rng)
	{
		float angle = rng.NextFloat(0f, MathF.PI * 2f);
		float radius = MathF.Sqrt(rng.NextFloat());
		return new Vect2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
	}

	/// <summary>
	/// Returns a randomly generated pastel color.
	/// </summary>
	/// <param name="rng">The random generator instance.</param>
	/// <returns>A <see cref="Color"/> in pastel range (light tones).</returns>
	public static Color RandomPastelColor(this FastRandom rng)
	{
		// bais towards lighter values:
		byte r = (byte)rng.RangeInt(128, 255);
		byte g = (byte)rng.RangeInt(128, 255);
		byte b = (byte)rng.RangeInt(128, 255);

		return new Color(r, g, b);
	}
}
