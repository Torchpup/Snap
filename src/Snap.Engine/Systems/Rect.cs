namespace Snap.Engine.Systems;

/// <summary>
/// Represents a two-dimensional axis-aligned rectangle defined by its position (X, Y) and size (Width, Height).
/// Provides methods for geometric operations such as containment, intersection, union, inflation, offsetting,
/// and utility helpers for layout, collision detection, and spatial queries.
/// </summary>
public struct Rect2 : IEquatable<Rect2>
{
	private const float Epsilon = 1e-6f;

	#region Properties
	/// <summary>
	/// The X-coordinate of the rectangle's top-left corner.
	/// </summary>
	public float X;

	/// <summary>
	/// The Y-coordinate of the rectangle's top-left corner.
	/// </summary>
	public float Y;

	/// <summary>
	/// The width of the rectangle.
	/// </summary>
	public float Width;

	/// <summary>
	/// The height of the rectangle.
	/// </summary>
	public float Height;

	/// <summary>
	/// Gets the X-coordinate of the rectangle's left edge.
	/// </summary>
	public readonly float Left => X;

	/// <summary>
	/// Gets the Y-coordinate of the rectangle's top edge.
	/// </summary>
	public readonly float Top => Y;

	/// <summary>
	/// Gets the X-coordinate of the rectangle's right edge.
	/// </summary>
	public readonly float Right => X + Width;

	/// <summary>
	/// Gets the Y-coordinate of the rectangle's bottom edge.
	/// </summary>
	public readonly float Bottom => Y + Height;

	/// <summary>
	/// Gets an empty rectangle with all components set to zero.
	/// </summary>
	public static Rect2 Empty => new(0, 0, 0, 0);

	/// <summary>
	/// Gets a value indicating whether this rectangle is effectively empty,
	/// defined as having a width and height less than or equal to a small epsilon.
	/// </summary>
	public readonly bool IsEmpty => MathF.Abs(Width) <= Epsilon && MathF.Abs(Height) <= Epsilon;

	/// <summary>
	/// Gets or sets the center point of the rectangle.
	/// </summary>
	/// <value>
	/// When retrieved, returns the midpoint of the rectangle as a <see cref="Vect2"/>.
	/// When set, adjusts the rectangle's position so that its center aligns with the specified value.
	/// </value>
	public Vect2 Center
	{
		readonly get => new(X + Width * 0.5f, Y + Height * 0.5f);
		set
		{
			// Setting a new center must move X,Y so that the rect is centered there.
			X = value.X - Width * 0.5f;
			Y = value.Y - Height * 0.5f;
		}
	}

	/// <summary>
	/// Gets the area of the rectangle, calculated as width multiplied by height.
	/// </summary>
	public readonly float Area => Width * Height;

	/// <summary>
	/// Gets the aspect ratio of the rectangle, defined as width divided by height.
	/// Returns 0 if the height is zero.
	/// </summary>
	public readonly float AspectRatio => Height != 0f ? Width / Height : 0f;

	/// <summary>
	/// Gets the coordinates of the rectangle's top-left corner.
	/// </summary>
	public readonly Vect2 TopLeft => new(Left, Top);

	/// <summary>
	/// Gets the coordinates of the rectangle's top-right corner.
	/// </summary>
	public readonly Vect2 TopRight => new(Right, Top);

	/// <summary>
	/// Gets the coordinates of the rectangle's bottom-left corner.
	/// </summary>
	public readonly Vect2 BottomLeft => new(Left, Bottom);

	/// <summary>
	/// Gets the coordinates of the rectangle's bottom-right corner.
	/// </summary>
	public readonly Vect2 BottomRight => new(Right, Bottom);

	/// <summary>
	/// Gets the midpoint along the top edge of the rectangle.
	/// </summary>
	public readonly Vect2 MidTop => new(Center.X, Top);

	/// <summary>
	/// Gets the midpoint along the bottom edge of the rectangle.
	/// </summary>
	public readonly Vect2 MidBottom => new(Center.X, Bottom);

	/// <summary>
	/// Gets the midpoint along the left edge of the rectangle.
	/// </summary>
	public readonly Vect2 MidLeft => new(Left, Center.Y);


	/// <summary>
	/// Gets the midpoint along the right edge of the rectangle.
	/// </summary>
	public readonly Vect2 MidRight => new(Right, Center.Y);

