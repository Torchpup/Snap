namespace Snap.Engine.Systems;

/// <summary>
/// Represents a color using RGBA components, with each component ranging from 0 to 255.
/// </summary>
public struct Color : IEquatable<Color>
{
	/// <summary>
	/// Gets a predefined white color (255, 255, 255, 255).
	/// </summary>
	public static Color White => new(255, 255, 255);

	/// <summary>
	/// Gets a predefined cornflower blue color (100, 149, 237, 255).
	/// </summary>
	public static Color CornFlowerBlue => new(100, 149, 237);

	/// <summary>
	/// Gets a predefined black color (0, 0, 0, 255).
	/// </summary>
	public static Color Black => new(0, 0, 0);

	/// <summary>
	/// Gets a predefined red color (255, 0, 0, 255).
	/// </summary>
	public static Color Red => new(255, 0, 0);

	/// <summary>
	/// Gets a predefined green color (0, 255, 0, 255).
	/// </summary>
	public static Color Green => new(0, 255, 0);

	/// <summary>
	/// Gets a predefined blue color (0, 0, 255, 255).
	/// </summary>
	public static Color Blue => new(0, 0, 255);

	/// <summary>
	/// Gets a predefined yellow color (255, 255, 0, 255).
	/// </summary>
	public static Color Yellow => new(255, 255, 0);

	/// <summary>
	/// Gets a predefined cyan color (0, 255, 255, 255).
	/// </summary>
	public static Color Cyan => new(0, 255, 255);

	/// <summary>
	/// Gets a predefined magenta color (255, 0, 255, 255).
	/// </summary>
	public static Color Magenta => new(255, 0, 255);

	/// <summary>
	/// Gets a fully transparent color (0, 0, 0, 0).
	/// </summary>
	public static Color Transparent => new(0, 0, 0, 0);

	/// <summary>
	/// Gets or sets the red component of the color (0-255).
	/// </summary>
	public byte R;

	/// <summary>
	/// Gets or sets the green component of the color (0-255).
	/// </summary>
	public byte G;

	/// <summary>
	/// Gets or sets the blue component of the color (0-255).
	/// </summary>
	public byte B;

	/// <summary>
	/// Gets or sets the alpha (transparency) component of the color (0-255).
	/// </summary>
	public byte A;

