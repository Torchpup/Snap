namespace Snap.Engine.Entities.Panels;

/// <summary>
/// A panel that anchors its child entity to a specific position within its parent container.
/// The position is determined by horizontal alignment, vertical alignment, and an optional pixel offset.
/// </summary>
public class AnchorPanel : Panel
{
	/// <summary>
	/// Gets or sets the horizontal alignment of the panel within its parent container.
	/// </summary>
	public HAlign HAlign { get; set; }

	/// <summary>
	/// Gets or sets the vertical alignment of the panel within its parent container.
	/// </summary>
	public VAlign VAlign { get; set; }

	/// <summary>
	/// Gets or sets the pixel offset applied to the final calculated anchor position.
	/// Positive X moves right, positive Y moves down.
	/// </summary>
	public Vect2 Offset { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="AnchorPanel"/> class with the specified child entity.
	/// </summary>
	/// <param name="child">The child entity to be managed and positioned by this anchor panel.</param>
	public AnchorPanel(Entity child) : base(child) { }

	/// <summary>
	/// Called when the panel requires a layout update.
	/// Recalculates the anchored position based on parent size, alignment properties, and offset.
	/// </summary>
	/// <param name="state">The type of change that triggered the update.</param>
	/// <remarks>
	/// Exits early if the panel has no parent. Uses <see cref="AlignHelpers.AlignWidth(float, float, HAlign, float)"/> and 
	/// <see cref="AlignHelpers.AlignHeight(float, float, VAlign, float)"/> for position calculation.
	/// </remarks>
	protected override void OnDirty(DirtyState state)
	{
		if (Parent == null) return;

		var parentSize = Parent.Size;
		var mySize = Size;

		float x = AlignHelpers.AlignWidth(parentSize.X, mySize.X, HAlign, Offset.X);
		float y = AlignHelpers.AlignHeight(parentSize.Y, mySize.Y, VAlign, Offset.Y);

		LocalPosition = new Vect2(x, y);

		base.OnDirty(state);
	}
}