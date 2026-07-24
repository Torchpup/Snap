namespace Snap.Engine.Inputs;

/// <summary>
/// Represents the current active input device type.
/// Used for resolving input priority and adapting UI prompts (e.g., showing gamepad vs keyboard icons).
/// </summary>
public enum ActiveInput
{
	/// <summary>Indicates that keyboard or mouse input is currently active.</summary>
	Keyboard,

	/// <summary>Indicates that gamepad input is currently active.</summary>
	Gamepad
}

/// <summary>
/// Represents a mapping between high-level input actions and their bound keyboard, mouse, and gamepad inputs.
/// Used to track input state, query actions, and manage device-specific bindings.
/// </summary>
/// <remarks>
/// Input maps define how raw device input (e.g., buttons or keys) is associated with gameplay actions.
/// Developers can create custom maps or use <see cref="DefaultInputMap"/> as a fallback.
/// </remarks>
public class InputMap
{
	private static readonly KeyboardButton[] AllKeyboardButtons = Enum.GetValues<KeyboardButton>();
	private static readonly MouseButton[] AllMouseButtons = Enum.GetValues<MouseButton>();
	private static readonly GamepadButton[] AllGamepadButtons = Enum.GetValues<GamepadButton>();

	private readonly uint _joyCount;
	private readonly Dictionary<uint, bool> _joysticks = [];
	private readonly List<SdlControllerEntry> _allEntries = [];
	private readonly Dictionary<uint, Dictionary<char, int>> _controllerMaps = [];

	internal readonly Dictionary<uint, List<InputMapEntry>> Actions = new(20);

	/// <summary>
	/// Gets the currently active input device type (keyboard/mouse or gamepad).
	/// </summary>
	/// <remarks>
	/// This value is automatically updated based on the last input event detected,
	/// allowing the engine to adapt UI or control schemes accordingly.
	/// </remarks>
	public ActiveInput Current { get; private set; }

	/// <summary>
	/// Gets the current mouse position relative to the game window or rendering surface.
	/// </summary>
	public Vect2 MousePosition => SFMouse.GetPosition(Game.Instance.ToRenderer);

	/// <summary>
	/// Gets the current mouse position in global screen coordinates.
	/// </summary>
	public Vect2 GlobalMousePosition => SFMouse.GetPosition();

	#region Constructor
	/// <summary>
	/// Initializes a new <see cref="InputMap"/> and detects connected input devices.
	/// </summary>
	public InputMap()
	{
		SFJoystick.Update();

		_joyCount = SFJoystick.Count;
		_joysticks = new Dictionary<uint, bool>((int)_joyCount);

		for (uint i = 0; i < _joyCount; i++)
		{
			_joysticks[i] = SFJoystick.IsConnected(i);
		}

		_allEntries = SdlControllerDbParser.LoadAll();
	}
	#endregion


	/// <summary>
	/// Checks whether any input button is currently being held down across all input devices.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one keyboard key, mouse button, or gamepad button is currently pressed;
	/// otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method aggregates the pressed state from all connected input devices.
	/// It is useful for detecting when the user is providing any input at all,
	/// such as for idle timeout detection or input-sensitive UI effects.
	/// </remarks>
	public bool AnyInputPressed() =>
		AnyKeyboardButtonPressed() ||
		AnyMouseButtonPressed() ||
		AnyGamepadButtonPressed();

	/// <summary>
	/// Checks whether any input button was just pressed (transitioned from not pressed to pressed)
	/// in the current frame across all input devices.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one keyboard key, mouse button, or gamepad button was pressed
	/// in the current frame; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method detects the initial press frame for any input across all devices.
	/// It is useful for detecting the first moment of user interaction,
	/// such as to dismiss a splash screen or capture initial focus.
	/// </remarks>
	public bool AnyInputJustPressed() =>
		AnyKeyboardButtonJustPressed() ||
		AnyMouseButtonJustPressed() ||
		AnyGamepadButtonJustPressed();