	/// <summary>
	/// Gets or sets the position of the rectangle's top-left corner as a <see cref="Vect2"/>.
	/// </summary>
	/// <value>
	/// When retrieved, returns the current X and Y coordinates.
	/// When set, updates the rectangle's X and Y values to match the specified vector.
	/// </value>
	public Vect2 Position
	{
		readonly get => new(X, Y);
		set
		{
			X = value.X;
			Y = value.Y;
		}
	}

	/// <summary>
	/// Gets or sets the size of the rectangle as a <see cref="Vect2"/>.
	/// </summary>
	/// <value>
	/// When retrieved, returns the current width and height.
	/// When set, updates the rectangle's width and height to match the specified vector.
	/// </value>
	public Vect2 Size
	{
		readonly get => new(Width, Height);
		set
		{
			Width = value.X;
			Height = value.Y;
		}
	}
	#endregion


	#region Constuctors
	/// <summary>
	/// Initializes a new instance of the <see cref="Rect2"/> struct with the specified position and size.
	/// </summary>
	/// <param name="x">The X-coordinate of the rectangle's top-left corner.</param>
	/// <param name="y">The Y-coordinate of the rectangle's top-left corner.</param>
	/// <param name="width">The width of the rectangle.</param>
	/// <param name="height">The height of the rectangle.</param>
	public Rect2(float x, float y, float width, float height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Rect2"/> struct with the specified position and size.
	/// </summary>
	/// <param name="position">The top-left corner of the rectangle as a <see cref="Vect2"/>.</param>
	/// <param name="size">The size of the rectangle as a <see cref="Vect2"/>.</param>
	public Rect2(in Vect2 position, in Vect2 size)
		: this(position.X, position.Y, size.X, size.Y) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Rect2"/> struct from an <see cref="SFRectF"/>.
	/// </summary>
	/// <param name="rect">The source rectangle defined with floating-point values.</param>
	internal Rect2(SFRectF rect) : this(rect.Left, rect.Top, rect.Width, rect.Height) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Rect2"/> struct from an <see cref="SFRectI"/>.
	/// </summary>
	/// <param name="rect">The source rectangle defined with integer values.</param>
	internal Rect2(SFRectI rect) : this(rect.Left, rect.Top, rect.Width, rect.Height) { }

	/// <summary>
	/// Deconstructs this rectangle into its component values.
	/// </summary>
	/// <param name="x">The X-coordinate of the rectangle's top-left corner.</param>
	/// <param name="y">The Y-coordinate of the rectangle's top-left corner.</param>
	/// <param name="w">The width of the rectangle.</param>
	/// <param name="h">The height of the rectangle.</param>
	public readonly void Deconstruct(out float x, out float y, out float w, out float h)
	{
		x = X;
		y = Y;
		w = Width;
		h = Height;
	}
	#endregion


	#region Operator: ==, !=
	/// <summary>
	/// Determines whether two <see cref="Rect2"/> instances are equal.
	/// </summary>
	/// <param name="a">The first rectangle to compare.</param>
	/// <param name="b">The second rectangle to compare.</param>
	/// <returns><c>true</c> if the rectangles are equal; otherwise, <c>false</c>.</returns>
	public static bool operator ==(in Rect2 a, in Rect2 b) => a.Equals(b);

	/// <summary>
	/// Determines whether two <see cref="Rect2"/> instances are not equal.
	/// </summary>
	/// <param name="a">The first rectangle to compare.</param>
	/// <param name="b">The second rectangle to compare.</param>
	/// <returns><c>true</c> if the rectangles are not equal; otherwise, <c>false</c>.</returns>
	public static bool operator !=(in Rect2 a, in Rect2 b) => !a.Equals(b);
	#endregion


	#region Operator: + / -
	/// <summary>
	/// Expands the specified <see cref="Rect2"/> by a uniform margin.
	/// </summary>
	/// <param name="a">The rectangle to expand.</param>
	/// <param name="b">
	/// The margin to apply. The rectangle's position is shifted outward by this value,
	/// and its width and height are increased by twice this value.
	/// </param>
	/// <returns>
	/// A new <see cref="Rect2"/> representing the expanded rectangle.
	/// </returns>
	public static Rect2 operator +(in Rect2 a, float b)
	{
		return new Rect2(
			a.X - b,
			a.Y - b,
			a.Width + b * 2f,
			a.Height + b * 2f
		);
	}

	/// <summary>
	/// Translates the specified <see cref="Rect2"/> by the given vector.
	/// </summary>
	/// <param name="a">The rectangle to translate.</param>
	/// <param name="b">The vector specifying the translation offset.</param>
	/// <returns>A new <see cref="Rect2"/> representing the translated rectangle.</returns>
	public static Rect2 operator +(in Rect2 a, in Vect2 b)
		=> new(a.X + b.X, a.Y + b.Y, a.Width, a.Height);

	/// <summary>
	/// Shrinks the specified <see cref="Rect2"/> by a uniform margin.
	/// </summary>
	/// <param name="a">The rectangle to shrink.</param>
	/// <param name="b">
	/// The margin to apply. The rectangle's position is shifted inward by this value,
	/// and its width and height are decreased by twice this value.
	/// </param>
	/// <returns>A new <see cref="Rect2"/> representing the shrunken rectangle.</returns>
	public static Rect2 operator -(in Rect2 a, float b) => a + -b;

	/// <summary>
	/// Translates the specified <see cref="Rect2"/> by the negative of the given vector.
	/// </summary>
	/// <param name="a">The rectangle to translate.</param>
	/// <param name="b">The vector specifying the translation offset.</param>
	/// <returns>A new <see cref="Rect2"/> representing the translated rectangle.</returns>
	public static Rect2 operator -(in Rect2 a, in Vect2 b)
		=> new(a.X - b.X, a.Y - b.Y, a.Width, a.Height);
	#endregion


	#region Implicit Operators
	/// <summary>
	/// Defines an implicit conversion from <see cref="Rect2"/> to <see cref="SFRectF"/>.
	/// </summary>
	/// <param name="v">The source rectangle.</param>
	/// <returns>
	/// A new <see cref="SFRectF"/> with the same position and size as the <see cref="Rect2"/>.
	/// </returns>
	public static implicit operator SFRectF(in Rect2 v) =>
		new(v.X, v.Y, v.Width, v.Height);

	/// <summary>
	/// Defines an implicit conversion from <see cref="Rect2"/> to <see cref="SFRectI"/>.
	/// </summary>
	/// <param name="v">The source rectangle.</param>
	/// <returns>
	/// A new <see cref="SFRectI"/> with the same position and size as the <see cref="Rect2"/>,
	/// with components cast to integers.
	/// </returns>
	public static implicit operator SFRectI(in Rect2 v) =>
		new((int)v.X, (int)v.Y, (int)v.Width, (int)v.Height);
	#endregion


	#region IEquatable
	/// <summary>
	/// Indicates whether the current <see cref="Rect2"/> is equal to another <see cref="Rect2"/>.
	/// </summary>
	/// <param name="other">The rectangle to compare with this instance.</param>
	/// <returns>
	/// <c>true</c> if the rectangles have the same position and size; otherwise, <c>false</c>.
	/// </returns>
	public readonly bool Equals(Rect2 other) =>
		X.Equals(other.X) && Y.Equals(other.Y) &&
		Width.Equals(other.Width) && Height.Equals(other.Height);

	/// <summary>
	/// Determines whether the specified object is equal to the current <see cref="Rect2"/>.
	/// </summary>
	/// <param name="obj">The object to compare with this instance.</param>
	/// <returns>
	/// <c>true</c> if the specified object is a <see cref="Rect2"/> and is equal to this instance;
	/// otherwise, <c>false</c>.
	/// </returns>
	public override readonly bool Equals([NotNullWhen(true)] object obj) =>
		obj is Rect2 value && Equals(value);

	/// <summary>
	/// Returns a hash code for this <see cref="Rect2"/>.
	/// </summary>
	/// <returns>
	/// A 32-bit signed integer hash code that uniquely represents the rectangle's position and size.
	/// </returns>
	public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

	/// <summary>
	/// Returns a string that represents the current <see cref="Rect2"/>.
	/// </summary>
	/// <returns>
	/// A string in the format <c>Rect(X,Y,Width,Height)</c>.
	/// </returns>
	public override readonly string ToString() => $"Rect({X},{Y},{Width},{Height})";
	#endregion


	#region Contains
	/// <summary>
	/// Determines whether the specified point lies within the bounds of this rectangle.
	/// </summary>
	/// <param name="px">The X-coordinate of the point to test.</param>
	/// <param name="py">The Y-coordinate of the point to test.</param>
	/// <returns><c>true</c> if the point is inside the rectangle; otherwise, <c>false</c>.</returns>
	public readonly bool Contains(float px, float py)
	{
		return px >= Left && px < Right
			&& py >= Top && py < Bottom;
	}

	/// <summary>
	/// Determines whether the specified point lies within the bounds of this rectangle.
	/// </summary>
	/// <param name="point">The point to test.</param>
	/// <returns><c>true</c> if the point is inside the rectangle; otherwise, <c>false</c>.</returns>
	public readonly bool Contains(in Vect2 point)
		=> Contains(point.X, point.Y);

	/// <summary>
	/// Determines whether the specified rectangle is entirely contained within this rectangle.
	/// </summary>
	/// <param name="other">The rectangle to test.</param>
	/// <returns><c>true</c> if the rectangle is fully contained; otherwise, <c>false</c>.</returns>
	public readonly bool Contains(in Rect2 other)
	{
		return other.Left >= Left
			&& other.Right <= Right
			&& other.Top >= Top
			&& other.Bottom <= Bottom;
	}
	#endregion


	#region Intersects
	/// <summary>
	/// Determines whether this rectangle intersects with another rectangle.
	/// </summary>
	/// <param name="other">The rectangle to test for intersection.</param>
	/// <returns><c>true</c> if the rectangles overlap; otherwise, <c>false</c>.</returns>
	public readonly bool Intersects(in Rect2 other)
	{
		return other.Left < Right
			&& Left < other.Right
			&& other.Top < Bottom
			&& Top < other.Bottom;
	}

	/// <summary>
	/// Computes the intersection of two rectangles.
	/// </summary>
	/// <param name="a">The first rectangle.</param>
	/// <param name="b">The second rectangle.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> representing the overlapping region of the two rectangles,
	/// or <see cref="Rect2.Empty"/> if they do not overlap.
	/// </returns>
	public static Rect2 Intersection(in Rect2 a, in Rect2 b)
	{
		float x1 = MathF.Max(a.Left, b.Left);
		float y1 = MathF.Max(a.Top, b.Top);
		float x2 = MathF.Min(a.Right, b.Right);
		float y2 = MathF.Min(a.Bottom, b.Bottom);

		if (x2 <= x1 || y2 <= y1)
		{
			// No overlap
			return Empty;
		}

		return new Rect2(x1, y1, x2 - x1, y2 - y1);
	}

	/// <summary>
	/// Computes the intersection of this rectangle with another rectangle.
	/// </summary>
	/// <param name="other">The rectangle to intersect with.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> representing the overlapping region of the two rectangles,
	/// or <see cref="Rect2.Empty"/> if they do not overlap.
	/// </returns>
	public readonly Rect2 Intersection(in Rect2 other)
		=> Intersection(this, other);
	#endregion


	#region Union
	/// <summary>
	/// Computes the union of this rectangle with another rectangle.
	/// </summary>
	/// <param name="other">The rectangle to combine with this instance.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> that fully encompasses both rectangles.
	/// </returns>
	public readonly Rect2 Union(in Rect2 other)
		=> Union(this, other);

	/// <summary>
	/// Computes the union of two rectangles.
	/// </summary>
	/// <param name="a">The first rectangle.</param>
	/// <param name="b">The second rectangle.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> that fully encompasses both rectangles.
	/// </returns>
	public static Rect2 Union(in Rect2 a, in Rect2 b)
	{
		float x1 = MathF.Min(a.Left, b.Left);
		float y1 = MathF.Min(a.Top, b.Top);
		float x2 = MathF.Max(a.Right, b.Right);
		float y2 = MathF.Max(a.Bottom, b.Bottom);

		return new Rect2(x1, y1, x2 - x1, y2 - y1);
	}
	#endregion


	#region Inflate
	/// <summary>
	/// Returns a new rectangle padded uniformly by the specified amount.
	/// </summary>
	/// <param name="amount">
	/// The amount to pad on all sides. The rectangle's position shifts outward,
	/// and its width and height increase accordingly.
	/// </param>
	/// <returns>A new <see cref="Rect2"/> representing the padded rectangle.</returns>
	public readonly Rect2 Pad(float amount) => Inflate(this, amount, amount);

	/// <summary>
	/// Returns a new rectangle inflated by the specified horizontal and vertical amounts.
	/// </summary>
	/// <param name="dx">The amount to inflate horizontally (left and right).</param>
	/// <param name="dy">The amount to inflate vertically (top and bottom).</param>
	/// <returns>A new <see cref="Rect2"/> representing the inflated rectangle.</returns>
	public readonly Rect2 Inflate(float dx, float dy) => Inflate(this, dx, dy);

	/// <summary>
	/// Returns a new rectangle inflated by the specified horizontal and vertical amounts.
	/// </summary>
	/// <param name="r">The rectangle to inflate.</param>
	/// <param name="dx">The amount to inflate horizontally (left and right).</param>
	/// <param name="dy">The amount to inflate vertically (top and bottom).</param>
	/// <returns>A new <see cref="Rect2"/> representing the inflated rectangle.</returns>
	public static Rect2 Inflate(in Rect2 r, float dx, float dy)
		=> new Rect2(
			r.X - dx,
			r.Y - dy,
			r.Width + dx * 2f,
			r.Height + dy * 2f
		);
	#endregion


	#region Offset
	/// <summary>
	/// Returns a new rectangle offset by the specified horizontal and vertical amounts.
	/// </summary>
	/// <param name="dx">The amount to offset along the X-axis.</param>
	/// <param name="dy">The amount to offset along the Y-axis.</param>
	/// <returns>A new <see cref="Rect2"/> representing the offset rectangle.</returns>
	public readonly Rect2 Offset(float dx, float dy) => Offset(this, dx, dy);

	/// <summary>
	/// Returns a new rectangle offset by the specified horizontal and vertical amounts.
	/// </summary>
	/// <param name="r">The rectangle to offset.</param>
	/// <param name="dx">The amount to offset along the X-axis.</param>
	/// <param name="dy">The amount to offset along the Y-axis.</param>
	/// <returns>A new <see cref="Rect2"/> representing the offset rectangle.</returns>
	public static Rect2 Offset(in Rect2 r, float dx, float dy)
		=> new(r.X + dx, r.Y + dy, r.Width, r.Height);

	/// <summary>
	/// Returns a new rectangle offset by the specified vector.
	/// </summary>
	/// <param name="delta">The vector specifying the offset along both axes.</param>
	/// <returns>A new <see cref="Rect2"/> representing the offset rectangle.</returns>
	public readonly Rect2 Offset(in Vect2 delta) => Offset(this, delta);

	/// <summary>
	/// Returns a new rectangle offset by the specified vector.
	/// </summary>
	/// <param name="r">The rectangle to offset.</param>
	/// <param name="delta">The vector specifying the offset along both axes.</param>
	/// <returns>A new <see cref="Rect2"/> representing the offset rectangle.</returns>
	public static Rect2 Offset(in Rect2 r, in Vect2 delta)
		=> new(r.X + delta.X, r.Y + delta.Y, r.Width, r.Height);
	#endregion


	#region Compare
	/// <summary>
	/// Compares this rectangle with another <see cref="Rect2"/> based on position.
	/// </summary>
	/// <param name="other">The rectangle to compare with.</param>
	/// <returns>
	/// A signed integer indicating the relative order:
	/// <list type="bullet">
	/// <item><description>Less than zero if this rectangle precedes <paramref name="other"/>.</description></item>
	/// <item><description>Zero if both rectangles have the same position.</description></item>
	/// <item><description>Greater than zero if this rectangle follows <paramref name="other"/>.</description></item>
	/// </list>
	/// Comparison is performed first on the X-coordinate, then on the Y-coordinate if X is equal.
	/// </returns>
	public readonly int CompareTo(Rect2 other)
	{
		int cmp = X.CompareTo(other.X);
		if (cmp != 0) return cmp;
		return Y.CompareTo(other.Y);
	}

	/// <summary>
	/// Compares two rectangles by their area.
	/// </summary>
	/// <param name="a">The first rectangle.</param>
	/// <param name="b">The second rectangle.</param>
	/// <returns>
	/// A signed integer indicating the relative order:
	/// <list type="bullet">
	/// <item><description>Less than zero if <paramref name="a"/> has a smaller area than <paramref name="b"/>.</description></item>
	/// <item><description>Zero if both rectangles have the same area.</description></item>
	/// <item><description>Greater than zero if <paramref name="a"/> has a larger area than <paramref name="b"/>.</description></item>
	/// </list>
	/// </returns>
	public static int CompareByArea(in Rect2 a, in Rect2 b)
		=> a.Area.CompareTo(b.Area);
	#endregion


	#region Enclose
	/// <summary>
	/// Expands this rectangle, if necessary, to enclose the specified point.
	/// </summary>
	/// <param name="point">The point to include within the rectangle.</param>
	/// <remarks>
	/// If the point lies outside the current bounds, the rectangle is resized so that
	/// the point is contained. Otherwise, the rectangle remains unchanged.
	/// </remarks>
	public void Enclose(in Vect2 point)
	{
		float minX = MathF.Min(Left, point.X);
		float minY = MathF.Min(Top, point.Y);
		float maxX = MathF.Max(Right, point.X);
		float maxY = MathF.Max(Bottom, point.Y);

		X = minX;
		Y = minY;
		Width = maxX - minX;
		Height = maxY - minY;
	}

	/// <summary>
	/// Returns a new rectangle that expands, if necessary, to enclose the specified point.
	/// </summary>
	/// <param name="r">The rectangle to expand.</param>
	/// <param name="point">The point to include within the rectangle.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> that contains both the original rectangle and the specified point.
	/// </returns>
	public static Rect2 Enclose(in Rect2 r, in Vect2 point)
	{
		float minX = MathF.Min(r.Left, point.X);
		float minY = MathF.Min(r.Top, point.Y);
		float maxX = MathF.Max(r.Right, point.X);
		float maxY = MathF.Max(r.Bottom, point.Y);

		return new Rect2(minX, minY, maxX - minX, maxY - minY);
	}
	#endregion


	#region FromCenter
	/// <summary>
	/// Creates a new rectangle centered at the specified point with the given size.
	/// </summary>
	/// <param name="center">The center point of the rectangle.</param>
	/// <param name="size">The size of the rectangle as a <see cref="Vect2"/>.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> positioned so that its center aligns with <paramref name="center"/>.
	/// </returns>
	public static Rect2 FromCenter(in Vect2 center, in Vect2 size)
	{
		float halfW = size.X * 0.5f;
		float halfH = size.Y * 0.5f;
		return new Rect2(center.X - halfW,
						 center.Y - halfH,
						 size.X,
						 size.Y);
	}

	/// <summary>
	/// Creates a new rectangle centered at the specified coordinates with the given dimensions.
	/// </summary>
	/// <param name="centerX">The X-coordinate of the rectangle's center.</param>
	/// <param name="centerY">The Y-coordinate of the rectangle's center.</param>
	/// <param name="width">The width of the rectangle.</param>
	/// <param name="height">The height of the rectangle.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> positioned so that its center aligns with the specified coordinates.
	/// </returns>
	public static Rect2 FromCenter(float centerX, float centerY, float width, float height)
	{
		float halfW = width * 0.5f;
		float halfH = height * 0.5f;
		return new Rect2(centerX - halfW,
						 centerY - halfH,
						 width,
						 height);
	}
	#endregion


	#region Clamp
	/// <summary>
	/// Clamps a point to the bounds of this rectangle.
	/// </summary>
	/// <param name="point">The point to clamp.</param>
	/// <returns>
	/// A new <see cref="Vect2"/> representing the point constrained within the rectangle.
	/// If the point lies outside, it is moved to the nearest edge.
	/// </returns>
	public readonly Vect2 ClampPoint(in Vect2 point)
	{
		float cx = MathF.Min(MathF.Max(point.X, Left), Right);
		float cy = MathF.Min(MathF.Max(point.Y, Top), Bottom);

		return new Vect2(cx, cy);
	}

	/// <summary>
	/// Crops this rectangle to fit within the specified bounds.
	/// </summary>
	/// <param name="bounds">The rectangle defining the cropping bounds.</param>
	/// <returns>
	/// A new <see cref="Rect2"/> representing the intersection of this rectangle with the bounds.
	/// If there is no overlap, <see cref="Rect2.Empty"/> is returned.
	/// </returns>
	public readonly Rect2 CropTo(in Rect2 bounds)
		=> Intersection(this, bounds);
	#endregion
}
