using System.Threading.Tasks.Dataflow;

namespace Snap.Engine.Helpers;

/// <summary>
/// Provides reflection-based utility methods for working with types marked with the <see cref="DiscoverableAttribute"/>.
/// This static helper is used by the Snap engine to locate and process classes tagged as <c>[Discoverable]</c>
/// across loaded assemblies, including engine modules, scripts, and mod/plugin DLLs.
/// <para/>
/// Typical usage includes:
/// <list type="bullet">
///   <item><description>Scanning assemblies for discoverable types</description></item>
///   <item><description>Registering mod/plugin-defined systems or tools</description></item>
///   <item><description>Populating debug panels or editor listings dynamically</description></item>
/// </list>
/// <para/>
/// This class is engine-internal and not intended for direct use by external mods (unless exposed explicitly).
/// </summary>
public static class DiscoverableHelper
{
	private static readonly WeakReference<List<Type>> _allTypesRef = new(null);
	private static readonly object _allTypesLock = new();
	private static readonly ConcurrentDictionary<Type, DiscoverableAttribute> _metaCache = [];
	private static readonly ConcurrentDictionary<Type, IReadOnlyList<Type>> _findAllCache = [];

	static DiscoverableHelper()
	{
		AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
		{
			_allTypesRef.SetTarget(null);
			_metaCache.Clear();
			_findAllCache.Clear();
		};
	}

	private static List<Type> AllTypes
	{
		get
		{
			if (!_allTypesRef.TryGetTarget(out var types) || types == null)
			{
				lock (_allTypesLock)
				{
					if (!_allTypesRef.TryGetTarget(out types) || types == null)
					{
						types = AppDomain.CurrentDomain.GetAssemblies()
							.Where(a => !a.IsDynamic)
							.SelectMany(a =>
							{
								try { return a.GetTypes(); }
								catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
							})
							.Where(t => t != null && t.IsClass)
							.Select(t => t!)
							.ToList();

						_allTypesRef.SetTarget(types);
					}
				}
			}
			return types!;
		}
	}

	private static IEnumerable<(Type Type, DiscoverableAttribute Meta)> AllWithMeta() =>
		AllTypes.Select(t => (Type: t, Meta: _metaCache.GetOrAdd(t, GetMeta)))
				.Where(x => x.Meta != null)
				.Select(x => (x.Type, x.Meta!));

	private static IEnumerable<(Type Type, DiscoverableAttribute Meta)> Filter<T>() =>
		AllWithMeta()
			.Where(x =>
				typeof(T).IsAssignableFrom(x.Type)
				&& x.Type != typeof(T)
				&& x.Meta.Enabled
			);

	/// <summary>
	/// Retrieves all discoverable types implementing or inheriting T.
	/// Uses a cache per T to avoid repeated enumeration.
	/// </summary>
	public static IReadOnlyList<Type> FindAll<T>()
	{
		var key = typeof(T);
		return _findAllCache.GetOrAdd(key, _ =>
			Filter<T>()
				.OrderBy(x => x.Meta.Priority)
				.Select(x => x.Type)
				.ToList()
		);
	}

	/// <summary>
	/// Attempts to find exactly one discoverable type by name; returns null otherwise.
	/// </summary>
	public static Type FindSingleByName<T>(string name)
	{
		var matches = FindManyByName<T>(name);
		return matches.Count == 1 ? matches[0] : null;
	}

	/// <summary>
	/// Enum overload for TryFindSingleByName; uses the enum's string representation.
	/// </summary>
	public static Type FindSingleByName<T>(Enum name)
		=> FindSingleByName<T>(name.ToEnumString());

	/// <summary>
	/// Attempts to find exactly one discoverable type within the specified category.
	/// </summary>
	/// <typeparam name="T">
	/// The base type or interface used to filter discoverable results. Only types
	/// assignable to <typeparamref name="T"/> will be considered.
	/// </typeparam>
	/// <param name="category">
	/// The category identifier to match against discoverable metadata. Comparison is
	/// case-insensitive.
	/// </param>
	/// <returns>
	/// The single matching type if exactly one discoverable type belongs to the
	/// specified category; otherwise <c>null</c>.
	/// </returns>
	public static Type FindSingleByCategory<T>(string category)
	{
		var matches = FindManyByCategory<T>(category);
		return matches.Count == 1 ? matches[0] : null;
	}

	/// <summary>
	/// Attempts to find exactly one discoverable type within the specified enum-based category.
	/// </summary>
	/// <typeparam name="T">
	/// The base type or interface used to filter discoverable results. Only types
	/// assignable to <typeparamref name="T"/> will be considered.
	/// </typeparam>
	/// <param name="category">
	/// An <see cref="Enum"/> value whose string representation is used as the category
	/// identifier when matching discoverable metadata.
	/// </param>
	/// <returns>
	/// The single matching type if exactly one discoverable type belongs to the
	/// specified category; otherwise <c>null</c>.
	/// </returns>
	public static Type FindSingleByCategory<T>(Enum category)
	{
		var matches = FindManyByCategory<T>(category);
		return matches.Count == 1 ? matches[0] : null;
	}


	public static Type FindSingleByNameAndCategory<T>(string name, Enum category)
	{
		var matches = FindManyByNameAndCategory<T>(name, category);
		return matches.Count == 1 ? matches[0] : null;
	}

	public static Type FindSingleByNameAndCategory<T>(Enum name, string category)
	{
		var matches = FindManyByNameAndCategory<T>(name, category);
		return matches.Count == 1 ? matches[0] : null;
	}

