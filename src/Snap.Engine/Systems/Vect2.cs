namespace Snap.Engine.Systems;

/// <summary>
/// Represents a 2D vector using floating-point components (X, Y).
/// Supports common vector operations, arithmetic, and geometric transformations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Vect2"/> struct with the specified components.
/// </remarks>
/// <param name="x">The X component.</param>
/// <param name="y">The Y component.</param>
public struct Vect2(float x, float y) : IEquatable<Vect2>
{
	private const float Epsilon = 1e-6f;

	/// <summary>Gets or sets the X component of the vector.</summary>
	public float X = x;

	/// <summary>Gets or sets the Y component of the vector.</summary>
	public float Y = y;

	#region Properties
	/// <summary>Gets a vector with both components set to zero (0, 0).</summary>
	public static Vect2 Zero => new(0, 0);

	/// <summary>Gets a vector with both components set to one (1, 1).</summary>
	public static Vect2 One => new(1, 1);

	/// <summary>Gets a unit vector pointing upwards (0, -1).</summary>
	public static Vect2 Up => new(0, -1);

	/// <summary>Gets a unit vector pointing to the right (1, 0).</summary>
	public static Vect2 Right => new(1, 0);

	/// <summary>Gets a unit vector pointing downwards (0, 1).</summary>
	public static Vect2 Down => new(0, 1);

	/// <summary>Gets a unit vector pointing to the left (-1, 0).</summary>
	public static Vect2 Left => new(-1, 0);

	/// <summary>Gets a value indicating whether this vector is effectively zero, within a small epsilon.</summary>
	public readonly bool IsZero => MathF.Abs(X) < Epsilon && MathF.Abs(Y) < Epsilon;

	#endregion
	#region Constructors

	/// <summary>
	/// Initializes a new instance of the <see cref="Vect2"/> struct with both components set to the same value.
	/// </summary>
	/// <param name="value">The value for both X and Y components.</param>
	public Vect2(float value) : this(value, value) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Vect2"/> struct as a deep copy of another vector.
	/// </summary>
	/// <param name="value">The vector to copy.</param>
	public Vect2(Vect2 value) : this(value.X, value.Y) { }

	/// <summary>
	/// Deconstructs the vector into its X and Y components.
	/// </summary>
	/// <param name="x">Output for the X component.</param>
	/// <param name="y">Output for the Y component.</param>
	public readonly void Deconstruct(out float x, out float y)
	{
		x = X;
		y = Y;
	}
	#endregion


	#region Operator: ==, !=
	/// <summary>
	/// Determines whether two vectors are equal.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns><c>true</c> if the vectors are equal; otherwise, <c>false</c>.</returns>
	public static bool operator ==(in Vect2 a, in Vect2 b) => a.Equals(b);

	/// <summary>
	/// Determines whether two vectors are not equal.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns><c>true</c> if the vectors are not equal; otherwise, <c>false</c>.</returns>
	public static bool operator !=(in Vect2 a, in Vect2 b) => !a.Equals(b);
	#endregion


	#region Operator: +
	/// <summary>
	/// Adds two vectors component-wise.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator +(in Vect2 a, in Vect2 b) => new(a.X + b.X, a.Y + b.Y);

	/// <summary>
	/// Adds a scalar to each component of the vector.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <param name="b">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator +(in Vect2 a, float b) => new(a.X + b, a.Y + b);

	/// <summary>
	/// Adds a scalar to each component of the vector.
	/// </summary>
	/// <param name="a">The scalar.</param>
	/// <param name="b">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator +(float a, in Vect2 b) => new(a + b.X, a + b.Y);
	#endregion


	#region Operator: *
	/// <summary>
	/// Multiplies two vectors component-wise.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator *(in Vect2 a, in Vect2 b)
	{
		// Improves precision by avoiding extra rounding steps
		float x = MathF.FusedMultiplyAdd(a.X, b.X, 0f);
		float y = MathF.FusedMultiplyAdd(a.Y, b.Y, 0f);
		return new Vect2(x, y);
	}

