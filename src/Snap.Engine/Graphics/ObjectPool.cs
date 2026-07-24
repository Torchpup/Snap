namespace Snap.Engine.Graphics;

/// <summary>
/// Provides a generic object pooling mechanism for efficiently managing reusable instances.
/// This class reduces garbage collection pressure and improves performance by recycling objects
/// rather than creating and destroying them repeatedly.
/// </summary>
/// <typeparam name="T">The type of object to pool. Must be a reference type (class).</typeparam>
/// <remarks>
/// <para>
/// This pool maintains a collection of available objects that can be rented and returned.
/// When an object is requested and the pool is empty, new instances are created using
/// a factory function. Returned objects are reset to a clean state for future use.
/// </para>
/// <para>
/// This implementation is suitable for objects that are expensive to create or destroy,
/// such as audio instances, particle systems, or network connections. The pool helps
/// maintain consistent performance in scenarios with frequent object allocation.
/// </para>
/// </remarks>
public class ObjectPool<T> where T : class
{
	private readonly Stack<T> _pool = [];
	private readonly Func<T> _createFunc;
	private readonly Action<T> _resetAction;

	/// <summary>
	/// Initializes a new instance of the object pool with the specified creation and reset behavior.
	/// </summary>
	/// <param name="createFunc">A function that creates new instances when the pool is empty. Cannot be null.</param>
	/// <param name="resetAction">An optional action to reset objects to a clean state before returning them to the pool.</param>
	/// <remarks>
	/// The <paramref name="createFunc"/> is used when a requested object cannot be satisfied
	/// from the available pool. The <paramref name="resetAction"/> is applied to objects
	/// before they're made available for reuse, ensuring they're in a consistent state.
	/// </remarks>
	public ObjectPool(Func<T> createFunc, Action<T> resetAction = null)
	{
		_createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
		_resetAction = resetAction;
	}

	/// <summary>
	/// Retrieves an object instance from the pool. Creates a new instance if the pool is empty.
	/// </summary>
	/// <returns>An available object instance, ready for use.</returns>
	/// <remarks>
	/// This method is thread-safe and provides efficient object reuse. When the internal
	/// pool contains available objects, the most recently returned one is provided
	/// (LIFO behavior), which can improve cache locality.
	/// </remarks>
	public T Rent()
	{
		lock (_pool)
		{
			return _pool.Count > 0 ? _pool.Pop() : _createFunc();
		}
	}

	/// <summary>
	/// Returns an object to the pool for future reuse.
	/// </summary>
	/// <param name="item">The object to return to the pool. Null values are silently ignored.</param>
	/// <remarks>
	/// If a reset action was provided during pool construction, it is applied to the object
	/// before it's returned to the available pool. This ensures the object is in a clean
	/// state for its next use. The method is thread-safe and handles null parameters gracefully.
	/// </remarks>
	public void Return(T item)
	{
		if (item == null) return;

		_resetAction?.Invoke(item);

		lock (_pool)
		{
			_pool.Push(item);
		}
	}
}
