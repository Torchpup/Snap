namespace System;

/// <summary>
/// Provides extension methods for working with the Entity hierarchy, including traversal, querying, and alignment.
/// </summary>
public static class EntityExtentions
{
	/// <summary>
	/// Recursively retrieves all descendant entities of the specified entity.
	/// </summary>
	/// <param name="e">The root entity to retrieve descendants from.</param>
	/// <returns>An enumerable of all descendant entities.</returns>
	public static IEnumerable<Entity> GetDescendants(this Entity e)
    {
        foreach (var child in e.Children)
        {
            yield return child;

            foreach (var descendant in child.GetDescendants())
                yield return descendant;
        }
    }

	/// <summary>
	/// Retrieves the first descendant of the specified type from the entity.
	/// </summary>
	/// <typeparam name="T">The type of descendant to retrieve.</typeparam>
	/// <param name="e">The entity to search from.</param>
	/// <returns>The first descendant of type T, or null if not found.</returns>
	public static T GetDescendantOfType<T>(this Entity e) where T : Entity =>
        e.GetDescendants().OfType<T>().FirstOrDefault();

	/// <summary>
	/// Attempts to retrieve the first descendant of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of descendant to retrieve.</typeparam>
	/// <param name="e">The entity to search from.</param>
	/// <param name="result">The resulting entity of type T if found.</param>
	/// <returns>True if a descendant of type T was found; otherwise, false.</returns>
	public static bool TryGetDescendantOfType<T>(this Entity e, out T result) where T : Entity
    {
        foreach (var descendant in e.GetDescendants())
        {
            if (descendant is T t)
            {
                result = t;
                return true;
            }
        }

        result = null;

        return false;
    }

	/// <summary>
	/// Gets the root entity of the hierarchy by traversing up from the given entity.
	/// </summary>
	/// <param name="e">The entity to find the root of.</param>
	/// <returns>The root entity.</returns>
	public static Entity GetRoot(this Entity e)
    {
        var current = e;

        while (current.Parent != null)
            current = current.Parent;

        return current;
    }

	/// <summary>
	/// Determines whether the entity has any direct children of type T.
	/// </summary>
	/// <typeparam name="T">The child type to check for.</typeparam>
	/// <param name="e">The parent entity.</param>
	/// <returns>True if any child of type T exists; otherwise, false.</returns>
	public static bool HasChildOfType<T>(this Entity e) where T : Entity =>
        e.Children.OfType<T>().Any();

	/// <summary>
	/// Applies the specified action to all descendant entities.
	/// </summary>
	/// <param name="e">The root entity.</param>
	/// <param name="action">The action to apply to each descendant.</param>
	public static void ApplyToDescendants(this Entity e, Action<Entity> action)
    {
        foreach (var descendant in e.GetDescendants())
            action(descendant);
    }

	/// <summary>
	/// Retrieves all descendant entities of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of descendants to retrieve.</typeparam>
	/// <param name="e">The root entity.</param>
	/// <returns>An enumerable of descendants of type T.</returns>
	public static IEnumerable<T> GetDescendantsOfType<T>(this Entity e) where T : Entity
    {
        foreach (var a in GetDescendants(e))
        {
            if (a is T)
                yield return (T)a;
        }
    }

	/// <summary>
	/// Returns the global position of the entity by summing all parent positions.
	/// </summary>
	/// <param name="e">The entity to calculate global position for.</param>
	/// <returns>The world-space position of the entity.</returns>
	public static Vect2 GetGlobalPosition(this Entity e)
    {
        if (e == null || e.IsExiting)
            return Vect2.Zero;

        var pos = e._position;

        if (e.IsChild && e.Parent != null)
            pos += e.Parent.GetGlobalPosition();

        // if (e.IsChild)
        // 	return e.Parent?.Position + e._position ?? e._position;
        // else
        // 	return e._position;

        return pos;
    }