	/// <summary>
	/// Multiplies a vector by a scalar.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <param name="b">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator *(in Vect2 a, float b)
	{
		float x = MathF.FusedMultiplyAdd(a.X, b, 0f);
		float y = MathF.FusedMultiplyAdd(a.Y, b, 0f);
		return new Vect2(x, y);
	}

	/// <summary>
	/// Multiplies a vector by a scalar.
	/// </summary>
	/// <param name="a">The scalar.</param>
	/// <param name="b">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator *(float a, in Vect2 b) => b * a;
	#endregion


	#region Operator: /
	/// <summary>
	/// Divides two vectors component-wise.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator /(in Vect2 a, in Vect2 b)
	{
		float x = MathF.Abs(b.X) > Epsilon ? a.X / b.X : 0f;
		float y = MathF.Abs(b.Y) > Epsilon ? a.Y / b.Y : 0f;
		return new Vect2(x, y);
	}

	/// <summary>
	/// Divides a vector by a scalar.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <param name="b">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator /(in Vect2 a, float b)
	{
		// Avoid division by values close to zero
		float safeB = MathF.Abs(b) > Epsilon ? b : 1f;
		return new Vect2(a.X / safeB, a.Y / safeB);
	}

	/// <summary>
	/// Divides a scalar by each component of the vector.
	/// </summary>
	/// <param name="a">The scalar.</param>
	/// <param name="b">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator /(float a, in Vect2 b)
	{
		float x = MathF.Abs(b.X) > Epsilon ? a / b.X : 0f;
		float y = MathF.Abs(b.Y) > Epsilon ? a / b.Y : 0f;
		return new Vect2(x, y);
	}
	#endregion


	#region Operator: -
	/// <summary>
	/// Negates the vector.
	/// </summary>
	/// <param name="value">The vector to negate.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator -(in Vect2 value) => new(-value.X, -value.Y);

	/// <summary>
	/// Subtracts two vectors component-wise.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator -(in Vect2 a, in Vect2 b) => new(a.X - b.X, a.Y - b.Y);

	/// <summary>
	/// Subtracts a scalar from each component of the vector.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <param name="b">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator -(in Vect2 a, float b) => new(a.X - b, a.Y - b);

	/// <summary>
	/// Subtracts each component of the vector from a scalar.
	/// </summary>
	/// <param name="a">The scalar.</param>
	/// <param name="b">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 operator -(float a, in Vect2 b) => new(a - b.X, a - b.Y);
	#endregion


	#region Operator: implicit
	/// <summary>
	/// Implicitly converts a <see cref="Vect2"/> to a <see cref="SFVectF"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator SFVectF(in Vect2 v) => new(v.X, v.Y);

	/// <summary>
	/// Implicitly converts a <see cref="Vect2"/> to a <see cref="SFVectI"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator SFVectI(in Vect2 v) => new((int)MathF.Round(v.X), (int)MathF.Round(v.Y));

	/// <summary>
	/// Implicitly converts a <see cref="Vect2"/> to a <see cref="SFVectU"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator SFVectU(in Vect2 v) => new((uint)MathF.Round(v.X), (uint)MathF.Round(v.Y));

	/// <summary>
	/// Implicitly converts a <see cref="SFVectF"/> to a <see cref="Vect2"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator Vect2(in SFVectF v) => new(v.X, v.Y);

	/// <summary>
	/// Implicitly converts a <see cref="SFVectI"/> to a <see cref="Vect2"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator Vect2(in SFVectI v) => new(v.X, v.Y);

	/// <summary>
	/// Implicitly converts a <see cref="SFVectU"/> to a <see cref="Vect2"/>.
	/// </summary>
	/// <param name="v">The vector to convert.</param>
	public static implicit operator Vect2(in SFVectU v) => new(v.X, v.Y);
	#endregion


	// public static explicit operator Vect2(in SFVectI v) => new(v.X, v.Y);


	#region IEquatable
	/// <summary>
	/// Determines whether this vector is equal to another vector, within a small epsilon.
	/// </summary>
	/// <param name="other">The vector to compare with.</param>
	/// <returns><c>true</c> if the vectors are equal; otherwise, <c>false</c>.</returns>
	public readonly bool Equals(Vect2 other) =>
		MathF.Abs(X - other.X) < Epsilon && MathF.Abs(Y - other.Y) < Epsilon;

