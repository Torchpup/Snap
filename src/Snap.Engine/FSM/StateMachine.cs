namespace Snap.Engine.FSM;

/// <summary>
/// A simple finite state machine that manages state transitions for an owner of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the object that owns and drives this state machine.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="StateMachine{T}"/> class.
/// </remarks>
/// <param name="owner">The object that owns and drives this state machine.</param>
public sealed class StateMachine<T>(T owner)
{
	// Holds a requested state change until the next Update cycle.
	private IState<T> _pendingState;

	/// <summary>
	/// Gets the instance of <typeparamref name="T"/> that this state machine controls.
	/// </summary>
	public T Owner { get; } = owner;

	/// <summary>
	/// Gets the currently active state. May be <c>null</c> if no state has been entered yet.
	/// </summary>
	public IState<T> Current { get; private set; }

	/// <summary>
	/// Requests a transition to a new state. The actual swap will occur at the start of the next <see cref="Update(float)"/> call.
	/// </summary>
	/// <param name="newState">The new state to transition to. Cannot be <c>null</c>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="newState"/> is <c>null</c>.</exception>
	public void ChangeState(IState<T> newState) =>
		_pendingState = newState ?? throw new ArgumentNullException(nameof(newState));

	/// <summary>
	/// Advances the state machine by one tick:  
	/// 1. Performs any pending state transition (calling <c>OnExit</c> on the old state and <c>OnEnter</c> on the new one).  
	/// 2. Invokes <c>Update</c> on the current state with the provided delta time.
	/// </summary>
	/// <param name="dt">Time elapsed since the last update, in seconds.</param>
	public void Update(float dt)
	{
		// Handle any pending transition
		if (_pendingState != null)
		{
			Current?.OnExit(Owner);

			var next = _pendingState;

			_pendingState = null;

			Current = next;
			Current.OnEnter(Owner);
		}

		// Now run the current state's Update
		Current?.Update(Owner, dt);
	}
}
