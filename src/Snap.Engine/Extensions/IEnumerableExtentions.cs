namespace System;

/// <summary>
/// Provides extension methods for <see cref="IEnumerable{T}"/> to simplify common sequence operations.
/// </summary>
public static class IEnumerableExtensions
{
	/// <summary>
	/// Determines whether the sequence is <c>null</c> or contains no elements.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="v">The sequence to test.</param>
	/// <returns><c>true</c> if <paramref name="v"/> is <c>null</c> or empty; otherwise, <c>false</c>.</returns>
	public static bool IsEmpty<T>(this IEnumerable<T> v) => v == null || !v.Any();

	/// <summary>
	/// Determines whether the sequence contains one or more elements.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="v">The sequence to test.</param>
	/// <returns><c>true</c> if <paramref name="v"/> is not <c>null</c> and contains at least one element; otherwise, <c>false</c>.</returns>
	public static bool IsNotEmpty<T>(this IEnumerable<T> v) => !IsEmpty(v);

	/// <summary>
	/// Performs the specified <paramref name="action"/> on each element of the sequence.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="source">The sequence whose elements to iterate.</param>
	/// <param name="action">The action to perform on each element.</param>
	public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
	{
		foreach (var item in source)
			action(item);
	}

	/// <summary>
	/// Safely retrieves the element at the given <paramref name="index"/>, or returns default if out of range.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="source">The sequence to index.</param>
	/// <param name="index">The zero-based element index.</param>
	/// <returns>
	/// The element at position <paramref name="index"/> if within bounds; otherwise, <c>default</c> of <typeparamref name="T"/>.
	/// </returns>
	public static T SafeElementAt<T>(this IEnumerable<T> source, int index) =>
		index >= 0 && index < source.Count() ? source.ElementAt(index) : default;

	/// <summary>
	/// Returns the index of the first occurrence of the specified <paramref name="item"/> in the sequence,
	/// or the default index (0) if not found.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="source">The sequence to search.</param>
	/// <param name="item">The item to locate.</param>
	/// <returns>
	/// The zero-based index of the first occurrence of <paramref name="item"/>,
	/// or 0 if the item is not found.
	/// </returns>
	public static int IndexOf<T>(this IEnumerable<T> source, T item) =>
		source.Select((value, index) => (value, index))
			.FirstOrDefault(x => EqualityComparer<T>.Default.Equals(x.value, item)).index;

	/// <summary>
	/// Splits the sequence into two subsequences based on the given <paramref name="predicate"/>.
	/// </summary>
	/// <typeparam name="T">The element type of the sequence.</typeparam>
	/// <param name="source">The sequence to partition.</param>
	/// <param name="predicate">A function to test each element for a condition.</param>
	/// <returns>
	/// A tuple containing:
	/// <list type="bullet">
	///   <item><description><c>matches</c>: elements satisfying <paramref name="predicate"/>.</description></item>
	///   <item><description><c>nonMatches</c>: elements not satisfying <paramref name="predicate"/>.</description></item>
	/// </list>
	/// </returns>
	public static (IEnumerable<T> matches, IEnumerable<T> nonMatches) Partition<T>(this IEnumerable<T> source, Func<T, bool> predicate)
	{
		IEnumerable<T> matches = source.Where(predicate);
		IEnumerable<T> nonMatches = source.Where(x => !predicate(x));

		return (matches, nonMatches);
	}
}