	/// <summary>
	/// Initializes a new instance of the <see cref="Color"/> struct with the specified RGBA components.
	/// </summary>
	/// <param name="r">The red component (0-255).</param>
	/// <param name="g">The green component (0-255).</param>
	/// <param name="b">The blue component (0-255).</param>
	/// <param name="a">The alpha component (0-255).</param>
	public Color(byte r, byte g, byte b, byte a)
	{
		R = r;
		G = g;
		B = b;
		A = a;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Color"/> struct with the specified RGB components and full opacity.
	/// </summary>
	/// <param name="r">The red component (0-255).</param>
	/// <param name="g">The green component (0-255).</param>
	/// <param name="b">The blue component (0-255).</param>
	public Color(byte r, byte g, byte b) : this(r, g, b, 255) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Color"/> struct with the specified RGBA components as floats (0.0-1.0).
	/// </summary>
	/// <param name="r">The red component (0.0-1.0).</param>
	/// <param name="g">The green component (0.0-1.0).</param>
	/// <param name="b">The blue component (0.0-1.0).</param>
	/// <param name="a">The alpha component (0.0-1.0).</param>
	public Color(float r, float g, float b, float a)
		: this(
			(byte)Math.Clamp(r * 255f, 0f, 255f),
			(byte)Math.Clamp(g * 255f, 0f, 255f),
			(byte)Math.Clamp(b * 255f, 0f, 255f),
			(byte)Math.Clamp(a * 255f, 0f, 255f)
		)
	{ }

	/// <summary>
	/// Initializes a new instance of the <see cref="Color"/> struct with the specified RGB components as floats (0.0-1.0) and full opacity.
	/// </summary>
	/// <param name="r">The red component (0.0-1.0).</param>
	/// <param name="g">The green component (0.0-1.0).</param>
	/// <param name="b">The blue component (0.0-1.0).</param>
	public Color(float r, float g, float b)
		: this(r, g, b, 1.0f) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="Color"/> struct from a hexadecimal string.
	/// </summary>
	/// <param name="hex">The hexadecimal string representing the color (e.g., "#RRGGBB" or "#RRGGBBAA").</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="hex"/> is null or whitespace.</exception>
	/// <exception cref="Exception">Thrown if the hexadecimal string is not in a valid format.</exception>
	public Color(string hex)
	{
		if (string.IsNullOrWhiteSpace(hex))
			throw new ArgumentNullException(nameof(hex));

		var r = hex.TrimStart('#');

		if (r.Length == 8)
		{
			A = (byte)int.Parse(r.Substring(0, 2), NumberStyles.HexNumber);
			R = (byte)int.Parse(r.Substring(2, 2), NumberStyles.HexNumber);
			G = (byte)int.Parse(r.Substring(4, 2), NumberStyles.HexNumber);
			B = (byte)int.Parse(r.Substring(6, 2), NumberStyles.HexNumber);
		}
		else if (r.Length == 6)
		{
			R = (byte)int.Parse(r.Substring(0, 2), NumberStyles.HexNumber);
			G = (byte)int.Parse(r.Substring(2, 2), NumberStyles.HexNumber);
			B = (byte)int.Parse(r.Substring(4, 2), NumberStyles.HexNumber);
			A = 255;
		}
		else if (r.Length == 3)
		{
			R = (byte)int.Parse(r.Substring(0, 1), NumberStyles.HexNumber);
			G = (byte)int.Parse(r.Substring(1, 1), NumberStyles.HexNumber);
			B = (byte)int.Parse(r.Substring(2, 1), NumberStyles.HexNumber);
			A = 255;
		}
		else
		{
			throw new Exception();
		}
	}


	#region Operator: ==, !=
	/// <summary>
	/// Determines whether two <see cref="Color"/> instances are equal.
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns><c>true</c> if the colors are equal; otherwise, <c>false</c>.</returns>
	public static bool operator ==(in Color a, in Color b) => a.Equals(b);

	/// <summary>
	/// Determines whether two <see cref="Color"/> instances are not equal.
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns><c>true</c> if the colors are not equal; otherwise, <c>false</c>.</returns>
	public static bool operator !=(in Color a, in Color b) => !a.Equals(b);
	#endregion


	#region Operator: +
	/// <summary>
	/// Adds two colors together, clamping each component to the valid range (0-255).
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the addition.</returns>
	public static Color operator +(in Color a, in Color b) => new(
			(byte)Math.Clamp(a.R + b.R, 0, 255),
			(byte)Math.Clamp(a.G + b.G, 0, 255),
			(byte)Math.Clamp(a.B + b.B, 0, 255),
			(byte)Math.Clamp(a.A + b.A, 0, 255)
		);

	/// <summary>
	/// Adds a float value to each component of the color, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The color.</param>
	/// <param name="b">The float value to add.</param>
	/// <returns>A new <see cref="Color"/> resulting from the addition.</returns>
	public static Color operator +(in Color a, float b) => new(
		(byte)Math.Clamp(a.R + b, 0f, 255f),
		(byte)Math.Clamp(a.G + b, 0f, 255f),
		(byte)Math.Clamp(a.B + b, 0f, 255f),
		(byte)Math.Clamp(a.A + b, 0f, 255f)
	);

	/// <summary>
	/// Adds a float value to each component of the color, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The float value to add.</param>
	/// <param name="b">The color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the addition.</returns>
	public static Color operator +(float a, in Color b) => new(
		(byte)Math.Clamp(b.R + a, 0f, 255f),
		(byte)Math.Clamp(b.G + a, 0f, 255f),
		(byte)Math.Clamp(b.B + a, 0f, 255f),
		(byte)Math.Clamp(b.A + a, 0f, 255f)
	);
	#endregion


	#region Operator: *
	/// <summary>
	/// Multiplies two colors together, clamping each component to the valid range (0-255).
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the multiplication.</returns>
	public static Color operator *(in Color a, in Color b) => new(
			(byte)Math.Clamp(a.R * b.R, 0, 255),
			(byte)Math.Clamp(a.G * b.G, 0, 255),
			(byte)Math.Clamp(a.B * b.B, 0, 255),
			(byte)Math.Clamp(a.A * b.A, 0, 255)
		);

	/// <summary>
	/// Multiplies each component of the color by a float value, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The color.</param>
	/// <param name="b">The float value to multiply by.</param>
	/// <returns>A new <see cref="Color"/> resulting from the multiplication.</returns>
	public static Color operator *(Color a, float b) => new(
		(byte)Math.Clamp(a.R * b, 0f, 255f),
		(byte)Math.Clamp(a.G * b, 0f, 255f),
		(byte)Math.Clamp(a.B * b, 0f, 255f),
		(byte)Math.Clamp(a.A * b, 0f, 255f)
	);

	/// <summary>
	/// Multiplies each component of the color by a float value, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The float value to multiply by.</param>
	/// <param name="b">The color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the multiplication.</returns>
	public static Color operator *(float a, in Color b) => new(
		(byte)Math.Clamp(b.R * a, 0f, 255f),
		(byte)Math.Clamp(b.G * a, 0f, 255f),
		(byte)Math.Clamp(b.B * a, 0f, 255f),
		(byte)Math.Clamp(b.A * a, 0f, 255f)
	);
	#endregion


	#region Operator: /
	/// <summary>
	/// Divides two colors, clamping each component to the valid range (0-255).
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the division.</returns>
	public static Color operator /(in Color a, in Color b) => new(
			(byte)Math.Clamp(a.R / b.R, 0, 255),
			(byte)Math.Clamp(a.G / b.G, 0, 255),
			(byte)Math.Clamp(a.B / b.B, 0, 255),
			(byte)Math.Clamp(a.A / b.A, 0, 255)
		);

	/// <summary>
	/// Divides each component of the color by a float value, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The color.</param>
	/// <param name="b">The float value to divide by.</param>
	/// <returns>A new <see cref="Color"/> resulting from the division.</returns>
	public static Color operator /(in Color a, float b) => new(
		(byte)Math.Clamp(a.R / b, 0f, 255f),
		(byte)Math.Clamp(a.G / b, 0f, 255f),
		(byte)Math.Clamp(a.B / b, 0f, 255f),
		(byte)Math.Clamp(a.A / b, 0f, 255f)
	);

	/// <summary>
	/// Divides a float value by each component of the color, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The float value to divide.</param>
	/// <param name="b">The color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the division.</returns>
	public static Color operator /(float a, in Color b) => new(
		(byte)Math.Clamp(b.R / a, 0f, 255f),
		(byte)Math.Clamp(b.G / a, 0f, 255f),
		(byte)Math.Clamp(b.B / a, 0f, 255f),
		(byte)Math.Clamp(b.A / a, 0f, 255f)
	);
	#endregion


	#region Operator: -
	/// <summary>
	/// Subtracts two colors, clamping each component to the valid range (0-255).
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the subtraction.</returns>
	public static Color operator -(in Color a, in Color b) => new(
			(byte)Math.Clamp(a.R - b.R, 0, 255),
			(byte)Math.Clamp(a.G - b.G, 0, 255),
			(byte)Math.Clamp(a.B - b.B, 0, 255),
			(byte)Math.Clamp(a.A - b.A, 0, 255)
		);

	/// <summary>
	/// Subtracts a float value from each component of the color, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The color.</param>
	/// <param name="b">The float value to subtract.</param>
	/// <returns>A new <see cref="Color"/> resulting from the subtraction.</returns>
	public static Color operator -(in Color a, float b) => new(
		(byte)Math.Clamp(a.R - b, 0f, 255f),
		(byte)Math.Clamp(a.G - b, 0f, 255f),
		(byte)Math.Clamp(a.B - b, 0f, 255f),
		(byte)Math.Clamp(a.A - b, 0f, 255f)
	);

	/// <summary>
	/// Subtracts each component of the color from a float value, clamping the result to the valid range (0-255).
	/// </summary>
	/// <param name="a">The float value to subtract from.</param>
	/// <param name="b">The color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the subtraction.</returns>
	public static Color operator -(float a, in Color b) => new(
		(byte)Math.Clamp(b.R - a, 0f, 255f),
		(byte)Math.Clamp(b.G - a, 0f, 255f),
		(byte)Math.Clamp(b.B - a, 0f, 255f),
		(byte)Math.Clamp(b.A - a, 0f, 255f)
	);
	#endregion


	#region Blend
	/// <summary>
	/// Blends this color with another color by a specified factor.
	/// </summary>
	/// <param name="other">The color to blend with.</param>
	/// <param name="t">The blend factor (0.0-1.0).</param>
	/// <returns>A new <see cref="Color"/> resulting from the blend.</returns>
	public readonly Color Blend(in Color other, float t) => Blend(this, other, t);

	/// <summary>
	/// Blends two colors by a specified factor.
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <param name="t">The blend factor (0.0-1.0).</param>
	/// <returns>A new <see cref="Color"/> resulting from the blend.</returns>
	public static Color Blend(in Color a, in Color b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);

		return new Color(
			(byte)Math.Clamp(a.R + (b.R - a.R) * t, 0f, 255f),
			(byte)Math.Clamp(a.G + (b.G - a.G) * t, 0f, 255f),
			(byte)Math.Clamp(a.B + (b.B - a.B) * t, 0f, 255f),
			(byte)Math.Clamp(a.A + (b.A - a.A) * t, 0f, 255f)
		);
	}
	#endregion


