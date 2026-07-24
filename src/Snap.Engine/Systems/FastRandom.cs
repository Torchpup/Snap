namespace Snap.Engine.Systems;

/// <summary>
/// A high-performance, seeded random number generator using the Xoroshiro128+ algorithm.
/// Supports generating boolean, integer, floating-point, and long values with optional ranges.
/// </summary>
/// <remarks>
/// This class is thread-safe for single-threaded use. For multi-threaded scenarios,
/// create separate instances with different seeds.
/// </remarks>
public sealed class FastRandom
{
	private ulong _state0, _state1;

	/// <summary>
	/// Gets the singleton instance of the <see cref="FastRandom"/> class.
	/// </summary>
	public static FastRandom Instance { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="FastRandom"/> class with a seed based on the current UTC time.
	/// </summary>
	public FastRandom()
	{
		Instance ??= this;

		ulong seed = (ulong)DateTime.Now.Ticks;
		SetSeed(seed);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FastRandom"/> class with the specified seed.
	/// </summary>
	/// <param name="seed">The seed value for the random number generator.</param>
	public FastRandom(ulong seed)
	{
		Instance ??= this;

		SetSeed(seed);
	}

	/// <summary>
	/// Sets the seed for the random number generator.
	/// </summary>
	/// <param name="seed">The seed value.</param>
	public void SetSeed(ulong seed)
	{
		ulong sm = seed;

		_state0 = SplitMix64(ref sm);
		_state1 = SplitMix64(ref sm);
	}

	/// <summary>
	/// Generates a random boolean value.
	/// </summary>
	/// <returns><see langword="true"/> or <see langword="false"/> with equal probability.</returns>
	public bool NextBool() => (NextUlong() & 1UL) == 1UL;

	#region NextInt
	/// <summary>
	/// Generates a random 32-bit signed integer.
	/// </summary>
	/// <returns>A random integer.</returns>
	public int NextInt() => (int)(NextUlong() >> 33);

	/// <summary>
	/// Generates a random integer in the range [0, <paramref name="max"/>).
	/// </summary>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random integer in the range [0, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="max"/> is less than or equal to zero.
	/// </exception>
	public int NextInt(int max)
	{
		if (max <= 0)
			throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");
		return (int)NextInRange(0, (ulong)max);
	}

	/// <summary>
	/// Generates a random integer in the range [<paramref name="min"/>, <paramref name="max"/>).
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random integer in the range [<paramref name="min"/>, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than or equal to <paramref name="max"/>.
	/// </exception>
	public int NextInt(int min, int max)
	{
		if (min >= max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than max.");

		ulong range = (ulong)max - (ulong)min;
		ulong r = NextInRange(0UL, range);
		return (int)(min + (long)r);
	}
	#endregion


	#region NextFloat
	/// <summary>
	/// Generates a random floating-point number in the range [0.0f, 1.0f).
	/// </summary>
	/// <returns>A random float in the range [0.0f, 1.0f).</returns>
	public float NextFloat()
	{
		// Generates 24-bit mantissa for float:
		uint bits = (uint)(NextUlong() >> 40); // 64-24=40
		return bits / (float)(1u << 24);
	}

	/// <summary>
	/// Generates a random floating-point number in the range [0.0f, <paramref name="max"/>).
	/// </summary>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random float in the range [0.0f, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="max"/> is less than or equal to zero.
	/// </exception>
	public float NextFloat(float max)
	{
		if (max <= 0f)
			throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");
		return NextFloat() * max;
	}

	/// <summary>
	/// Generates a random floating-point number in the range [<paramref name="min"/>, <paramref name="max"/>).
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random float in the range [<paramref name="min"/>, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than or equal to <paramref name="max"/>.
	/// </exception>
	public float NextFloat(float min, float max)
	{
		if (min >= max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than max.");
		return min + NextFloat() * (max - min);
	}
	#endregion


	#region NextDouble
	/// <summary>
	/// Generates a random double-precision floating-point number in the range [0.0, 1.0).
	/// </summary>
	/// <returns>A random double in the range [0.0, 1.0).</returns>
	public double NextDouble()
	{
		// Generate 53-bit mantissa for double:
		ulong bits = NextUlong() >> 11; // 64-53 = 11
		return bits * (1.0 / (1UL << 53));
	}

	/// <summary>
	/// Generates a random double-precision floating-point number in the range [0.0, <paramref name="max"/>).
	/// </summary>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random double in the range [0.0, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="max"/> is less than or equal to zero.
	/// </exception>
	public double NextDouble(double max)
	{
		if (max <= 0.0)
			throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");
		return NextDouble() * max;
	}

	/// <summary>
	/// Generates a random double-precision floating-point number in the range [<paramref name="min"/>, <paramref name="max"/>).
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random double in the range [<paramref name="min"/>, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than or equal to <paramref name="max"/>.
	/// </exception>
	public double NextDouble(double min, double max)
	{
		if (min >= max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than max.");
		return min + NextDouble() * (max - min);
	}
	#endregion


	#region NextLong
	/// <summary>
	/// Generates a random 64-bit signed integer.
	/// </summary>
	/// <returns>A random long value.</returns>
	public long NextLong()
	{
		// use upper 63 bits to ensure non-nagitive:
		return (long)(NextUlong() >> 1);
	}

	/// <summary>
	/// Generates a random long in the range [0, <paramref name="max"/>).
	/// </summary>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random long in the range [0, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="max"/> is less than or equal to zero.
	/// </exception>
	public long NextLong(long max)
	{
		if (max <= 0L)
			throw new ArgumentOutOfRangeException(nameof(max), "max must be positive.");
		return (long)NextInRange(0UL, (ulong)max);
	}

	/// <summary>
	/// Generates a random long in the range [<paramref name="min"/>, <paramref name="max"/>).
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The exclusive upper bound of the random number.</param>
	/// <returns>A random long in the range [<paramref name="min"/>, <paramref name="max"/>).</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than or equal to <paramref name="max"/>.
	/// </exception>
	public long NextLong(long min, long max)
	{
		if (min >= max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than max.");

		ulong range = (ulong)max - (ulong)min;
		ulong r = NextInRange(0UL, range);
		return min + (long)r;
	}
	#endregion


	#region Range
	/// <summary>
	/// Returns a random integer in the range [<paramref name="min"/>, <paramref name="max"/>] inclusive.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The inclusive upper bound of the random number.</param>
	/// <returns>A random integer in the range [<paramref name="min"/>, <paramref name="max"/>].</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public int RangeInt(int min, int max)
	{
		if (min > max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
		// Compute range size as (max - min + 1)
		ulong range = (ulong)max - (ulong)min + 1UL;
		ulong r = NextInRange(0UL, range);
		return (int)(min + (long)r);
	}

	/// <summary>
	/// Returns a random float in the range [<paramref name="min"/>, <paramref name="max"/>] inclusive.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The inclusive upper bound of the random number.</param>
	/// <returns>A random float in the range [<paramref name="min"/>, <paramref name="max"/>].</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public float RangeFloat(float min, float max)
	{
		if (min > max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
		return min + NextFloat() * (max - min);
	}

	/// <summary>
	/// Returns a random double in the range [<paramref name="min"/>, <paramref name="max"/>] inclusive.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The inclusive upper bound of the random number.</param>
	/// <returns>A random double in the range [<paramref name="min"/>, <paramref name="max"/>].</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public double RangeDouble(double min, double max)
	{
		if (min > max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
		return min + NextDouble() * (max - min);
	}

	/// <summary>
	/// Returns a random long in the range [<paramref name="min"/>, <paramref name="max"/>] inclusive.
	/// </summary>
	/// <param name="min">The inclusive lower bound of the random number.</param>
	/// <param name="max">The inclusive upper bound of the random number.</param>
	/// <returns>A random long in the range [<paramref name="min"/>, <paramref name="max"/>].</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.
	/// </exception>
	public long RangeLong(long min, long max)
	{
		if (min > max)
			throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max.");
		// Compute range size as (max - min + 1)
		ulong range = (ulong)max - (ulong)min + 1UL;
		ulong r = NextInRange(0UL, range);
		return min + (long)r;
	}
	#endregion


	#region Private Methods
	private ulong SplitMix64(ref ulong x)
	{
		x += 0x9E3779B97F4A7C15UL;
		ulong z = x;
		z = (z ^ z >> 30) * 0xBF58476D1CE4E5B9UL;
		z = (z ^ z >> 27) * 0x94D049BB133111EBUL;
		return z ^ z >> 31;
	}

	private ulong NextInRange(ulong origin, ulong bound)
	{
		ulong range = bound - origin;
		if (range == 0UL)
			return origin;

		// calc the rejection threshold: highest multiple of ranges <= ULong.MaxValue
		ulong threshold = unchecked(ulong.MaxValue / range * range);
		ulong r;

		do
		{
			r = NextUlong();
		} while (r >= threshold);

		return (r % range) + origin;
	}

	private ulong NextUlong()
	{
		ulong s0 = _state0;
		ulong s1 = _state1;
		ulong result = s0 + s1;

		s1 ^= s0;
		_state0 = RotateLeft(s0, 55) ^ s1 ^ s1 << 14;
		_state1 = RotateLeft(s1, 36);

		return result;
	}

	private ulong RotateLeft(ulong x, int k) => x << k | x >> 64 - k;
	#endregion
}
