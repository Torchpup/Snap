namespace Snap.Engine.Helpers;

/// <summary>
/// Provides FNV‑1a hash functions for 32‑bit and 64‑bit hashes over byte arrays or UTF‑8 strings.
/// </summary>
public static class HashHelpers
{
	private static readonly Dictionary<string, uint> s_cache32 = [];
	private static readonly Dictionary<string, ulong> s_cache64 = [];

	/// <summary>
	/// Gets a cached 32-bit hash for the given name, computing and storing it if needed.
	/// </summary>
	/// <param name="name">The string to hash and cache.</param>
	/// <returns>
	/// The 32-bit hash value associated with <paramref name="name"/>.
	/// If the value was previously cached, the cached value is returned.
	/// </returns>
	public static uint Cache32(string name)
	{
		if (s_cache32.TryGetValue(name, out var id))
			return id;

		uint hash = Hash32(name);

		s_cache32[name] = hash;
		return hash;
	}

	/// <summary>
	/// Gets a cached 64-bit hash for the given name, computing and storing it if needed.
	/// </summary>
	/// <param name="name">The string to hash and cache.</param>
	/// <returns>
	/// The 64-bit hash value associated with <paramref name="name"/>.
	/// If the value was previously cached, the cached value is returned.
	/// </returns>
	public static ulong Cache64(string name)
	{
		if (s_cache64.TryGetValue(name, out var id))
			return id;

		ulong hash = Hash64(name);

		s_cache64[name] = hash;
		return hash;
	}

	/// <summary>
	/// Computes the 32‑bit FNV‑1a hash of the given byte span.
	/// </summary>
	/// <param name="data">The input data to hash.</param>
	/// <returns>The 32‑bit FNV‑1a hash value.</returns>
	public static uint Hash32(ReadOnlySpan<byte> data)
	{
		const uint offsetBasis = 2166136261u;
		const uint prime = 16777619u;

		uint hash = offsetBasis;
		for (int i = 0; i < data.Length; i++)
		{
			hash ^= data[i];
			hash *= prime;
		}
		return hash;
	}

	/// <summary>
	/// Computes the 64‑bit FNV‑1a hash of the given byte span.
	/// </summary>
	/// <param name="data">The input data to hash.</param>
	/// <returns>The 64‑bit FNV‑1a hash value.</returns>
	public static ulong Hash64(ReadOnlySpan<byte> data)
	{
		const ulong offsetBasis = 1469598103934665603ul;
		const ulong prime = 1099511628211ul;

		ulong hash = offsetBasis;
		for (int i = 0; i < data.Length; i++)
		{
			hash ^= data[i];
			hash *= prime;
		}
		return hash;
	}

	/// <summary>
	/// Computes the 32‑bit FNV‑1a hash of the given string, using UTF‑8 encoding.
	/// </summary>
	/// <param name="text">The input string to hash.</param>
	/// <returns>The 32‑bit FNV‑1a hash value of the UTF‑8 bytes of <paramref name="text"/>.</returns>
	public static uint Hash32(string text) => Hash32(Encoding.UTF8.GetBytes(text));

	/// <summary>
	/// Computes the 32‑bit FNV‑1a hash of the given enum value, using its string representation.
	/// </summary>
	/// <param name="text">The enum value to hash. It will be converted to a string using <c>ToEnumString()</c>, then UTF‑8 encoded.</param>
	/// <returns>The 32‑bit FNV‑1a hash value of the enum's name.</returns>
	public static uint Hash32(Enum text) => Hash32(Encoding.UTF8.GetBytes(text.ToEnumString()));

	/// <summary>
	/// Gets a cached 32-bit hash for the given enum value.
	/// </summary>
	/// <param name="text">The enum value to hash and cache.</param>
	/// <returns>
	/// The 32-bit hash value associated with <paramref name="text"/>.
	/// The enum is converted to its string representation before hashing.
	/// </returns>
	public static uint Cache32(Enum text) => Cache32(text.ToEnumString());

	/// <summary>
	/// Computes the 64‑bit FNV‑1a hash of the given string, using UTF‑8 encoding.
	/// </summary>
	/// <param name="text">The input string to hash.</param>
	/// <returns>The 64‑bit FNV‑1a hash value of the UTF‑8 bytes of <paramref name="text"/>.</returns>
	public static ulong Hash64(string text) => Hash64(Encoding.UTF8.GetBytes(text));

	/// <summary>
	/// Computes the 64‑bit FNV‑1a hash of the given enum value, using its string representation.
	/// </summary>
	/// <param name="text">The enum value to hash. It will be converted to a string using <c>ToEnumString()</c>, then UTF‑8 encoded.</param>
	/// <returns>The 64‑bit FNV‑1a hash value of the enum's name.</returns>
	public static ulong Hash64(Enum text) => Hash64(Encoding.UTF8.GetBytes(text.ToEnumString()));

	/// <summary>
	/// Gets a cached 64-bit hash for the given enum value.
	/// </summary>
	/// <param name="text">The enum value to hash and cache.</param>
	/// <returns>
	/// The 64-bit hash value associated with <paramref name="text"/>.
	/// The enum is converted to its string representation before hashing.
	/// </returns>
	public static ulong Cache64(Enum text) => Cache64(text.ToEnumString());

}