	/// <summary>
	/// Determines whether this vector is equal to another object.
	/// </summary>
	/// <param name="obj">The object to compare with.</param>
	/// <returns><c>true</c> if the object is a vector and equal to this vector; otherwise, <c>false</c>.</returns>
	public override readonly bool Equals([NotNullWhen(true)] object obj) =>
		obj is Vect2 value && Equals(value);

	/// <summary>
	/// Gets the hash code for this vector.
	/// </summary>
	/// <returns>The hash code.</returns>
	public override readonly int GetHashCode() => HashCode.Combine(X, Y);

	/// <summary>
	/// Returns a string representation of this vector.
	/// </summary>
	/// <returns>A string in the format "Vect(X, Y)".</returns>

	public override readonly string ToString() => $"Vect({X}, {Y})";
	#endregion


	#region Length
	/// <summary>
	/// Gets the squared length of this vector.
	/// </summary>
	/// <returns>The squared length.</returns>
	public readonly float LengthSquared() => LengthSquared(this);

	/// <summary>
	/// Gets the squared length of the specified vector.
	/// </summary>
	/// <param name="value">The vector.</param>
	/// <returns>The squared length.</returns>
	public static float LengthSquared(in Vect2 value)
		=> value.X * value.X + value.Y * value.Y;

	/// <summary>
	/// Gets the length of this vector.
	/// </summary>
	/// <returns>The length.</returns>
	public readonly float Length() => Length(this);

	/// <summary>
	/// Gets the length of the specified vector.
	/// </summary>
	/// <param name="value">The vector.</param>
	/// <returns>The length.</returns>
	public static float Length(in Vect2 value)
		=> MathF.Sqrt(LengthSquared(value));
	#endregion


	#region NearlyEquals
	/// <summary>
	/// Determines whether this vector is nearly equal to another vector, within a specified epsilon.
	/// </summary>
	/// <param name="other">The vector to compare with.</param>
	/// <param name="epsilon">The maximum allowed difference for equality.</param>
	/// <returns><c>true</c> if the vectors are nearly equal; otherwise, <c>false</c>.</returns>
	public readonly bool NearlyEquals(in Vect2 other, float epsilon = Epsilon) =>
		NearlyEquals(this, other, epsilon);

	/// <summary>
	/// Determines whether two vectors are nearly equal, within a specified epsilon.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <param name="epsilon">The maximum allowed difference for equality.</param>
	/// <returns><c>true</c> if the vectors are nearly equal; otherwise, <c>false</c>.</returns>
	public static bool NearlyEquals(in Vect2 a, in Vect2 b, float epsilon = Epsilon) =>
		MathF.Abs(a.X - b.X) <= epsilon && MathF.Abs(a.Y - b.Y) <= epsilon;
	#endregion


	#region Sign
	/// <summary>
	/// Gets a vector whose components are the signs of this vector's components.
	/// </summary>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Sign() => Sign(this);

	/// <summary>
	/// Gets a vector whose components are the signs of the specified vector's components.
	/// </summary>
	/// <param name="value">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Sign(Vect2 value) =>
		new(MathF.Sign(value.X), MathF.Sign(value.Y));
	#endregion


	#region Normalize
	/// <summary>
	/// Returns a normalized copy of this vector.
	/// </summary>
	/// <returns>The normalized vector.</returns>
	public readonly Vect2 Normalize() => Normalize(this);

	/// <summary>
	/// Returns a normalized copy of the specified vector.
	/// </summary>
	/// <param name="value">The vector to normalize.</param>
	/// <returns>The normalized vector.</returns>
	public static Vect2 Normalize(in Vect2 value)
	{
		var len = Length(value);
		return len > 0f
			? new Vect2(value.X / len, value.Y / len)
			: new Vect2(0f, 0f);
	}
	#endregion


