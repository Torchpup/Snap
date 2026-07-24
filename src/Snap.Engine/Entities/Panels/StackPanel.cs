namespace Snap.Engine.Entities.Panels;

/// <summary>
/// Specifies the direction in which child elements are arranged within a <see cref="StackPanel"/>.
/// </summary>
public enum StackDirection
{
	/// <summary>
	/// Child elements are arranged vertically, from top to bottom.
	/// </summary>
	Vertical,

	/// <summary>
	/// Child elements are arranged horizontally, from left to right.
	/// </summary>
	Horizontal
}

/// <summary>
/// Arranges child entities in a single line, either horizontally or vertically, with optional spacing between them.
/// </summary>
/// <remarks>
/// The panel can automatically size itself to fit all children, or have its size manually set.
/// Supports alignment of children within the available space.
/// </remarks>
public class StackPanel : Panel
{
	private float _spacing;

	private HAlign _hAlign = HAlign.Left;
	private VAlign _vAlign = VAlign.Top;

	/// <summary>
	/// Gets or sets the horizontal alignment of the children within each row.
	/// </summary>
	/// <value>The horizontal alignment of children within their allocated space.</value>
	public HAlign HAlign
	{
		get => _hAlign;
		set
		{
			if (_hAlign == value)
				return;
			_hAlign = value;
			SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Gets or sets the vertical alignment of the entire group of children within the panel.
	/// </summary>
	/// <value>The vertical alignment of the children group within the panel.</value>
	public VAlign VAlign
	{
		get => _vAlign;
		set
		{
			if (_vAlign == value)
				return;
			_vAlign = value;
			SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Gets or sets the spacing between each child element (in pixels).
	/// </summary>
	/// <value>The spacing between child elements in pixels.</value>
	public float Spacing
	{
		get => _spacing;
		set
		{
			if (_spacing == value) return;
			_spacing = value;
			SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
		}
	}

	/// <summary>
	/// Gets the stacking direction of the panel.
	/// </summary>
	/// <value>The direction in which child elements are arranged.</value>
	public StackDirection Direction { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="StackPanel"/> class with specified spacing, direction, and child entities.
	/// </summary>
	/// <param name="spacing">The spacing between child elements in pixels.</param>
	/// <param name="direction">The direction in which to stack child elements.</param>
	/// <param name="children">The child entities to arrange in the stack.</param>
	public StackPanel(int spacing, StackDirection direction, params Entity[] children) : base(children)
	{
		_spacing = spacing;
		Direction = direction;

		LocalSize = OnResize(children);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StackPanel"/> class with default spacing (4 pixels).
	/// </summary>
	/// <param name="direction">The direction in which to stack child elements.</param>
	/// <param name="children">The child entities to arrange in the stack.</param>
	public StackPanel(StackDirection direction, params Entity[] children)
		: this(4, direction, children) { }

	/// <summary>
	/// Called when the panel requires a layout update.
	/// Recalculates panel size (if auto-sized) and positions all visible children according to stacking direction and alignment.
	/// </summary>
	/// <param name="state">The type of change that triggered the update.</param>
	protected override void OnDirty(DirtyState state)
	{
		var children = Children
			.Where(x => x.Visible && !x.IsExiting)
			.ToList();

		if (children.Count == 0)
		{
			if (AutoSize)
				LocalSize = Vect2.Zero;
			base.OnDirty(state);
			return;
		}

		if (AutoSize)
		{
			LocalSize = OnResize(Children);
			SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);
		}

		if (Direction == StackDirection.Vertical)
			VPanelStack(children);
		else
			HPanelStack(children);

		if (IsTopmostScreen || Parent == null)
			Screen?.SetDirtyState(DirtyState.Sort | DirtyState.AddOrRemove);

		base.OnDirty(state);
	}

	private void HPanelStack(List<Entity> children)
	{
		var width = children.Sum(x => x.Size.X + _spacing) - _spacing;
		var height = children.Max(x => x.Size.Y);
		var offset = 0f;

		for (int i = 0; i < children.Count; i++)
		{
			var child = children[i];
			var eWidth = AlignHelpers.AlignWidth(Size.X, width, _hAlign);
			var eHeight = AlignHelpers.AlignHeight(Size.Y, height, _vAlign);

			child.Position = new Vect2(offset + eWidth, eHeight).Round();

			offset += child.Size.X;
			if (i < children.Count - 1)
				offset += _spacing;
		}
	}

	private void VPanelStack(List<Entity> children)
	{
		var width = children.Max(x => x.Size.X);
		var height = children.Sum(x => x.Size.Y + _spacing) - _spacing;
		var offset = 0f;

		for (int i = 0; i < children.Count; i++)
		{
			var child = children[i];
			var eWidth = AlignHelpers.AlignWidth(Size.X, width, _hAlign);
			var eHeight = AlignHelpers.AlignHeight(Size.Y, height, _vAlign);

			child.Position = new Vect2(eWidth, offset + eHeight).Round();

			offset += child.Size.Y;
			if (i < children.Count - 1)
				offset += _spacing;
		}
	}

	/// <summary>
	/// Calculates the required size for the panel based on its child entities and stacking direction.
	/// </summary>
	/// <param name="children">The collection of child entities to be arranged within the panel.</param>
	/// <returns>
	/// A <see cref="Vect2"/> representing the calculated width and height needed
	/// to contain all visible children with proper spacing.
	/// Returns <see cref="Vect2.Zero"/> if no children are visible or auto-sizing is disabled.
	/// </returns>
	protected override Vect2 OnResize(IEnumerable<Entity> children)
	{
		if (!AutoSize)
			return Size;

		var visible = children
			.Where(x => x.Visible && !x.IsExiting)
			.ToList();

		if (visible.Count == 0)
			return Vect2.Zero;

		if (Direction == StackDirection.Vertical)
		{
			float width = visible.Max(x => x.Size.X);
			float totalHeight = 0f;
			for (int i = 0; i < visible.Count; i++)
			{
				totalHeight += visible[i].Size.Y;
				if (i < visible.Count - 1)
					totalHeight += _spacing;
			}
			return new Vect2(width, totalHeight);
		}
		else
		{
			float height = visible.Max(x => x.Size.Y);
			float totalWidth = 0f;
			for (int i = 0; i < visible.Count; i++)
			{
				totalWidth += visible[i].Size.X;
				if (i < visible.Count - 1)
					totalWidth += _spacing;
			}
			return new Vect2(totalWidth, height);
		}
	}
}