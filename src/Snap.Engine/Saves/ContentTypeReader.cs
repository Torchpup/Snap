namespace Snap.Engine.Saves;

/// <summary>
/// Extends <see cref="BinaryReader"/> to provide custom deserialization for game-specific types.
/// </summary>
public sealed class ContentTypeReader : BinaryReader
{
	internal ContentTypeReader(Stream stream) : base(stream) { }

	/// <summary>
	/// Reads a <see cref="Vect2"/> value from the current stream.
	/// </summary>
	/// <returns>The deserialized <see cref="Vect2"/>.</returns>
	public Vect2 ReadVect2()
	{
		float x = ReadSingle();
		float y = ReadSingle();
		return new Vect2(x, y);
	}

	/// <summary>
	/// Reads a <see cref="Rect2"/> value from the current stream.
	/// </summary>
	/// <returns>The deserialized <see cref="Rect2"/>.</returns>
	public Rect2 ReadRect2()
	{
		float x = ReadSingle();
		float y = ReadSingle();
		float w = ReadSingle();
		float h = ReadSingle();
		return new Rect2(x, y, w, h);
	}

	/// <summary>
	/// Reads a <see cref="Color"/> value from the current stream.
	/// </summary>
	/// <returns>The deserialized <see cref="Color"/>.</returns>
	public Color ReadColor()
	{
		byte r = ReadByte();
		byte g = ReadByte();
		byte b = ReadByte();
		byte a = ReadByte();
		return new Color(r, g, b, a);
	}


	/// <summary>
	/// Reads a 32-bit integer from the underlying stream and converts it to the specified enum type.
	/// </summary>
	/// <typeparam name="T">The type of enum to read.</typeparam>
	/// <returns>
	/// The enum value of type <typeparamref name="T"/> corresponding to the integer read from the stream.
	/// </returns>
	/// <remarks>
	/// This method assumes that all enums were written as Int32 values. 
	/// It does not preserve the original underlying type of the enum.
	/// </remarks>
	public T ReadEnum<T>() where T : Enum =>
		(T)Enum.ToObject(typeof(T), ReadInt32());
	
	

	/// <summary>
	/// Reads an object of type <typeparamref name="T"/> from the current stream using XML deserialization.
	/// </summary>
	/// <typeparam name="T">The type of the object to deserialize.</typeparam>
	/// <returns>The deserialized object.</returns>
	/// <remarks>
	/// The object is deserialized from a byte array previously written by <see cref="ContentTypeWriter.WriteObject{T}(T)"/>.
	/// The length of the array is read first, followed by the array itself.
	/// </remarks>
	public T ReadObject<T>()
	{
		var length = ReadInt32();
		var bytes = ReadBytes(length);

		using var ms = new MemoryStream(bytes);
		var serializer = new XmlSerializer(typeof(T));
		return (T)serializer.Deserialize(ms);
	}
}