	#region Multiply
	/// <summary>
	/// Multiplies this color with another color, component-wise.
	/// </summary>
	/// <param name="other">The color to multiply with.</param>
	/// <returns>A new <see cref="Color"/> resulting from the multiplication.</returns>
	public readonly Color Multiply(in Color other) => Multiply(this, other);

	/// <summary>
	/// Multiplies two colors, component-wise.
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the multiplication.</returns>
	public static Color Multiply(in Color a, in Color b)
	{
		return new Color(
			(byte)(a.R / 255f * (b.R / 255f) * 255f),
			(byte)(a.G / 255f * (b.G / 255f) * 255f),
			(byte)(a.B / 255f * (b.B / 255f) * 255f),
			(byte)(a.A / 255f * (b.A / 255f) * 255f)
		);
	}
	#endregion


	#region Average
	/// <summary>
	/// Averages this color with another color.
	/// </summary>
	/// <param name="other">The color to average with.</param>
	/// <returns>A new <see cref="Color"/> resulting from the average.</returns>
	public readonly Color Average(in Color other) => Average(this, other);

	/// <summary>
	/// Averages two colors.
	/// </summary>
	/// <param name="a">The first color.</param>
	/// <param name="b">The second color.</param>
	/// <returns>A new <see cref="Color"/> resulting from the average.</returns>
	public static Color Average(in Color a, in Color b)
	{
		return new Color(
			(byte)((a.R + b.R) / 2),
			(byte)((a.G + b.G) / 2),
			(byte)((a.B + b.B) / 2),
			(byte)((a.A + b.A) / 2)
		);
	}
	#endregion


