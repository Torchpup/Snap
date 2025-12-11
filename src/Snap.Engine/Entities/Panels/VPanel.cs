namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A vertical layout panel that stacks child entities top to bottom, with configurable spacing and alignment.
/// </summary>
public class VPanel : Panel
{
	private float _spacing;
	private bool _isAutoSize = true;
	private HAlign _hAlign = HAlign.Left;
	private VAlign _vAlign = VAlign.Top;
	// private bool _isDirty = true;

	/// <summary>
	/// Gets or sets the size of the panel.
	/// Setting this manually disables automatic sizing based on children.
	/// </summary>
	public new Vect2 Size
	{
		get => base.Size;
		set
		{
			if (base.Size == value)
				return;
			base.Size = value;
			_isAutoSize = false;
			// _isDirty = true;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the horizontal alignment of the children within each row.
	/// </summary>
	public HAlign HAlign
	{
		get => _hAlign;
		set
		{
			if (_hAlign == value)
				return;
			_hAlign = value;
			// _isDirty = true;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the vertical alignment of the entire group of children within the panel.
	/// </summary>
	public VAlign VAlign
	{
		get => _vAlign;
		set
		{
			if (_vAlign == value)
				return;
			_vAlign = value;
			// _isDirty = true;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the spacing between each child element (in pixels).
	/// </summary>
	public float Spacing
	{
		get => _spacing;
		set
		{
			if (_spacing == value) return;
			_spacing = value;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Initializes a new <see cref="VPanel"/> with a specified spacing and child entities.
	/// </summary>
	/// <param name="spacing">The spacing between each child element.</param>
	/// <param name="entities">The entities to add to the panel.</param>
	public VPanel(float spacing, params Entity[] entities) : base(entities)
	{
		_spacing = spacing;

		UpdateSize(entities);
	}

	/// <summary>
	/// Initializes a new <see cref="VPanel"/> with a default spacing of 4 pixels.
	/// </summary>
	/// <param name="entities">The entities to add to the panel.</param>
	public VPanel(params Entity[] entities) : this(spacing: 4, entities) { }

	/// <summary>
	/// Called when the panel is marked as dirty.
	/// Recalculates child layout, sizes, and alignment.
	/// </summary>
	/// <param name="state">The dirty state flags that triggered the update.</param>
	protected override void OnDirty(DirtyState state)
	{
		var allKids = Children
			.Where(x => x.IsVisible && !x.IsExiting)
			.ToList();

		if (allKids.Count == 0)
		{
			if (_isAutoSize)
				base.Size = Vect2.Zero;
			base.OnDirty(state);
			return;
		}

		var width = allKids.Max(x => x.Size.X);
		var height = allKids.Sum(x => x.Size.Y + _spacing) - _spacing;
		var offset = 0f;

		for (int i = 0; i < allKids.Count; i++)
		{
			var child = allKids[i];
			var eWidth = AlignHelpers.AlignWidth(Size.X, width, HAlign);
			var eHeight = AlignHelpers.AlignHeight(Size.Y, height, VAlign);

			child.Position = new Vect2(eWidth, offset + eHeight).Round();

			offset += child.Size.Y;
			if (i < allKids.Count - 1)
				offset += _spacing;
		}

		UpdateSize(allKids);

		foreach (var e in this.GetAncestorsOfType<Panel>())
			e.SetDirtyState(DirtyState.Update | DirtyState.Sort);

		if (IsTopmostScreen || Parent == null)
			Screen?.SetDirtyState(DirtyState.Sort | DirtyState.Update);

		base.OnDirty(state);
	}

	// protected override void OnUpdate()
	// {
	// 	if (_isDirty)
	// 	{
	// 		foreach (var e in this.GetAncestorsOfType<Panel>())
	// 			e.SetDirtyState(DirtyState.Update | DirtyState.Sort);
	// 		// SetDirtyState(DirtyState.Update | DirtyState.Sort); //<-- Dont add

	// 		_isDirty = false;
	// 	}

	// 	base.OnUpdate();
	// }

	private void UpdateSize(IEnumerable<Entity> children)
	{
		if (!_isAutoSize)
			return;
		if (!children.Any())
		{
			base.Size = Vect2.Zero;
			return;
		}

		var vChildren = children
			.Where(x => x.IsVisible && !x.IsExiting)
			.ToList();

		if (vChildren.Count == 0)
		{
			base.Size = Vect2.Zero;
			return;
		}

		float width = vChildren.Max(x => x.Size.X);
		float totalHeight = 0f;
		for (int i = 0; i < vChildren.Count; i++)
		{
			totalHeight += vChildren[i].Size.Y;
			if (i < vChildren.Count - 1)
				totalHeight += _spacing;
		}

		var newSize = new Vect2(width, totalHeight);

		if (base.Size != newSize)
			base.Size = newSize;
	}
}