	#region RotateAround
	/// <summary>
	/// Rotates this vector around a pivot point by the specified angle (in radians).
	/// </summary>
	/// <param name="pivot">The pivot point.</param>
	/// <param name="radians">The angle in radians.</param>
	/// <returns>The rotated vector.</returns>
	public readonly Vect2 RotateAround(in Vect2 pivot, float radians) => RotateAround(this, pivot, radians);

	/// <summary>
	/// Rotates a vector around a pivot point by the specified angle (in radians).
	/// </summary>
	/// <param name="point">The vector to rotate.</param>
	/// <param name="pivot">The pivot point.</param>
	/// <param name="radians">The angle in radians.</param>
	/// <returns>The rotated vector.</returns>
	public static Vect2 RotateAround(in Vect2 point, in Vect2 pivot, float radians)
	{
		// Translate so pivot is origin
		float dx = point.X - pivot.X;
		float dy = point.Y - pivot.Y;
		float cos = MathF.Cos(radians);
		float sin = MathF.Sin(radians);
		float rx = dx * cos - dy * sin;
		float ry = dx * sin + dy * cos;
		// Translate back
		return new Vect2(rx + pivot.X, ry + pivot.Y);
	}
	#endregion



	#region Round
	/// <summary>
	/// Rounds both components of this vector to the nearest integer.
	/// </summary>
	/// <returns>A new <see cref="Vect2"/> with X and Y rounded to the nearest integer.</returns>
	public readonly Vect2 Round() => Round(this);

	/// <summary>
	/// Rounds both components of the given vector to the nearest integer.
	/// </summary>
	/// <param name="a">The vector to round.</param>
	/// <returns>A new <see cref="Vect2"/> with X and Y rounded to the nearest integer.</returns>
	public static Vect2 Round(in Vect2 a)
		=> new(MathF.Round(a.X), MathF.Round(a.Y));

	/// <summary>
	/// Rounds both components of this vector to the specified number of fractional digits.
	/// </summary>
	/// <param name="digits">
	/// The number of fractional digits (0–6).  
	/// 0 = round to integer, 1 = tenths, 2 = hundredths, etc.
	/// </param>
	/// <returns>A new <see cref="Vect2"/> with X and Y rounded to the given precision.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="digits"/> is less than 0 or greater than 6.
	/// </exception>
	public readonly Vect2 Round(int digits) => Round(this, digits);

	/// <summary>
	/// Rounds both components of the given vector to the specified number of fractional digits.
	/// </summary>
	/// <param name="a">The vector to round.</param>
	/// <param name="digits">
	/// The number of fractional digits (0–6).  
	/// 0 = round to integer, 1 = tenths, 2 = hundredths, etc.
	/// </param>
	/// <returns>A new <see cref="Vect2"/> with X and Y rounded to the given precision.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown if <paramref name="digits"/> is less than 0 or greater than 6.
	/// </exception>
	public static Vect2 Round(in Vect2 a, int digits)
	{
		return digits switch
		{
			< 0 or > 6 => throw new ArgumentOutOfRangeException(nameof(digits)),
			_ => new(MathF.Round(a.X, digits), MathF.Round(a.Y, digits))
		};
	}
	#endregion



	#region Min
	/// <summary>
	/// Returns a vector whose components are the minimum of this vector's and another vector's components.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Min(in Vect2 other) => Min(this, other);

