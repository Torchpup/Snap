namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A base UI container entity that supports dynamic layout updates via dirty state tracking.
/// Automatically tracks changes to its children and invokes layout recalculations.
/// </summary>
public class Panel : Entity
{
	private readonly List<Entity> _entityAdd;
	private DirtyState _state;
	private bool _isAutoSize = true;

	/// <summary>
	/// Gets or sets the size of the panel.
	/// </summary>
	/// <remarks>
	/// When setting a new size, if the value differs from the current size,
	/// it triggers a layout update by marking this panel and all ancestor panels as dirty.
	/// This ensures that parent containers recalculate their layout when child panels change size.
	/// </remarks>
	/// <value>
	/// A <see cref="Vect2"/> representing the width and height of the panel.
	/// </value>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when attempting to set a size with negative width or height values.
	/// </exception>
	public new Vect2 Size
	{
		get => base.Size;
		set
		{
			if (base.Size == value)
				return;
			base.Size = value;
			_isAutoSize = false;

			foreach (var p in this.GetAncestorsOfType<Panel>())
				p.SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);

			SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);
		}
	}

	/// <summary>
	/// Gets or sets whether this panel automatically resizes to fit its children.
	/// </summary>
	/// <remarks>
	/// When set to <c>true</c>, the panel will immediately recalculate its size based on
	/// its visible children. When set to <c>false</c>, the panel retains its current size.
	/// Setting <see cref="Size"/> manually will automatically disable auto-sizing.
	/// </remarks>
	/// <value>
	/// <c>true</c> if the panel sizes itself to its children; otherwise <c>false</c>.
	/// </value>
	public bool AutoSize
	{
		get => _isAutoSize;
		set
		{
			if (_isAutoSize == value)
				return;
			_isAutoSize = value;

			if (_isAutoSize)
				SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);
		}
	}

	/// <summary>
	/// Marks this panel as needing a layout or sort update using the given dirty state flags.
	/// </summary>
	/// <param name="state">The dirty state flags to apply (e.g., Update, Sort).</param>
	public void SetDirtyState(DirtyState state) => _state |= state;

	/// <summary>
	/// Initializes a new <see cref="Panel"/> with the specified child entities.
	/// </summary>
	/// <param name="entities">The child entities to add when the panel is entered.</param>
	public Panel(params Entity[] entities)
	{
		if (entities == null || entities.Length == 0)
			return;

		_entityAdd = [.. entities];
	}

	/// <summary>
	/// Initializes a new empty <see cref="Panel"/>.
	/// </summary>
	public Panel() : this([]) { }

	/// <summary>
	/// Called when the panel enters the scene.
	/// Automatically adds queued children and triggers initial layout.
	/// </summary>
	protected override void OnEnter()
	{
		if (_entityAdd != null)
		{
			base.AddChild([.. _entityAdd]);
			_entityAdd.Clear();

			SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);
		}

		base.OnEnter();
	}

	/// <summary>
	/// Called every frame. If the panel is marked dirty, triggers <see cref="OnDirty"/>.
	/// </summary>
	protected override void OnUpdate()
	{
		if (_state != DirtyState.None)
		{
			OnDirty(_state);

			_state = DirtyState.None;
		}
	}

	/// <summary>
	/// Called when the panel's layout is marked dirty via <see cref="SetDirtyState"/>.
	/// Override this method in subclasses to perform layout recalculation or sorting.
	/// </summary>
	/// <param name="state">The combined dirty state flags.</param>
	protected virtual void OnDirty(DirtyState state) { }

	/// <summary>
	/// Calculates the required size for the panel based on its child entities.
	/// </summary>
	/// <param name="children">The collection of child entities to be arranged within the panel.</param>
	/// <returns>
	/// A <see cref="Vect2"/> representing the calculated width and height needed
	/// to properly contain and arrange the specified children.
	/// The base implementation returns <see cref="Vect2.Zero"/>.
	/// </returns>
	/// <remarks>
	/// This method is called during layout updates to determine the panel's size requirements.
	/// Derived panel classes should override this method to implement specific layout logic
	/// that calculates size based on their children's dimensions and arrangement rules.
	/// </remarks>
	protected virtual Vect2 OnResize(IEnumerable<Entity> children)
	{
		return Vect2.Zero;
	}

	/// <summary>
	/// Adds one or more child entities to the panel and marks the layout as dirty.
	/// </summary>
	/// <param name="children">The entities to add.</param>
	public new void AddChild(params Entity[] children)
	{
		if (children == null || children.Length == 0)
			return;

		IEnumerator Routine(Entity[] children)
		{
			if (Screen == null)
				yield return new WaitWhile(() => Screen == null);

			base.AddChild(children);

			SetDirtyState(DirtyState.AddOrRemove | DirtyState.Sort);

			for (int i = 0; i < children.Length; i++)
				OnChildAdded(children[i]);
		}

		StartRoutine(Routine(children));
	}

	/// <summary>
	/// Called when a child entity is added to this panel.
	/// </summary>
	/// <param name="entity">The child entity that was added to the panel.</param>
	/// <remarks>
	/// This method provides a hook for derived panel classes to perform custom logic
	/// when a new child is added. The base implementation does nothing.
	/// Common use cases include setting up initial layout properties for the child
	/// or triggering a layout recalculation.
	/// </remarks>
	protected virtual void OnChildAdded(Entity entity) { }

	/// <summary>
	/// Removes one or more child entities from the panel and marks the layout as dirty.
	/// </summary>
	/// <param name="children">The entities to remove.</param>
	/// <returns>True if at least one child was removed; otherwise, false.</returns>
	public new bool RemoveChild(params Entity[] children)
	{
		if (children == null || children.Length == 0) return false;

		if (base.RemoveChild(children))
		{
			SetDirtyState(DirtyState.AddOrRemove);

			for (int i = 0; i < children.Length; i++)
				OnChildRemoved(children[i]);

			return true;
		}

		return false;
	}

	/// <summary>
	/// Called when a child entity is removed from this panel.
	/// </summary>
	/// <param name="entity">The child entity that was removed from the panel.</param>
	/// <remarks>
	/// This method provides a hook for derived panel classes to perform cleanup
	/// or custom logic when a child is removed. The base implementation does nothing.
	/// Common use cases include releasing resources associated with the child
	/// or triggering a layout recalculation.
	/// </remarks>
	protected virtual void OnChildRemoved(Entity entity) { }

	/// <summary>
	/// Removes all child entities from the panel and marks the layout as dirty.
	/// </summary>
	public new void ClearChildren()
	{
		if (Children.Count == 0)
			return;

		if (base.RemoveChild([.. Children]))
		{
			OnChildrenCleared();

			SetDirtyState(DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Called when all child entities are cleared from this panel.
	/// </summary>
	/// <remarks>
	/// This method provides a hook for derived panel classes to perform cleanup
	/// or reset internal state when all children are removed at once.
	/// The base implementation does nothing.
	/// </remarks>
	protected virtual void OnChildrenCleared() { }
}
