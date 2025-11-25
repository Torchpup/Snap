namespace Snap.Engine.Saves;

/// <summary>
/// Extends <see cref="BinaryWriter"/> to provide custom serialization for game-specific types.
/// </summary>
public sealed class ContentTypeWriter : BinaryWriter
{
	internal ContentTypeWriter(Stream stream) : base(stream) { }

	/// <summary>
	/// Writes a <see cref="Vect2"/> value to the current stream.
	/// </summary>
	/// <param name="value">The <see cref="Vect2"/> to write.</param>
	public void Write(Vect2 value)
	{
		Write(value.X);
		Write(value.Y);
	}

	/// <summary>
	/// Writes a <see cref="Rect2"/> value to the current stream.
	/// </summary>
	/// <param name="value">The <see cref="Rect2"/> to write.</param>
	public void Write(Rect2 value)
	{
		Write(value.X);
		Write(value.Y);
		Write(value.Width);
		Write(value.Height);
	}

	/// <summary>
	/// Writes a <see cref="Color"/> value to the current stream.
	/// </summary>
	/// <param name="value">The <see cref="Color"/> to write.</param>
	public void Write(Color value)
	{
		Write(value.R);
		Write(value.G);
		Write(value.B);
		Write(value.A);
	}

	/// <summary>
	/// Writes an enum value to the underlying stream as a 32 bit integer.
	/// </summary>
	/// <param name="value">
	/// The enum value to write. The enum's underlying value is converted to
	/// an Int32 before being written.
	/// </param>
	/// <remarks>
	/// This method does not preserve the enum's original underlying type.
	/// All enums are serialized as Int32 values for consistency.
	/// </remarks>
	public void WriteEnum(Enum value) =>
		Write(Convert.ToInt32(value));
	

	/// <summary>
	/// Writes an object of type <typeparamref name="T"/> to the current stream using XML serialization.
	/// </summary>
	/// <typeparam name="T">The type of the object to serialize.</typeparam>
	/// <param name="value">The object to write.</param>
	/// <remarks>
	/// The object is serialized to a byte array using <see cref="XmlSerializer"/>,
	/// and the length of the array is written before the array itself.
	/// </remarks>
	public void WriteObject<T>(T value)
	{
		using var ms = new MemoryStream();
		var serializer = new XmlSerializer(typeof(T));
		serializer.Serialize(ms, value);

		var bytes = ms.ToArray();

		Write(bytes.Length);
		Write(bytes);
	}
}