	/// <summary>
	/// Returns a vector whose components are the minimum of two vectors' components.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Min(in Vect2 a, in Vect2 b)
		=> new(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
	#endregion


	#region Max
	/// <summary>
	/// Returns a vector whose components are the maximum of this vector's and another vector's components.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Max(in Vect2 other) => Max(this, other);

	/// <summary>
	/// Returns a vector whose components are the maximum of two vectors' components.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Max(in Vect2 a, in Vect2 b)
		=> new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
	#endregion


	#region Abs
	/// <summary>
	/// Returns a vector whose components are the absolute values of this vector's components.
	/// </summary>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Abs() => Abs(this);

	/// <summary>
	/// Returns a vector whose components are the absolute values of the specified vector's components.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Abs(in Vect2 a)
		=> new(MathF.Abs(a.X), MathF.Abs(a.Y));
	#endregion


	#region Floor
	/// <summary>
	/// Returns a vector whose components are the floor of this vector's components.
	/// </summary>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Floor() => Floor(this);

	/// <summary>
	/// Returns a vector whose components are the floor of the specified vector's components.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Floor(in Vect2 a)
		=> new(MathF.Floor(a.X), MathF.Floor(a.Y));
	#endregion


	#region Ceiling
	/// <summary>
	/// Returns a vector whose components are the ceiling of this vector's components.
	/// </summary>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Ceiling() => Ceiling(this);

	/// <summary>
	/// Returns a vector whose components are the ceiling of the specified vector's components.
	/// </summary>
	/// <param name="a">The vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Ceiling(in Vect2 a)
		=> new(MathF.Ceiling(a.X), MathF.Ceiling(a.Y));
	#endregion


	#region Clamp
	/// <summary>
	/// Clamps this vector's components between the components of two other vectors.
	/// </summary>
	/// <param name="min">The minimum vector.</param>
	/// <param name="max">The maximum vector.</param>
	/// <returns>The clamped vector.</returns>
	public readonly Vect2 Clamp(in Vect2 min, in Vect2 max) => Clamp(this, min, max);

	/// <summary>
	/// Clamps a vector's components between the components of two other vectors.
	/// </summary>
	/// <param name="value">The vector to clamp.</param>
	/// <param name="min">The minimum vector.</param>
	/// <param name="max">The maximum vector.</param>
	/// <returns>The clamped vector.</returns>
	public static Vect2 Clamp(in Vect2 value, in Vect2 min, in Vect2 max) =>
		new(
			MathF.Min(MathF.Max(value.X, min.X), max.X),
			MathF.Min(MathF.Max(value.Y, min.Y), max.Y)
		);
	#endregion


	#region Center
	/// <summary>
	/// Returns the center point between this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <param name="clamped">Whether to clamp the result to the range defined by the two vectors.</param>
	/// <returns>The center point.</returns>
	public readonly Vect2 Center(in Vect2 other, bool clamped) => Center(this, other, clamped);

	/// <summary>
	/// Returns the center point between two vectors.
	/// </summary>
	/// <param name="value">The first vector.</param>
	/// <param name="other">The second vector.</param>
	/// <param name="clamped">Whether to clamp the result to the range defined by the two vectors.</param>
	/// <returns>The center point.</returns>
	public static Vect2 Center(in Vect2 value, in Vect2 other, bool clamped) => new(
		MathHelpers.Center(value.X, other.X, clamped),
		MathHelpers.Center(value.Y, other.Y, clamped)
	);
	#endregion


	#region Direction
	/// <summary>
	/// Returns the direction from this vector to another vector.
	/// </summary>
	/// <param name="other">The target vector.</param>
	/// <returns>The normalized direction vector.</returns>
	public readonly Vect2 Direction(in Vect2 other) => Direction(this, other);

	/// <summary>
	/// Returns the direction from one vector to another.
	/// </summary>
	/// <param name="a">The source vector.</param>
	/// <param name="b">The target vector.</param>
	/// <returns>The normalized direction vector.</returns>
	public static Vect2 Direction(in Vect2 a, in Vect2 b) => (b - a).Normalize();
	#endregion


	#region Distance
	/// <summary>
	/// Gets the squared distance between this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The squared distance.</returns>
	public readonly float DistanceSquared(in Vect2 other) => DistanceSquared(this, other);

	/// <summary>
	/// Gets the squared distance between two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The squared distance.</returns>
	public static float DistanceSquared(in Vect2 a, in Vect2 b)
		=> (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

	/// <summary>
	/// Gets the distance between this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The distance.</returns>
	public readonly float Distance(in Vect2 other) => Distance(this, other);

	/// <summary>
	/// Gets the distance between two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The distance.</returns>
	public static float Distance(in Vect2 a, in Vect2 b)
		=> (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

	/// <summary>
	/// Gets the Manhattan (“L1”) distance between this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The Manhattan distance.</returns>
	public readonly float ManhattanDistance(in Vect2 other) => ManhattanDistance(this, other);

	/// <summary>
	/// Gets the Manhattan (“L1”) distance between two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The Manhattan distance.</returns>
	public static float ManhattanDistance(in Vect2 a, in Vect2 b)
		=> MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y);
	#endregion


	#region Dot
	/// <summary>
	/// Gets the dot product of this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The dot product.</returns>
	public readonly float Dot(in Vect2 other) => Dot(this, other);

	/// <summary>
	/// Gets the dot product of two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The dot product.</returns>
	public static float Dot(in Vect2 a, in Vect2 b) => a.X * b.X + a.Y * b.Y;
	#endregion


	#region Cross
	/// <summary>
	/// Gets the cross product of this vector and another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The cross product.</returns>
	public readonly float Cross(in Vect2 other) => Cross(this, other);

	/// <summary>
	/// Gets the cross product of two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The cross product.</returns>
	public static float Cross(in Vect2 a, in Vect2 b) => a.X * b.Y - a.Y * b.X;
	#endregion


	#region Add
	/// <summary>
	/// Adds this vector to another vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Add(in Vect2 other) => Add(this, other);

	/// <summary>
	/// Adds two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Add(in Vect2 a, in Vect2 b) => new(a.X + b.X, a.Y + b.Y);
	#endregion


	#region Subtract
	/// <summary>
	/// Subtracts another vector from this vector.
	/// </summary>
	/// <param name="other">The other vector.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Subtract(in Vect2 other) => Subtract(this, other);

	/// <summary>
	/// Subtracts two vectors.
	/// </summary>
	/// <param name="a">The first vector.</param>
	/// <param name="b">The second vector.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Subtract(in Vect2 a, in Vect2 b) => new(a.X - b.X, a.Y - b.Y);
	#endregion


	#region Multiply
	/// <summary>
	/// Multiplies this vector by a scalar.
	/// </summary>
	/// <param name="amount">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Multiply(float amount) => Multiply(this, amount);

	/// <summary>
	/// Multiplies a vector by a scalar.
	/// </summary>
	/// <param name="vector">The vector.</param>
	/// <param name="s">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Multiply(in Vect2 vector, float s) => new(vector.X * s, vector.Y * s);
	#endregion


	#region Divide
	/// <summary>
	/// Divides this vector by a scalar.
	/// </summary>
	/// <param name="amount">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 Divide(float amount) => Divide(this, amount);

	/// <summary>
	/// Divides a vector by a scalar.
	/// </summary>
	/// <param name="vector">The vector.</param>
	/// <param name="amount">The scalar.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 Divide(in Vect2 vector, float amount) => new(vector.X / amount, vector.Y / amount);
	#endregion


	#region MoveTowards
	/// <summary>
	/// Moves this vector towards a target vector by a maximum amount.
	/// </summary>
	/// <param name="target">The target vector.</param>
	/// <param name="maxDelta">The maximum distance to move.</param>
	/// <returns>The resulting vector.</returns>
	public readonly Vect2 MoveTowards(in Vect2 target, float maxDelta)
		=> MoveTowards(this, target, maxDelta);

	/// <summary>
	/// Moves a vector towards a target vector by a maximum amount.
	/// </summary>
	/// <param name="current">The current vector.</param>
	/// <param name="target">The target vector.</param>
	/// <param name="maxDelta">The maximum distance to move.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 MoveTowards(in Vect2 current, in Vect2 target, float maxDelta)
	{
		// Vector from current → target
		float dx = target.X - current.X;
		float dy = target.Y - current.Y;
		float distSq = dx * dx + dy * dy;
		if (distSq == 0f || maxDelta <= 0f)
			return current;

		float dist = MathF.Sqrt(distSq);
		if (dist <= maxDelta)
		{
			// Already within maxDelta; snap to target
			return target;
		}

		// Move along the direction by maxDelta
		float scale = maxDelta / dist;
		return new Vect2(
			current.X + dx * scale,
			current.Y + dy * scale
		);
	}
	#endregion


	#region Lerp
	/// <summary>
	/// Linearly interpolates between this vector and another vector using a precise method.
	/// </summary>
	/// <param name="other">The target vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public readonly Vect2 LerpPercise(in Vect2 other, float t) => LerpPercise(this, other, t);

	/// <summary>
	/// Linearly interpolates between two vectors using a precise method.
	/// </summary>
	/// <param name="a">The start vector.</param>
	/// <param name="b">The end vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public static Vect2 LerpPercise(Vect2 a, Vect2 b, float t)
	{
		return new Vect2(
			MathHelpers.LerpPercise(a.X, b.X, t),
			MathHelpers.LerpPercise(a.Y, b.Y, t)
		);
	}

	/// <summary>
	/// Linearly interpolates between this vector and another vector.
	/// </summary>
	/// <param name="other">The target vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public readonly Vect2 Lerp(Vect2 other, float t) => Lerp(this, other, t);

	/// <summary>
	/// Linearly interpolates between two vectors.
	/// </summary>
	/// <param name="a">The start vector.</param>
	/// <param name="b">The end vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public static Vect2 Lerp(Vect2 a, Vect2 b, float t)
		=> new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

	/// <summary>
	/// Smoothly interpolates between this vector and a target vector using a smoothstep function.
	/// </summary>
	/// <param name="target">The target vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public readonly Vect2 SmoothStep(in Vect2 target, float t)
		=> SmoothStep(this, target, t);

	/// <summary>
	/// Smoothly interpolates between two vectors using a smoothstep function.
	/// </summary>
	/// <param name="a">The start vector.</param>
	/// <param name="b">The end vector.</param>
	/// <param name="t">The interpolation factor (0-1).</param>
	/// <returns>The interpolated vector.</returns>
	public static Vect2 SmoothStep(Vect2 a, Vect2 b, float t)
	{
		// clamp t to [0,1]
		t = Math.Clamp(t, 0f, 1f);

		// smooth‐step Hermite: 3t² – 2t³  (ease‐in/ease‐out)
		float tt = t * t * (3f - 2f * t);

		return new Vect2(
			a.X + (b.X - a.X) * tt,
			a.Y + (b.Y - a.Y) * tt
		);
	}
	#endregion


	#region Reflect
	/// <summary>
	/// Reflects a vector off a surface defined by a normal.
	/// </summary>
	/// <param name="v">The incoming vector.</param>
	/// <param name="normal">The surface normal.</param>
	/// <returns>The reflected vector.</returns>
	public static Vect2 Reflect(in Vect2 v, in Vect2 normal)
	{
		var dot = v.Dot(normal);
		return new Vect2(
			v.X - 2f * dot * normal.X,
			v.Y - 2f * dot * normal.Y
		);
	}
	#endregion


	#region Angle & Rotation
	/// <summary>
	/// Gets the angle (in radians) of this vector relative to the positive X-axis.
	/// </summary>
	/// <returns>The angle in radians.</returns>
	public readonly float Angle() => Angle(this);

	/// <summary>
	/// Gets the angle (in radians) of a vector relative to the positive X-axis.
	/// </summary>
	/// <param name="value">The vector.</param>
	/// <returns>The angle in radians.</returns>
	public static float Angle(Vect2 value) => MathF.Atan2(value.Y, value.X);

	/// <summary>
	/// Creates a unit vector from an angle (in radians).
	/// </summary>
	/// <param name="radians">The angle in radians.</param>
	/// <returns>The resulting vector.</returns>
	public static Vect2 FromAngle(float radians) => new(MathF.Cos(radians), MathF.Sin(radians));

	/// <summary>
	/// Rotates this vector by the specified angle (in radians).
	/// </summary>
	/// <param name="radians">The angle in radians.</param>
	public void Rotate(float radians)
	{
		var cos = MathF.Cos(radians);
		var sin = MathF.Sin(radians);
		var newX = X * cos - Y * sin;
		var newY = X * sin + Y * cos;

		X = newX;
		Y = newY;
	}

	/// <summary>
	/// Returns a new vector rotated by the specified angle (in radians).
	/// </summary>
	/// <param name="radians">The angle in radians.</param>
	/// <returns>The rotated vector.</returns>
	public readonly Vect2 Rotated(float radians)
	{
		var cos = MathF.Cos(radians);
		var sin = MathF.Sin(radians);

		return new Vect2(X * cos - Y * sin, X * sin + Y * cos);
	}
	#endregion
}