	#region Keyboard
	/// <summary>
	/// Checks whether any keyboard key is currently being held down.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one keyboard key is currently pressed; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="KeyboardButton"/> enumeration
	/// and checks for the pressed state of each key. It returns <c>true</c> on the first
	/// key found in the pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting continuous keyboard input or when the user
	/// is holding down any key, such as for input-sensitive UI effects or idle detection.
	/// </para>
	/// </remarks>
	public bool AnyKeyboardButtonPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllKeyboardButtons)
		{
			if (!IsKeyPressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks whether any keyboard key was just pressed (transitioned from not pressed to pressed) in the current frame.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one keyboard key was pressed in the current frame; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="KeyboardButton"/> enumeration
	/// and checks for the just-pressed state of each key. It returns <c>true</c> on the first
	/// key found in the just-pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting the initial moment of any keyboard interaction,
	/// such as waking up from a paused state or capturing initial keyboard focus.
	/// </para>
	/// </remarks>
	public bool AnyKeyboardButtonJustPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllKeyboardButtons)
		{
			if (!IsKeyJustPressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if the specified keyboard key is currently being held down.
	/// </summary>
	/// <param name="button">The keyboard key to check.</param>
	/// <returns><c>true</c> if the key is pressed; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// Will return <c>false</c> if the game window is not focused. If a key is pressed, 
	/// the active input mode is switched to <see cref="ActiveInput.Keyboard"/>.
	/// </remarks>
	public bool IsKeyPressed(KeyboardButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		var result = SFKeyboard.IsKeyPressed((SFKey)button);

		if (result)
			Current = ActiveInput.Keyboard;

		return result;
	}

	/// <summary>
	/// Checks if the specified keyboard key is currently released (not held down).
	/// </summary>
	/// <param name="button">The keyboard key to check.</param>
	/// <returns><c>true</c> if the key is not pressed; otherwise, <c>false</c>.</returns>
	public bool IsKeyReleased(KeyboardButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		return !IsKeyPressed(button);
	}

	/// <summary>
	/// Checks if the specified keyboard key was just pressed once (debounced).
	/// </summary>
	/// <param name="button">The keyboard key to check.</param>
	/// <returns><c>true</c> if the key was just pressed and hasn't been acknowledged yet; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This is a one-time trigger per press cycle, meant to avoid multiple detections
	/// in a single frame or input loop. Internally resets after detection.
	/// </remarks>
	public bool IsKeyJustPressed(KeyboardButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		if (_keysJustPressed.Contains(button))
			return false; // Already handled this press

		if (IsKeyPressed(button))
		{
			// _keyJustPressed = true;
			_keysJustPressed.Add(button);

			return true;
		}

		return false;
	}
	#endregion


	#region Mouse
	/// <summary>
	/// Checks whether any mouse button is currently being held down.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one mouse button is currently pressed; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="MouseButton"/> enumeration
	/// and checks for the pressed state of each button. It returns <c>true</c> on the first
	/// button found in the pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting continuous mouse input, such as during drag operations
	/// or when checking for any mouse interaction.
	/// </para>
	/// </remarks>
	public bool AnyMouseButtonPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllMouseButtons)
		{
			if (!IsMousePressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks whether any mouse button was just pressed (transitioned from not pressed to pressed) in the current frame.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one mouse button was pressed in the current frame; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="MouseButton"/> enumeration
	/// and checks for the just-pressed state of each button. It returns <c>true</c> on the first
	/// button found in the just-pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting the initial click of any mouse button,
	/// such as for focus capture or initial interaction detection.
	/// </para>
	/// </remarks>
	public bool AnyMouseButtonJustPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllMouseButtons)
		{
			if (!IsMouseJustPressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if the specified mouse button is currently being held down.
	/// </summary>
	/// <param name="button">The mouse button to check.</param>
	/// <returns><c>true</c> if the button is pressed; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// Returns <c>false</c> if the game window is not currently active.
	/// </remarks>
	public bool IsMousePressed(MouseButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		return SFMouse.IsButtonPressed((SFMouseButton)button);
	}

	/// <summary>
	/// Checks if the specified mouse button is currently released (not held down).
	/// </summary>
	/// <param name="button">The mouse button to check.</param>
	/// <returns><c>true</c> if the button is not pressed; otherwise, <c>false</c>.</returns>
	public bool IsMouseReleased(MouseButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		return !IsMousePressed(button);
	}

	/// <summary>
	/// Checks if the specified mouse button was just pressed once (debounced).
	/// </summary>
	/// <param name="button">The mouse button to check.</param>
	/// <returns><c>true</c> if the button was just pressed and not yet acknowledged; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This is a one-time detection meant to prevent multiple triggers during a single click.  
	/// Internally resets after the first detection until released.
	/// </remarks>
	public bool IsMouseJustPressed(MouseButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		if (_mouseJustPressed.Contains(button))
			return false;

		if (IsMousePressed(button))
		{
			_mouseJustPressed.Add(button);
			return true;
		}

		return false;
	}
	#endregion


	#region Gamepad
	/// <summary>
	/// Checks whether any gamepad button is currently being held down on any connected gamepad.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one gamepad button is currently pressed on any connected gamepad;
	/// otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="GamepadButton"/> enumeration
	/// and checks for the pressed state of each button across all connected gamepads.
	/// It returns <c>true</c> on the first button found in the pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting continuous gamepad input from any connected controller,
	/// such as for idle detection or input-sensitive effects that respond to any controller activity.
	/// </para>
	/// </remarks>
	public bool AnyGamepadButtonPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllGamepadButtons)
		{
			if (!IsGamepadPressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks whether any gamepad button was just pressed (transitioned from not pressed to pressed)
	/// in the current frame on any connected gamepad.
	/// </summary>
	/// <returns>
	/// <c>true</c> if at least one gamepad button was pressed in the current frame on any connected gamepad;
	/// otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method iterates through all values of the <see cref="GamepadButton"/> enumeration
	/// and checks for the just-pressed state of each button across all connected gamepads.
	/// It returns <c>true</c> on the first button found in the just-pressed state.
	/// </para>
	/// <para>
	/// The check is skipped entirely if the game window is not currently active
	/// (<see cref="Game.IsActive"/> is <c>false</c>).
	/// </para>
	/// <para>
	/// This method is useful for detecting the initial moment of any gamepad interaction,
	/// such as waking the game from a paused state or capturing initial controller focus.
	/// </para>
	/// </remarks>
	public bool AnyGamepadButtonJustPressed()
	{
		if (!Game.Instance.IsActive)
			return false;

		foreach (var button in AllGamepadButtons)
		{
			if (!IsGamepadJustPressed(button))
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if the specified gamepad button is currently released (not pressed).
	/// </summary>
	/// <param name="button">The gamepad button to check.</param>
	/// <returns><c>true</c> if the button is not pressed; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// Returns <c>false</c> if the game window is not currently active.
	/// </remarks>
	public bool IsGamepadReleased(GamepadButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		return !IsGamepadPressed(button);
	}

	/// <summary>
	/// Checks if the specified gamepad button was just pressed once (debounced).
	/// </summary>
	/// <param name="button">The gamepad button to check.</param>
	/// <returns><c>true</c> if the button was just pressed and not yet acknowledged; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This is a one-time trigger meant to prevent multiple detections during a single press cycle.  
	/// Returns <c>false</c> if the game window is not active or if the press has already been registered.
	/// </remarks>
	public bool IsGamepadJustPressed(GamepadButton button)
	{
		if (!Game.Instance.IsActive)
			return false;

		if (_gamepadJustPressed.Contains(button))
			return false;

		if (IsGamepadPressed(button))
		{
			_gamepadJustPressed.Add(button);
			return true;
		}

		return false;
	}

	/// <summary>
	/// Retrieves the analog force or press intensity for a given gamepad button.
	/// </summary>
	/// <param name="button">The gamepad button or analog direction to query.</param>
	/// <returns>
	/// A float between <c>0</c> and <c>1</c> representing how strongly the button is pressed or the axis is engaged.  
	/// Returns <c>1</c> for digital buttons, or a normalized value for analog triggers and sticks.  
	/// Returns <c>0</c> if the input is inactive or no device is connected.
	/// </returns>
	/// <remarks>
	/// <list type="bullet">
	///   <item>
	///     <description>For digital buttons (e.g., A, B, DPad), the value is <c>1.0</c> if pressed, otherwise <c>0.0</c>.</description>
	///   </item>
	///   <item>
	///     <description>For analog inputs (e.g., triggers, sticks), the returned value reflects the raw axis magnitude, normalized and clamped.</description>
	///   </item>
	///   <item>
	///     <description>The method automatically detects connected joysticks, updates internal state, and sets <see cref="Current"/> to <see cref="ActiveInput.Gamepad"/> if any input is active.</description>
	///   </item>
	/// </list>
	/// </remarks>
	public float GetGamepadForce(GamepadButton button)
	{
		if (!Game.Instance.IsActive) return 0f;
		SFJoystick.Update();

		// foreach (var joyId in joys)
		foreach (var (joyId, connected) in _joysticks)
		{
			if (!connected) continue;

			if (TryGetSdlIndex(joyId, button, out var idx))
			{
				// if idx refers to an axis, call GetAxis; otherwise treat as button
				if (IsAxisButton(button))
				{
					float f = GetAxis(joyId, (SFJoystickAxis)idx);
					if (f != 0f)
					{
						Current = ActiveInput.Gamepad;
						return MathF.Abs(f);
					}
				}
				else
				{
					if (SFJoystick.IsButtonPressed(joyId, (uint)idx))
					{
						Current = ActiveInput.Gamepad;
						return 1f;
					}
				}
			}
			else
			{
				float result = button switch
				{
					GamepadButton.AButton => SFJoystick.IsButtonPressed(joyId, 0) ? 1f : 0f,
					GamepadButton.BButton => SFJoystick.IsButtonPressed(joyId, 1) ? 1f : 0f,
					GamepadButton.XButton => SFJoystick.IsButtonPressed(joyId, 2) ? 1f : 0f,
					GamepadButton.YButton => SFJoystick.IsButtonPressed(joyId, 3) ? 1f : 0f,
					GamepadButton.LeftBumper => SFJoystick.IsButtonPressed(joyId, 4) ? 1f : 0f,
					GamepadButton.RightBumper => SFJoystick.IsButtonPressed(joyId, 5) ? 1f : 0f,
					GamepadButton.Back => SFJoystick.IsButtonPressed(joyId, 6) ? 1f : 0f,
					GamepadButton.Start => SFJoystick.IsButtonPressed(joyId, 7) ? 1f : 0f,
					GamepadButton.LeftStick => SFJoystick.IsButtonPressed(joyId, 8) ? 1f : 0f,
					GamepadButton.RightStick => SFJoystick.IsButtonPressed(joyId, 9) ? 1f : 0f,
					GamepadButton.DPadLeft => GetPovX(joyId) < 0f ? 1f : 0f,
					GamepadButton.DPadRight => GetPovX(joyId) > 0f ? 1f : 0f,
					GamepadButton.DPadDown => GetPovY(joyId) < 0f ? 1f : 0f,
					GamepadButton.DPadUp => GetPovY(joyId) > 0f ? 1f : 0f,
					GamepadButton.LeftStickLeft => MathF.Max(0f, -GetAxis(joyId, SFJoystickAxis.X)),
					GamepadButton.LeftStickRight => MathF.Max(0f, GetAxis(joyId, SFJoystickAxis.X)),
					GamepadButton.LeftStickUp => MathF.Max(0f, -GetAxis(joyId, SFJoystickAxis.Y)),
					GamepadButton.LeftStickDown => MathF.Max(0f, GetAxis(joyId, SFJoystickAxis.Y)),
					GamepadButton.RightStickLeft => MathF.Max(0f, -GetAxis(joyId, SFJoystickAxis.U)),
					GamepadButton.RightStickRight => MathF.Max(0f, GetAxis(joyId, SFJoystickAxis.U)),
					GamepadButton.RightStickUp => MathF.Max(0f, -GetAxis(joyId, SFJoystickAxis.V)),
					GamepadButton.RightStickDown => MathF.Max(0f, GetAxis(joyId, SFJoystickAxis.V)),
					GamepadButton.LeftTrigger => MathF.Max(0f, -GetAxis(joyId, SFJoystickAxis.Z)),
					GamepadButton.RightTrigger => MathF.Max(0f, GetAxis(joyId, SFJoystickAxis.Z)),
					_ => 0f
				};

				if (result > 0f)
				{
					Current = ActiveInput.Gamepad;

					return result;
				}
			}
		}

		return 0f;
	}

	/// <summary>
	/// Checks if the specified gamepad button is currently pressed on any connected joystick.
	/// </summary>
	/// <param name="button">The gamepad button or axis-based virtual button to check.</param>
	/// <returns><c>true</c> if the button is actively pressed; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This method supports both digital and analog inputs, including stick directions and triggers.
	/// It will also automatically update <see cref="Current"/> to <see cref="ActiveInput.Gamepad"/> if input is detected.
	/// <para/>
	/// Input is resolved in two stages:
	/// <list type="bullet">
	///   <item>
	///     <description>First, SDL-mapped button indices are checked using <c>TryGetSdlIndex</c>.</description>
	///   </item>
	///   <item>
	///     <description>If no SDL mapping is found, a fallback mapping is used based on the raw button or axis layout.</description>
	///   </item>
	/// </list>
	/// The gamepad must be connected and the application window must be active for this to return <c>true</c>.
	/// </remarks>
	public bool IsGamepadPressed(GamepadButton button)
	{
		if (!Game.Instance.IsActive) return false;
		SFJoystick.Update();

		foreach (var (joyId, connected) in _joysticks)
		{
			if (!connected) continue;

			// try SDL mapping first
			if (TryGetSdlIndex(joyId, button, out var idx))
			{
				if (SFJoystick.IsButtonPressed(joyId, (uint)idx))
				{
					Current = ActiveInput.Gamepad;
					return true;
				}
			}
			else
			{
				bool result = button switch
				{
					GamepadButton.AButton => SFJoystick.IsButtonPressed(joyId, 0),
					GamepadButton.BButton => SFJoystick.IsButtonPressed(joyId, 1),
					GamepadButton.XButton => SFJoystick.IsButtonPressed(joyId, 2),
					GamepadButton.YButton => SFJoystick.IsButtonPressed(joyId, 3),
					GamepadButton.LeftBumper => SFJoystick.IsButtonPressed(joyId, 4),
					GamepadButton.RightBumper => SFJoystick.IsButtonPressed(joyId, 5),
					GamepadButton.Back => SFJoystick.IsButtonPressed(joyId, 6),
					GamepadButton.Start => SFJoystick.IsButtonPressed(joyId, 7),
					GamepadButton.LeftStick => SFJoystick.IsButtonPressed(joyId, 8),
					GamepadButton.RightStick => SFJoystick.IsButtonPressed(joyId, 9),

					// Axis:
					GamepadButton.DPadLeft => GetPovX(joyId) < -DeadZone,
					GamepadButton.DPadRight => GetPovX(joyId) > DeadZone,
					GamepadButton.DPadDown => GetPovY(joyId) < -DeadZone,
					GamepadButton.DPadUp => GetPovY(joyId) > DeadZone,
					GamepadButton.LeftStickLeft => GetAxis(joyId, SFJoystickAxis.X) < -DeadZone,
					GamepadButton.LeftStickRight => GetAxis(joyId, SFJoystickAxis.X) > DeadZone,
					GamepadButton.LeftStickUp => GetAxis(joyId, SFJoystickAxis.Y) < -DeadZone,
					GamepadButton.LeftStickDown => GetAxis(joyId, SFJoystickAxis.Y) > DeadZone,
					GamepadButton.RightStickLeft => GetAxis(joyId, SFJoystickAxis.U) < -DeadZone,
					GamepadButton.RightStickRight => GetAxis(joyId, SFJoystickAxis.U) > DeadZone,
					GamepadButton.RightStickUp => GetAxis(joyId, SFJoystickAxis.V) < -DeadZone,
					GamepadButton.RightStickDown => GetAxis(joyId, SFJoystickAxis.V) > DeadZone,
					GamepadButton.LeftTrigger => GetAxis(joyId, SFJoystickAxis.Z) < -DeadZone,
					GamepadButton.RightTrigger => GetAxis(joyId, SFJoystickAxis.Z) > DeadZone,

					_ => false
				};

				if (result)
				{
					Current = ActiveInput.Gamepad;

					return true;
				}
			}
		}

		return false;
	}
	#endregion


	#region Transform
	/// <summary>
	/// Transforms a screen-space position (e.g., mouse or cursor) into world-space coordinates using the specified camera.
	/// </summary>
	/// <param name="position">The screen-space position in pixels.</param>
	/// <param name="camera">The camera used to perform the coordinate transformation.</param>
	/// <returns>The corresponding world-space <see cref="Vect2"/> position.</returns>
	/// <remarks>
	/// Useful for detecting where input occurred within the game world, accounting for camera zoom and offset.
	/// </remarks>
	public Vect2 Transform(Vect2 position, Camera camera)
	{
		var w = Game.Instance.ToRenderer;

		return w.MapPixelToCoords(position, camera.ToEngine);
	}

	/// <summary>
	/// Transforms a screen-space position into world-space coordinates using the active camera on the given screen.
	/// </summary>
	/// <param name="position">The screen-space position in pixels.</param>
	/// <param name="screen">The screen instance whose camera will be used for transformation.</param>
	/// <returns>The corresponding world-space <see cref="Vect2"/> position.</returns>
	public Vect2 Transform(Vect2 position, Screen screen) =>
		Transform(position, screen.Camera);
	#endregion


	#region Initialization/De-Initializaiton
	internal void Load()
	{
		var w = Game.Instance.ToRenderer;

		w.JoystickButtonReleased += OnJoystickButtonReleased;
		w.JoystickConnected += OnJoystickConnected;
		w.JoystickDisconnected += OnJoystickDisconnected;
		w.MouseButtonReleased += OnMouseButtonReleased;
		w.KeyReleased += OnKeyReleased;

		foreach (var (joyId, isConnected) in _joysticks)
		{
			if (!isConnected)
				continue;

			InternalConnectJoystick(joyId);
		}
	}

	internal void Unload()
	{
		var w = Game.Instance.ToRenderer;

		w.JoystickButtonReleased -= OnJoystickButtonReleased;
		w.JoystickConnected -= OnJoystickConnected;
		w.JoystickDisconnected -= OnJoystickDisconnected;
		w.MouseButtonReleased -= OnMouseButtonReleased;
		w.KeyReleased -= OnKeyReleased;
	}

	private readonly HashSet<KeyboardButton> _keysJustPressed = [];
	private void OnKeyReleased(object sender, SFKeyEventArgs e)
	{
		var button = (KeyboardButton)e.Code;
		_keysJustPressed.Remove(button);
	}

	private readonly HashSet<MouseButton> _mouseJustPressed = [];
	private void OnMouseButtonReleased(object sender, SFMouseButtonEventArgs e)
	{
		var button = (MouseButton)e.Button;
		_mouseJustPressed.Remove(button);
	}

	private readonly HashSet<GamepadButton> _gamepadJustPressed = [];
	private void OnJoystickButtonReleased(object sender, SFJoystickButtonEventArgs e)
	{
		GamepadButton button = e.Button switch
		{
			0 => GamepadButton.AButton,
			1 => GamepadButton.BButton,
			2 => GamepadButton.XButton,
			3 => GamepadButton.YButton,
			4 => GamepadButton.LeftBumper,
			5 => GamepadButton.RightBumper,
			6 => GamepadButton.Back,
			7 => GamepadButton.Start,
			8 => GamepadButton.LeftStick,
			9 => GamepadButton.RightStick,
			_ => GamepadButton.None
		};

		_gamepadJustPressed.Remove(button);
	}

	private void OnJoystickDisconnected(object sender, SFJoystickConnectEventArgs e)
	{
		SFJoystick.Update();
		_joysticks[e.JoystickId] = false;

		Logger.Instance.Log(LogLevel.Info, $"Gamepad disconnected on ID: {e.JoystickId}, joysticks ID is now: {_joysticks[e.JoystickId]}");
	}
	private void OnJoystickConnected(object sender, SFJoystickConnectEventArgs e)
	{
		SFJoystick.Update();
		_joysticks[e.JoystickId] = true;

		InternalConnectJoystick(e.JoystickId);
	}

	private void InternalConnectJoystick(uint joyId)
	{
		var ident = SFJoystick.GetIdentification(joyId);
		string vidHex = ident.VendorId.ToString("x4");  // e.g. "045e"
		string pidHex = ident.ProductId.ToString("x4"); // e.g. "028e"
		var entry = _allEntries.FirstOrDefault(ent =>
			ent.Guid.IndexOf(vidHex, StringComparison.OrdinalIgnoreCase) >= 0 &&
			ent.Guid.IndexOf(pidHex, StringComparison.OrdinalIgnoreCase) >= 0
		);

		if (entry == null && !string.IsNullOrEmpty(ident.Name))
		{
			string lowerName = ident.Name.ToLowerInvariant();
			entry = _allEntries.FirstOrDefault(ent =>
				ent.Name?.ToLowerInvariant().Split(' ')
					.Any(word => lowerName.Contains(word)) == true
			);
		}

		if (entry != null)
		{
			_controllerMaps[joyId] = entry.ButtonMap;

			Logger.Instance.Log(LogLevel.Info,
				$"[InputMap] Loaded mapping for “{entry.Name}” " +
				$"(VID=0x{vidHex}, PID=0x{pidHex})"
			);
		}
		else
		{
			Logger.Instance.Log(LogLevel.Warning,
				$"[InputMap] No SDL mapping found for joystick “{ident.Name}” " +
				$"(VID=0x{vidHex}, PID=0x{pidHex})"
			);
		}
	}
	#endregion


	#region Actions
	/// <summary>
	/// Checks whether the input action associated with the given enum is currently being pressed.
	/// </summary>
	/// <param name="name">An enum value representing the action name.</param>
	/// <returns><c>true</c> if any input bound to the action is currently pressed; otherwise, <c>false</c>.</returns>
	public bool IsActionPressed(Enum name) => IsActionPressed(name.ToEnumString());

	/// <summary>
	/// Checks whether the input action associated with the given name is currently being pressed.
	/// </summary>
	/// <param name="name">The name of the input action (case-sensitive).</param>
	/// <returns><c>true</c> if any associated keyboard, mouse, or gamepad binding is currently pressed; otherwise, <c>false</c>.</returns>
	public bool IsActionPressed(string name)
	{
		var hash = HashHelpers.Cache32(name);

		if (!Actions.TryGetValue(hash, out var actions))
			return false;

		foreach (var action in actions)
		{
			if (action is KeyboardInputState keyboard)
			{
				var result = IsKeyPressed(keyboard.ValueAs<KeyboardButton>());

				if (result)
					return true;
			}

			if (action is MouseInputState mouse)
			{
				var result = IsMousePressed(mouse.ValueAs<MouseButton>());

				if (result)
					return true;
			}

			if (action is GamepadInputState gamepad)
			{
				var result = IsGamepadPressed(gamepad.ValueAs<GamepadButton>());

				if (result)
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Attempts to get the force or analog value associated with the specified input action (by enum).
	/// </summary>
	/// <param name="name">The enum representing the input action.</param>
	/// <param name="output">
	/// When this method returns, contains the analog force value of the action, if active; otherwise, <c>0</c>.
	/// </param>
	/// <returns><c>true</c> if the action is currently active with a force greater than zero; otherwise, <c>false</c>.</returns>
	public bool TryGetActionForce(Enum name, out float output) =>
		TryGetActionForce(name.ToEnumString(), out output);

	/// <summary>
	/// Attempts to get the force or analog value associated with the specified input action name.
	/// </summary>
	/// <param name="name">The name of the input action.</param>
	/// <param name="output">
	/// When this method returns, contains the analog force value of the action, if active; otherwise, <c>0</c>.
	/// </param>
	/// <returns><c>true</c> if the action is currently active with a force greater than zero; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This method supports both digital (returns 1.0 if pressed) and analog inputs (e.g., triggers, sticks).
	/// </remarks>
	public bool TryGetActionForce(string name, out float output)
	{
		output = GetActionForce(name);
		return output > 0f;
	}

	/// <summary>
	/// Gets the force or analog value associated with the specified input action (by enum).
	/// </summary>
	/// <param name="name">The enum representing the input action.</param>
	/// <returns>
	/// A float representing the current activation level of the action:
	/// <c>1.0</c> for digital inputs if pressed, or the analog magnitude for gamepad axes (e.g., triggers, sticks);
	/// otherwise, <c>0.0</c>.
	/// </returns>
	public float GetActionForce(Enum name) => GetActionForce(name.ToEnumString());

	/// <summary>
	/// Gets the force or analog value associated with the specified input action name.
	/// </summary>
	/// <param name="name">The name of the input action.</param>
	/// <returns>
	/// A float representing the current activation level of the action:
	/// <c>1.0</c> for digital inputs if pressed, or the analog magnitude for gamepad axes (e.g., triggers, sticks);
	/// otherwise, <c>0.0</c>.
	/// </returns>
	/// <remarks>
	/// Supports keyboard, mouse, and gamepad inputs. Only the first matching active input source will return a non-zero value.
	/// </remarks>
	public float GetActionForce(string name)
	{
		var hash = HashHelpers.Cache32(name);

		if (!Actions.TryGetValue(hash, out var actions))
			return 0f;

		foreach (var action in actions)
		{
			if (action is KeyboardInputState keyboard)
			{
				var result = IsKeyPressed(keyboard.ValueAs<KeyboardButton>());

				if (result)
					return 1f;
			}

			if (action is MouseInputState mouse)
			{
				var result = IsMousePressed(mouse.ValueAs<MouseButton>());

				if (result)
					return 1f;
			}

			if (action is GamepadInputState gamepad)
			{
				var result = GetGamepadForce(gamepad.ValueAs<GamepadButton>());

				if (result > 0f)
					return result;
			}
		}

		return 0f;
	}


	/// <summary>
	/// Returns whether the specified input action (by enum) was just pressed this frame.
	/// </summary>
	/// <param name="name">The enum representing the input action.</param>
	/// <returns><c>true</c> if the action was just pressed; otherwise, <c>false</c>.</returns>
	public bool IsActionJustPressed(Enum name) => IsActionJustPressed(name.ToEnumString());

	/// <summary>
	/// Returns whether the specified input action name was just pressed this frame.
	/// </summary>
	/// <param name="name">The name of the input action.</param>
	/// <returns><c>true</c> if the action was just pressed; otherwise, <c>false</c>.</returns>
	public bool IsActionJustPressed(string name)
	{
		var hash = HashHelpers.Cache32(name);

		if (!Actions.TryGetValue(hash, out var actions))
			return false;

		foreach (var action in actions)
		{
			if (action is KeyboardInputState keyboard)
			{
				var result = IsKeyJustPressed(keyboard.ValueAs<KeyboardButton>());

				if (result)
					return true;
			}

			if (action is MouseInputState mouse)
			{
				var result = IsMouseJustPressed(mouse.ValueAs<MouseButton>());

				if (result)
					return true;
			}

			if (action is GamepadInputState gamepad)
			{
				var result = IsGamepadJustPressed(gamepad.ValueAs<GamepadButton>());

				if (result)
					return true;
			}
		}

		return false;
	}


	/// <summary>
	/// Returns whether the specified input action (by enum) is currently released (not pressed).
	/// </summary>
	/// <param name="name">The enum representing the input action.</param>
	/// <returns><c>true</c> if the action is released; otherwise, <c>false</c>.</returns>
	public bool IsActionReleased(Enum name) => IsActionReleased(name.ToEnumString());

	/// <summary>
	/// Returns whether the specified input action name is currently released (not pressed).
	/// </summary>
	/// <param name="name">The name of the input action.</param>
	/// <returns><c>true</c> if the action is released; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This checks the release state across all input types bound to the action: keyboard, mouse, and gamepad.
	/// If any bound input is still pressed, the action is considered active.
	/// </remarks>
	public bool IsActionReleased(string name)
	{
		var hash = HashHelpers.Cache32(name);

		if (!Actions.TryGetValue(hash, out var actions))
			return false;

		foreach (var action in actions)
		{
			if (action is KeyboardInputState keyboard)
			{
				var result = IsKeyReleased(keyboard.ValueAs<KeyboardButton>());

				if (result)
					return true;
			}

			if (action is MouseInputState mouse)
			{
				var result = IsMouseReleased(mouse.ValueAs<MouseButton>());

				if (result)
					return true;
			}

			if (action is GamepadInputState gamepad)
			{
				var result = IsGamepadReleased(gamepad.ValueAs<GamepadButton>());

				if (result)
					return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Adds an input action using an enum name as the identifier.
	/// </summary>
	/// <param name="name">The enum representing the action name.</param>
	/// <param name="inputs">One or more input bindings (e.g., <see cref="KeyboardButton"/>, <see cref="MouseButton"/>, or <see cref="GamepadButton"/>).</param>
	public void AddAction(Enum name, params object[] inputs) => AddAction(name.ToEnumString(), inputs);

	/// <summary>
	/// Adds or updates an input action with the specified name and input bindings.
	/// </summary>
	/// <param name="name">The name of the action to bind inputs to.</param>
	/// <param name="inputs">An array of input sources, such as <see cref="KeyboardButton"/>, <see cref="MouseButton"/>, or <see cref="GamepadButton"/>.</param>
	/// <remarks>
	/// If the action does not already exist, it is created and assigned the given inputs.
	/// If it already exists, the new inputs are appended to the existing ones.
	/// </remarks>
	public void AddAction(string name, params object[] inputs)
	{
		var hash = HashHelpers.Cache32(name);

		if (!Actions.TryGetValue(hash, out var i))
			Actions[hash] = new List<InputMapEntry>(GetInputs(inputs));
		else
			i.AddRange(GetInputs(inputs));
	}
	#endregion


	#region Private Methods
	private bool IsAxisButton(GamepadButton button)
	{
		return button switch
		{
			GamepadButton.DPadUp
				or GamepadButton.DPadDown
				or GamepadButton.DPadLeft
				or GamepadButton.DPadRight
				or GamepadButton.LeftStickLeft
				or GamepadButton.LeftStickRight
				or GamepadButton.LeftStickUp
				or GamepadButton.LeftStickDown
				or GamepadButton.RightStickLeft
				or GamepadButton.RightStickRight
				or GamepadButton.RightStickUp
				or GamepadButton.RightStickDown
				or GamepadButton.LeftTrigger
				or GamepadButton.RightTrigger

			=> true,

			_ => false
		};
	}

	private bool TryGetSdlIndex(uint joyId, GamepadButton button, out int sdlIndex)
	{
		sdlIndex = -1;

		if (!_controllerMaps.TryGetValue(joyId, out var map))
			return false;

		char key = button switch
		{
			GamepadButton.AButton => 'a',
			GamepadButton.BButton => 'b',
			GamepadButton.XButton => 'x',
			GamepadButton.YButton => 'y',

			GamepadButton.LeftBumper => 'l',
			GamepadButton.RightBumper => 'r',
			GamepadButton.Back => 'b',   // may collide with BButton if your parser used 'b'
			GamepadButton.Start => 's',
			GamepadButton.LeftStick => 'L',
			GamepadButton.RightStick => 'R',

			GamepadButton.DPadUp => 'u',
			GamepadButton.DPadDown => 'd',
			GamepadButton.DPadLeft => 'l',
			GamepadButton.DPadRight => 'r',

			GamepadButton.LeftTrigger => 't',
			GamepadButton.RightTrigger => 'T',

			_ => '\0'
		};

		// If we got a valid key and the map contains it, return the index
		if (key != '\0' && map.TryGetValue(key, out var idx))
		{
			sdlIndex = idx;
			return true;
		}

		return false;
	}

	private List<InputMapEntry> GetInputs(object[] inputs)
	{
		if (inputs == null || inputs.Length == 0)
			return new();

		var result = new List<InputMapEntry>(inputs.Length);

		for (int i = 0; i < inputs.Length; i++)
		{
			var current = inputs[i];

			if (current is KeyboardButton keyboard)
				result.Add(new KeyboardInputState(keyboard));
			else if (current is MouseButton mouse)
				result.Add(new MouseInputState(mouse));
			else if (current is GamepadButton gamepad)
				result.Add(new GamepadInputState(gamepad));
		}

		return result;
	}

	private float GetAxis(uint joyId, SFJoystickAxis axis)
	{
		var raw = SFJoystick.GetAxisPosition(joyId, axis);
		var norm = Math.Clamp(raw / 100f, -1f, 1f);
		return MathF.Abs(norm) > EngineSettings.Instance.DeadZone ? norm : 0f;

	}

	private float GetPovX(uint joyId) => GetAxis(joyId, SFJoystickAxis.PovX);
	private float GetPovY(uint joyUd) => GetAxis(joyUd, SFJoystickAxis.PovY);
	private float DeadZone => EngineSettings.Instance.DeadZone;
	#endregion
}
