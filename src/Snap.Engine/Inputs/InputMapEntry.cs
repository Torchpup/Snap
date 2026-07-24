namespace Snap.Engine.Inputs;

/// <summary>
/// Represents a single entry in an <see cref="InputMap"/>, storing a reference to an input action identifier.
/// </summary>
/// <remarks>
/// Typically used to associate a specific action (like <c>MoveLeft</c> or <c>Accept</c>) with one or more input bindings.
/// The underlying value is usually an enum such as <see cref="DefaultInputs"/> or a custom action identifier.
/// </remarks>
public class InputMapEntry
{
	private readonly object _value;

	/// <summary>
	/// Retrieves the stored value as the specified enum type.
	/// </summary>
	/// <typeparam name="T">The enum type to cast the value to.</typeparam>
	/// <returns>The stored value cast to <typeparamref name="T"/>.</returns>
	/// <exception cref="InvalidCastException">
	/// Thrown if the stored value is not compatible with <typeparamref name="T"/>.
	/// </exception>
	public T ValueAs<T>() where T : Enum => (T)_value;

	/// <summary>
	/// Initializes a new <see cref="InputMapEntry"/> with the specified action identifier.
	/// </summary>
	/// <param name="value">The enum value representing the input action associated with this entry.</param>
	internal InputMapEntry(object value) => _value = value;
}