	#region WithAlpha
	/// <summary>
	/// Returns a new <see cref="Color"/> with the same RGB values as this color,
	/// but with the alpha channel replaced by the specified value.
	/// </summary>
	/// <param name="alpha">
	/// The new alpha value, expressed as a normalized float in the range 0.0–1.0.
	/// Values outside this range are clamped.
	/// </param>
	/// <returns>
	/// A new <see cref="Color"/> whose alpha component is set to <paramref name="alpha"/>.
	/// </returns>
	public readonly Color WithAlpha(float alpha) => WithAlpha(this, alpha);

	/// <summary>
	/// Creates a new <see cref="Color"/> using the RGB components of the given color,
	/// but with the alpha channel replaced by the specified value.
	/// </summary>
	/// <param name="color">The source color whose RGB values will be preserved.</param>
	/// <param name="alpha">
	/// The new alpha value, expressed as a normalized float in the range 0.0–1.0.
	/// Values outside this range are clamped.
	/// </param>
	/// <returns>
	/// A new <see cref="Color"/> with the same RGB values as <paramref name="color"/>,
	/// but with its alpha component set to <paramref name="alpha"/>.
	/// </returns>
	public  static Color WithAlpha(Color color, float alpha) =>
		new(color.R, color.B, color.G, (byte)Math.Clamp(alpha * 255f, 0f, 255f));
	#endregion


	#region implicit Operators
	/// <summary>
	/// Implicitly converts a <see cref="Color"/> to an SFColor.
	/// </summary>
	/// <param name="a">The color to convert.</param>
	public static implicit operator SFColor(in Color a) => new(a.R, a.G, a.B, a.A);
	#endregion


	#region IEquatable
	/// <summary>
	/// Determines whether this color is equal to another color.
	/// </summary>
	/// <param name="other">The color to compare with.</param>
	/// <returns><c>true</c> if the colors are equal; otherwise, <c>false</c>.</returns>
	public readonly bool Equals(Color other) =>
			R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

	/// <summary>
	/// Determines whether this color is equal to another object.
	/// </summary>
	/// <param name="obj">The object to compare with.</param>
	/// <returns><c>true</c> if the object is a color and equal to this color; otherwise, <c>false</c>.</returns>
	public readonly override bool Equals([NotNullWhen(true)] object obj) =>
		obj is Color value && Equals(value);

	/// <summary>
	/// Gets the hash code for this color.
	/// </summary>
	/// <returns>The hash code.</returns>
	public readonly override int GetHashCode() => HashCode.Combine(R, G, B, A);

	/// <summary>
	/// Returns a string representation of this color.
	/// </summary>
	/// <returns>A string in the format "Color(R,G,B,A)".</returns>
	public readonly override string ToString() => $"Color({R},{G},{B},{A})";
	#endregion
}
