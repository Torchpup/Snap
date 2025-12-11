namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A base UI container entity that supports dynamic layout updates via dirty state tracking.
/// Automatically tracks changes to its children and invokes layout recalculations.
/// </summary>
public class Panel : Entity
{
	private readonly List<Entity> _entityAdd;
	private DirtyState _state;

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

		_entityAdd = new List<Entity>(entities);
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
			// for (int i = _entityAdd.Count - 1; i >= 0 ; i--)
			// 	_entityAdd[i].Position = Position;

			base.AddChild([.. _entityAdd]);
			_entityAdd.Clear();

			SetDirtyState(DirtyState.Update | DirtyState.Sort);
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

			SetDirtyState(DirtyState.Update | DirtyState.Sort);
		}

		StartRoutine(Routine(children));
	}

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
			SetDirtyState(DirtyState.Update);

			return true;
		}

		return false;
	}

	/// <summary>
	/// Removes all child entities from the panel and marks the layout as dirty.
	/// </summary>
	public new void ClearChildren()
	{
		if (Children.Count == 0)
			return;

		if (base.RemoveChild([.. Children]))
			SetDirtyState(DirtyState.Update);
	}
}