	/// <summary>
	/// Returns the local position of the entity.
	/// </summary>
	/// <param name="e">The entity to retrieve local position from.</param>
	/// <returns>The local position of the entity.</returns>
	public static Vect2 GetLocalPosition(this Entity e) => e._position;

	/// <summary>
	/// Calculates the local position relative to the global position.
	/// </summary>
	/// <param name="e">The entity to calculate from.</param>
	/// <param name="position">The absolute position.</param>
	/// <returns>The position relative to the entity’s global position.</returns>
	public static Vect2 GetLocalPosition(this Entity e, Vect2 position)
    {
        if (e == null || e.IsExiting)
            return Vect2.Zero;

        return position - e.GetGlobalPosition();
    }

	/// <summary>
	/// Calculates a global position by adding the given position to the entity’s global position.
	/// </summary>
	/// <param name="e">The entity to calculate from.</param>
	/// <param name="position">The position to offset from global.</param>
	/// <returns>The computed world-space position.</returns>
	public static Vect2 GetWorldPosition(this Entity e, Vect2 position)
    {
        if (e == null || e.IsExiting)
            return Vect2.Zero;

        return position + e.GetGlobalPosition();
    }

	/// <summary>
	/// Yields all ancestor entities from parent to root.
	/// </summary>
	/// <param name="e">The entity to retrieve ancestors from.</param>
	/// <returns>An enumerable of ancestor entities.</returns>
	public static IEnumerable<Entity> GetAncestors(this Entity e)
    {
        var current = e.Parent;

        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }

	/// <summary>
	/// Retrieves all ancestor entities of a specific type.
	/// </summary>
	/// <typeparam name="T">The type of ancestor to retrieve.</typeparam>
	/// <param name="e">The entity to search from.</param>
	/// <returns>An enumerable of matching ancestor entities.</returns>
	public static IEnumerable<T> GetAncestorsOfType<T>(this Entity e) where T : Entity
    {
        foreach (var a in GetAncestors(e))
        {
            if (a is T)
                yield return (T)a;
        }
    }

	/// <summary>
	/// Applies the specified action to all ancestor entities.
	/// </summary>
	/// <param name="e">The entity to start from.</param>
	/// <param name="action">The action to apply to each ancestor.</param>
	public static void ApplyToAncestors(this Entity e, Action<Entity> action)
    {
        foreach (var ancestors in e.GetAncestors())
            action(ancestors);
    }

	/// <summary>
	/// Returns the first ancestor of the specified type, or null if none found.
	/// </summary>
	/// <typeparam name="T">The ancestor type to find.</typeparam>
	/// <param name="e">The entity to search from.</param>
	/// <returns>The matching ancestor, or null.</returns>
	public static T GetAncestorOfType<T>(this Entity e) where T : Entity
        => e.GetAncestors().OfType<T>().FirstOrDefault();

	/// <summary>
	/// Attempts to find the first ancestor of the specified type <typeparamref name="T"/> in the entity hierarchy.
	/// </summary>
	/// <typeparam name="T">The type of ancestor to search for.</typeparam>
	/// <param name="e">The entity whose ancestors will be searched.</param>
	/// <param name="result">
	/// When this method returns, contains the first ancestor of type <typeparamref name="T"/> if found; otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if an ancestor of type <typeparamref name="T"/> was found; otherwise, <c>false</c>.
	/// </returns>
	public static bool TryGetAncestorOfType<T>(this Entity e, out T result) where T : Entity
    {
        var current = e.Parent;
        while (current != null)
        {
            if (current is T t)
            {
                result = t;
                return true;
            }
            current = current.Parent;
        }

        result = null;
        return false;
    }

	/// <summary>
	/// Attempts to find the first ancestor of the specified type.
	/// </summary>
	/// <typeparam name="T">The ancestor type to search for.</typeparam>
	/// <param name="e">The entity to start from.</param>
	/// <returns>True if an ancestor was found; otherwise, false.</returns>
	public static bool HasAncestorOfType<T>(this Entity e) where T : Entity =>
        TryGetAncestorOfType<T>(e, out _);

