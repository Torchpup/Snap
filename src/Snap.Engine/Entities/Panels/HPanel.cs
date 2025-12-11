namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A horizontal layout panel that arranges child entities side-by-side with optional spacing and alignment.
/// </summary>
public class HPanel : Panel
{
	private float _spacing;
	private bool _isAutoSize = true;
	private HAlign _hAlign = HAlign.Left;
	private VAlign _vAlign = VAlign.Top;

	/// <summary>
	/// Gets or sets the size of the panel.
	/// Setting a custom size disables autosizing based on child content.
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
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the horizontal alignment of the child elements within the panel bounds.
	/// </summary>
	public HAlign HAlign
	{
		get => _hAlign;
		set
		{
			if (_hAlign == value)
				return;
			_hAlign = value;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the vertical alignment of the child elements within the panel bounds.
	/// </summary>
	public VAlign VAlign
	{
		get => _vAlign;
		set
		{
			if (_vAlign == value)
				return;
			_vAlign = value;
			SetDirtyState(DirtyState.Sort | DirtyState.Update);
		}
	}

	/// <summary>
	/// Gets or sets the spacing (in pixels) between child elements.
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
	/// Initializes a new <see cref="HPanel"/> with a specified spacing and child entities.
	/// </summary>
	/// <param name="spacing">The spacing between each child element, in pixels.</param>
	/// <param name="entities">Optional child entities to add to the panel.</param>
	public HPanel(float spacing, params Entity[] entities) : base(entities)
	{
		_spacing = spacing;

		UpdateSize(entities);
	}

	/// <summary>
	/// Initializes a new <see cref="HPanel"/> with a default spacing of 4 pixels.
	/// </summary>
	/// <param name="entities">Optional child entities to add to the panel.</param>
	public HPanel(params Entity[] entities) : this(spacing: 4, entities) { }

	/// <summary>
	/// Recalculates the layout of child elements and updates parent panels and screen dirty states.
	/// </summary>
	/// <param name="state">The dirty state that triggered this update.</param>
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

		var width = allKids.Sum(x => x.Size.X + _spacing) - _spacing;
		var height = allKids.Max(x => x.Size.Y);
		var offset = 0f;

		for (int i = 0; i < allKids.Count; i++)
		{
			var child = allKids[i];
			var eWidth = AlignHelpers.AlignWidth(Size.X, width, HAlign);
			var eHeight = AlignHelpers.AlignHeight(Size.Y, height, VAlign);

			child.Position = new Vect2(offset + eWidth, eHeight).Round();

			offset += child.Size.X;
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

		float height = vChildren.Max(x => x.Size.Y);
		float totalWidth = 0f;
		for (int i = 0; i < vChildren.Count; i++)
		{
			totalWidth += vChildren[i].Size.X;
			if (i < vChildren.Count - 1)
				totalWidth += _spacing;
		}

		var newSize = new Vect2(totalWidth, height);

		if (base.Size != newSize)
			base.Size = newSize;
	}
}