	public static Type FindSingleByNameAndCategory<T>(Enum name, Enum category)
	{
		var matches = FindManyByNameAndCategory<T>(name, category);
		return matches.Count == 1 ? matches[0] : null;
	}

	public static Type FindSingleByNameAndCategory<T>(string name, string category)
	{
		var matches = FindManyByNameAndCategory<T>(name, category);
		return matches.Count == 1 ? matches[0] : null;
	}

	/// <summary>
	/// Retrieves all discoverable types matching the specified internal name.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByName<T>(string name) =>
		Filter<T>()
			.Where(x => string.Equals(x.Meta.InternalName, name, StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.Meta.Priority)
			.Select(x => x.Type)
			.ToList();

	/// <summary>
	/// Searches for all <typeparamref name="T"/> types marked with <c>[Discoverable]</c>
	/// that match the specified enum-based name.
	/// </summary>
	/// <typeparam name="T">
	/// The base type or interface to filter results by. Only types assignable to <typeparamref name="T"/>
	/// will be included in the result.
	/// </typeparam>
	/// <param name="name">
	/// An <see cref="Enum"/> value whose name will be converted (via <c>ToEnumString()</c>)
	/// and matched against discoverable type identifiers (typically by name or metadata).
	/// </param>
	/// <returns>
	/// A read-only list of types assignable to <typeparamref name="T"/> that match the specified name.
	/// </returns>
	public static IReadOnlyList<Type> FindManyByName<T>(Enum name) =>
		FindManyByName<T>(name.ToEnumString());

	/// <summary>
	/// Retrieves all discoverable types within the specified category.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByCategory<T>(string category) =>
		Filter<T>()
			.Where(x => string.Equals(x.Meta.InternalCategory, category, StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.Meta.Priority)
			.Select(x => x.Type)
			.ToList();

	/// <summary>
	/// Overload combining name and category filters for discoverable types.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByNameAndCategory<T>(string name, string category) =>
		Filter<T>()
			.Where(x =>
				string.Equals(x.Meta.InternalName, name, StringComparison.OrdinalIgnoreCase) &&
				string.Equals(x.Meta.InternalCategory, category, StringComparison.OrdinalIgnoreCase)
			)
			.OrderBy(x => x.Meta.Priority)
			.Select(x => x.Type)
			.ToList();

	/// <summary>
	/// Enum overload for FindManyByNameAndCategory; uses the enum's string representations.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByNameAndCategory<T>(Enum name, Enum category) =>
		FindManyByNameAndCategory<T>(name.ToEnumString(), category.ToEnumString());

	/// <summary>
	/// Enum overload for FindManyByNameAndCategory; uses the enum's string representations.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByNameAndCategory<T>(string name, Enum category) =>
		FindManyByNameAndCategory<T>(name, category.ToEnumString());

	/// <summary>
	/// Enum overload for FindManyByNameAndCategory; uses the enum's string representations.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByNameAndCategory<T>(Enum name, string category) =>
		FindManyByNameAndCategory<T>(name.ToEnumString(), category);

	/// <summary>
	/// Enum overload for FindManyByCategory; uses the enum's string representation.
	/// </summary>
	public static IReadOnlyList<Type> FindManyByCategory<T>(Enum category) =>
		FindManyByCategory<T>(category.ToEnumString());

	private static DiscoverableAttribute GetMeta(Type t) =>
		t.GetCustomAttribute<DiscoverableAttribute>(inherit: false);
}





/// <summary>
/// Marks a class as discoverable by the Snap engine's reflection-based systems.
/// This attribute is used to identify types that should be automatically found and exposed
/// for editor tools, debug UIs, runtime registration, scripting, and mod/plugin support.
/// <para/>
/// <b>Typical use cases include:</b>
/// <list type="bullet">
///   <item><description>Auto-discovery of engine components or subsystems</description></item>
///   <item><description>Debug inspection panels</description></item>
///   <item><description>Developer console commands or tools</description></item>
///   <item><description>Scripting or hot-reloading systems</description></item>
///   <item><description>Mod/plugin auto-registration (e.g., user-defined classes in loaded assemblies)</description></item>
/// </list>
/// <para/>
/// Only applies to classes. Inheritance is not supported, and multiple applications are not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DiscoverableAttribute : Attribute
{
	/// <summary>
	/// The display name or identifier for this discoverable item.  
	/// Can be any object, but typically a string or enum value.
	/// </summary>
	public object Name { get; set; }

	/// <summary>
	/// The category under which this discoverable item should be grouped.  
	/// Can be any object, but typically a string or enum value.
	/// </summary>
	public object Category { get; set; }

	/// <summary>
	/// Ordering priority for this discoverable item within its category.  
	/// Lower numbers indicate higher priority.
	/// </summary>
	public int Priority { get; set; }

	/// <summary>
	/// Indicates whether this discoverable item is enabled and should be included in searches or registrations.
	/// </summary>
	public bool Enabled { get; set; } = true;

	internal string InternalName =>
		Name is Enum e
			? $"{e.GetType().FullName}.{e}"
			: Name?.ToString() ?? throw new InvalidOperationException("Discoverable.Name is null");

	internal string InternalCategory =>
		Category is Enum e
			? $"{e.GetType().FullName}.{e}"
			: Category?.ToString() ?? throw new InvalidOperationException("Discoverable.Category is null");
}