	/// <summary>
	/// Aligns an entity to the renderer using the specified horizontal and vertical alignment and a positional offset.
	/// </summary>
	/// <param name="entity">The entity to align.</param>
	/// <param name="hAlign">The horizontal alignment (e.g., Left, Center, Right).</param>
	/// <param name="vAlign">The vertical alignment (e.g., Top, Middle, Bottom).</param>
	public static void AlignToRenderer(this Entity entity, HAlign hAlign, VAlign vAlign) =>
        AlignHelpers.AlignToRenderer(entity, hAlign, vAlign);

	/// <summary>
	/// Aligns an entity to the renderer using the specified horizontal and vertical alignment and a positional offset.
	/// </summary>
	/// <param name="entity">The entity to align.</param>
	/// <param name="hAlign">The horizontal alignment (e.g., Left, Center, Right).</param>
	/// <param name="vAlign">The vertical alignment (e.g., Top, Middle, Bottom).</param>
	/// <param name="offset">A pixel offset to apply after alignment.</param>
	public static void AlignToRenderer(this Entity entity, HAlign hAlign, VAlign vAlign, Vect2 offset) =>
        AlignHelpers.AlignToRenderer(entity, hAlign, vAlign, offset);

	/// <summary>
	/// Aligns a child entity relative to a parent entity using the specified alignment and a pixel offset.
	/// </summary>
	/// <param name="child">The child entity to align.</param>
	/// <param name="parent">The parent entity to align against.</param>
	/// <param name="hAlign">The horizontal alignment relative to the parent.</param>
	/// <param name="vAlign">The vertical alignment relative to the parent.</param>
	public static void AlignToEntity(this Entity child, Entity parent, HAlign hAlign, VAlign vAlign) =>
        AlignHelpers.AlignToEntity(parent, child, hAlign, vAlign);

	/// <summary>
	/// Aligns a child entity relative to a parent entity using the specified alignment and a pixel offset.
	/// </summary>
	/// <param name="child">The child entity to align.</param>
	/// <param name="parent">The parent entity to align against.</param>
	/// <param name="hAlign">The horizontal alignment relative to the parent.</param>
	/// <param name="vAlign">The vertical alignment relative to the parent.</param>
	/// <param name="offset">A pixel offset to apply after alignment.</param>
	public static void AlignToEntity(this Entity child, Entity parent, HAlign hAlign, VAlign vAlign, Vect2 offset) =>
        AlignHelpers.AlignToEntity(parent, child, hAlign, vAlign, offset);

	/// <summary>
	/// Creates a new instance of the specified entity type as a child of the current entity, using optional constructor arguments.
	/// </summary>
	/// <typeparam name="TEntity">The type of entity to create.</typeparam>
	/// <param name="entity">The parent entity that will own the new instance.</param>
	/// <param name="args">Optional arguments to pass to the constructor of the entity.</param>
	/// <returns>A newly created instance of type <typeparamref name="TEntity"/>.</returns>
	public static TEntity CreateInstance<TEntity>(this Entity entity, params object[] args) where TEntity : Entity =>
        InstanceHelpers.CreateInstanceFromObject<TEntity>(entity, args);

	/// <summary>
	/// Attempts to create a new instance of the specified entity type as a child of the current entity.
	/// </summary>
	/// <typeparam name="TEntity">The type of entity to create.</typeparam>
	/// <param name="entity">The parent entity that will own the new instance.</param>
	/// <param name="clone">The resulting created instance, or null if creation failed.</param>
	/// <param name="args">Optional arguments to pass to the constructor of the entity.</param>
	/// <returns>True if the instance was successfully created; otherwise, false.</returns>
	public static bool TryCreateInstance<TEntity>(this Entity entity, out TEntity clone, params object[] args) where TEntity : Entity =>
        InstanceHelpers.TryCreateInstanceFromObject(out clone, entity, args);
}
