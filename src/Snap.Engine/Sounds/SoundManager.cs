namespace Snap.Engine.Sounds;

/// <summary>
/// Provides centralized management of sound banks and active sound instances.
/// </summary>
/// <remarks>
/// The <see cref="SoundManager"/> is implemented as a sealed singleton, ensuring a single global instance
/// throughout the application.  
/// It maintains a collection of <see cref="SoundBank"/> objects, tracks active sounds, and provides
/// configuration for automatic eviction of unused assets.
/// </remarks>
public sealed class SoundManager
{
	private const int MaxSoundBanks = 16;

	private readonly Dictionary<uint, SoundBank> _banks = new(MaxSoundBanks);

	/// <summary>
	/// Gets the singleton instance of the <see cref="SoundManager"/>.
	/// </summary>
	/// <remarks>
	/// This property ensures that only one <see cref="SoundManager"/> exists within the application.  
	/// The instance is created and managed internally.
	/// </remarks>
	public static SoundManager Instance { get; private set; }

	/// <summary>
	/// Gets the number of sound banks currently managed.
	/// </summary>
	/// <value>
	/// An <see cref="int"/> representing the total count of sound banks in the manager.
	/// </value>
	public int Count => _banks.Count;

	/// <summary>
	/// Gets the total number of active sound instances across all managed sound banks.
	/// </summary>
	/// <value>
	/// An <see cref="int"/> representing the sum of active sound instances.
	/// </value>
	public int PlayCount => _banks.Sum(x => x.Value.Count);

	/// <summary>
	/// Gets or sets the time, in minutes, after which unused sound assets may be evicted.
	/// </summary>
	/// <remarks>
	/// Defaults to 15 minutes.  
	/// This property can be adjusted to control memory usage and asset lifecycle management.
	/// </remarks>
	/// <value>
	/// A <see cref="float"/> representing the eviction threshold in minutes.
	/// </value>
	public float EvictAfterMinutes { get; set; } = 15f;

	internal SoundManager()
	{
		Instance ??= this;

		// create master channel....
		_banks.Add(0, new SoundBank(0, EvictAfterMinutes));
	}

	/// <summary>
	/// Adds a new sound bank to the manager using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="volume">
	/// The initial volume level for the bank, clamped between 0.0 (silent) and 1.0 (full volume).  
	/// Defaults to 1.0.
	/// </param>
	/// <param name="pan">
	/// The initial stereo pan for the bank, clamped between -1.0 (left) and 1.0 (right).  
	/// Defaults to 0.0 (center).
	/// </param>
	/// <param name="pitch">
	/// The initial pitch adjustment for the bank, clamped between -3.0 and 3.0.  
	/// Defaults to 1.0 (original pitch).
	/// </param>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="AddBank(uint,float,float,float)"/>.
	/// </remarks>
	public void AddBank(Enum bankId, float volume = 1f, float pan = 0f, float pitch = 1f) =>
		AddBank(Convert.ToUInt32(bankId), volume, pan, pitch);

	/// <summary>
	/// Adds a new sound bank to the manager using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must be greater than zero and not already in use.
	/// </param>
	/// <param name="volume">
	/// The initial volume level for the bank, clamped between 0.0 (silent) and 1.0 (full volume).  
	/// Defaults to 1.0.
	/// </param>
	/// <param name="pan">
	/// The initial stereo pan for the bank, clamped between -1.0 (left) and 1.0 (right).  
	/// Defaults to 0.0 (center).
	/// </param>
	/// <param name="pitch">
	/// The initial pitch adjustment for the bank, clamped between -3.0 and 3.0.  
	/// Defaults to 1.0 (original pitch).
	/// </param>
	/// <exception cref="Exception">
	/// Thrown if <paramref name="bankId"/> is zero, if the maximum number of banks has been reached,
	/// or if a bank with the same identifier already exists.
	/// </exception>
	/// <remarks>
	/// A new <see cref="SoundBank"/> is created and added to the internal collection.  
	/// The addition is logged for diagnostic purposes.
	/// </remarks>
	public void AddBank(uint bankId, float volume = 1f, float pan = 0f, float pitch = 1f)
	{
		if (bankId == 0)
			throw new Exception();
		if (_banks.Count >= MaxSoundBanks) // can only do up to 16 banks.
			throw new Exception();
		if (_banks.ContainsKey(bankId))
			throw new Exception();

		_banks[bankId] = new SoundBank(bankId, EvictAfterMinutes, volume, pan, pitch);

		Logger.Instance.Log(LogLevel.Info,
			$"Sound channel ID {bankId} has been added and ready for sounds.");
	}

	/// <summary>
	/// Removes a sound bank from the manager using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank to remove.
	/// </param>
	/// <returns>
	/// <c>true</c> if the bank was successfully removed; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="RemoveBank(uint)"/>.
	/// </remarks>
	public bool RemoveBank(Enum bankId) => RemoveBank(Convert.ToUInt32(bankId));

	/// <summary>
	/// Removes a sound bank from the manager using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank to remove.  
	/// Must be greater than zero and correspond to an existing bank.
	/// </param>
	/// <returns>
	/// <c>true</c> if the bank was successfully removed; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if <paramref name="bankId"/> is zero or if no bank with the specified identifier exists.
	/// </exception>
	/// <remarks>
	/// Before removal, all sound instances within the bank are cleared by calling <see cref="SoundBank.Clear"/>.  
	/// A log entry is written to indicate successful removal.  
	/// The method then attempts to remove the bank from the internal collection.
	/// </remarks>
	public bool RemoveBank(uint bankId)
	{
		if (bankId == 0)
			throw new Exception();
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception();

		// remove any instances...
		bank.Clear();

		Logger.Instance.Log(LogLevel.Info, $"Sound channel ID {bankId} successfully removed.");

		return _banks.Remove(bankId); ;
	}

	/// <summary>
	/// Plays a sound in the specified sound bank using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to be played. Must not be <c>null</c>.
	/// </param>
	/// <returns>
	/// A <see cref="SoundInstance"/> representing the newly created and playing sound.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the bank does not exist or if <paramref name="sound"/> is <c>null</c>.
	/// </exception>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="Play(uint,Sound)"/>.
	/// </remarks>
	public SoundInstance Play(Enum bankId, Sound sound) => Play(Convert.ToUInt32(bankId), sound);

	/// <summary>
	/// Plays a sound in the specified sound bank using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must correspond to an existing bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to be played. Must not be <c>null</c>.  
	/// If the sound is invalid (e.g., evicted), it will be reloaded before playback.
	/// </param>
	/// <returns>
	/// A <see cref="SoundInstance"/> representing the newly created and playing sound.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the bank does not exist or if <paramref name="sound"/> is <c>null</c>.
	/// </exception>
	/// <remarks>
	/// If the sound is invalid, <see cref="Sound.Load"/> is called to reload it.  
	/// A log entry is written to indicate playback, and the sound is added to the specified bank.
	/// </remarks>
	public SoundInstance Play(uint bankId, Sound sound)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception($"Bank ID: {bankId} not found in sound banks.");
		if (sound == null)
			throw new Exception();
		
		if(!sound.IsValid) // Incase it was invicted
			sound.Load();

		Logger.Instance.Log(LogLevel.Info, $"Playing sound ID: {sound.Id} in sound bank ID: {bankId}.");

		return bank.Add(sound);
	}

	/// <summary>
	/// Retrieves a sound bank from the manager using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <returns>
	/// The <see cref="SoundBank"/> associated with the specified identifier.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if no bank with the specified identifier exists.
	/// </exception>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="GetBank(uint)"/>.
	/// </remarks>
	public SoundBank GetBank(Enum bankId) => GetBank(Convert.ToUInt32(bankId));

	/// <summary>
	/// Retrieves a sound bank from the manager using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must correspond to an existing bank.
	/// </param>
	/// <returns>
	/// The <see cref="SoundBank"/> associated with the specified identifier.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if no bank with the specified identifier exists.
	/// </exception>
	/// <remarks>
	/// This method attempts to locate the bank in the internal collection.  
	/// If found, the bank is returned; otherwise, an exception is thrown.
	/// </remarks>
	public SoundBank GetBank(uint bankId)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception($"Bank ID: {bankId} not found in sound banks.");

		return bank;
	}

	/// <summary>
	/// Attempts to retrieve a sound bank using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="bank">
	/// When this method returns, contains the <see cref="SoundBank"/> associated with the specified identifier,
	/// if the bank is found; otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the bank was found; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="TryGetBank(uint,out SoundBank)"/>.
	/// </remarks>
	public bool TryGetBank(Enum bankId, out SoundBank bank) => TryGetBank(Convert.ToUInt32(bankId), out bank);

	/// <summary>
	/// Attempts to retrieve a sound bank using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="bank">
	/// When this method returns, contains the <see cref="SoundBank"/> associated with the specified identifier,
	/// if the bank is found; otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the bank was found; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method calls <see cref="GetBank(uint)"/> internally.  
	/// If the bank exists, it is returned via the <paramref name="bank"/> parameter and the method returns <c>true</c>.  
	/// If the bank does not exist, <paramref name="bank"/> will be <c>null</c> and the method returns <c>false</c>.
	/// </remarks>
	public bool TryGetBank(uint bankId, out SoundBank bank)
	{
		bank = GetBank(bankId);

		return bank != null;
	}


	/// <summary>
	/// Stops all sounds in the specified sound bank using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <returns>
	/// <c>true</c> if all sounds were successfully stopped and cleared from the bank; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="StopAll(uint)"/>.
	/// </remarks>
	public bool StopAll(Enum bankId) => StopAll(Convert.ToUInt32(bankId));

	/// <summary>
	/// Stops all sounds in the specified sound bank using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must correspond to an existing bank.
	/// </param>
	/// <returns>
	/// <c>true</c> if all sounds were successfully stopped and cleared from the bank; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if no bank with the specified identifier exists.
	/// </exception>
	/// <remarks>
	/// This method attempts to locate the bank in the internal collection.  
	/// If found, all sound instances are cleared by calling <see cref="SoundBank.Clear"/>.  
	/// A log entry is written to indicate that all sounds in the bank have been stopped.
	/// </remarks>
	public bool StopAll(uint bankId)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception();

		Logger.Instance.Log(LogLevel.Info, $"Sound channel ID: {bankId} stopped all sounds.");

		return bank.Clear();
	}

	/// <summary>
	/// Stops a specific sound in the given sound bank using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to stop. Must not be <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound was successfully stopped and removed from the bank; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the specified bank does not exist.
	/// </exception>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="Stop(uint,Sound)"/>.
	/// </remarks>
	public bool Stop(Enum bankId, Sound sound) => Stop(Convert.ToUInt32(bankId), sound);

	/// <summary>
	/// Stops a specific sound in the given sound bank using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must correspond to an existing bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to stop. Must not be <c>null</c>.  
	/// The sound is removed from the bank once stopped.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound was successfully stopped and removed from the bank; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the specified bank does not exist.
	/// </exception>
	/// <remarks>
	/// This method attempts to locate the bank in the internal collection.  
	/// If found, the sound is stopped and removed by calling <see cref="SoundBank.Remove(Sound)"/>.  
	/// A log entry is written to indicate that the sound was stopped.
	/// </remarks>
	public bool Stop(uint bankId, Sound sound)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception();

		Logger.Instance.Log(LogLevel.Info,
			$"Sound channel ID {bankId} stopped sound ID {sound.Id}.");

		return bank.Remove(sound);
	}

	/// <summary>
	/// Determines whether a given sound is currently playing in the specified sound bank,
	/// using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="input">
	/// The <see cref="Sound"/> to check for playback status.
	/// </param>
	/// <param name="output">
	/// When this method returns, contains the <paramref name="input"/> sound if it is not playing;  
	/// otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound is currently playing in the specified bank; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="IsPlayingConditonal(uint,Sound,out Sound)"/>.
	/// </remarks>
	public bool IsPlayingConditonal(Enum bankId, Sound input, out Sound output) =>
		IsPlayingConditonal(Convert.ToUInt32(bankId), input, out output);

	/// <summary>
	/// Determines whether a given sound is currently playing in the specified sound bank,
	/// using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="input">
	/// The <see cref="Sound"/> to check for playback status.
	/// </param>
	/// <param name="output">
	/// When this method returns, contains the <paramref name="input"/> sound if it is not playing;  
	/// otherwise, <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound is currently playing in the specified bank; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// This method calls <see cref="IsPlaying(uint,Sound)"/> internally.  
	/// If the sound is not playing, <paramref name="output"/> is set to the input sound and the method returns <c>false</c>.  
	/// If the sound is playing, <paramref name="output"/> is set to <c>null</c> and the method returns <c>true</c>.
	/// </remarks>
	public bool IsPlayingConditonal(uint bankId, Sound input, out Sound output)
	{
		if (!IsPlaying(bankId, input))
		{
			output = input;
			return false;
		}

		output = null;
		return true;
	}

	/// <summary>
	/// Determines whether a given sound is currently playing in the specified sound bank,
	/// using an enumeration identifier.
	/// </summary>
	/// <param name="bankId">
	/// An <see cref="Enum"/> value representing the unique identifier of the sound bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to check for playback status. Must not be <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound is currently playing in the specified bank; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the specified bank does not exist.
	/// </exception>
	/// <remarks>
	/// This overload converts the enumeration identifier to a <see cref="uint"/> and delegates to
	/// <see cref="IsPlaying(uint,Sound)"/>.
	/// </remarks>
	public bool IsPlaying(Enum bankId, Sound sound) => IsPlaying(Convert.ToUInt32(bankId), sound);

	/// <summary>
	/// Determines whether a given sound is currently playing in the specified sound bank,
	/// using a numeric identifier.
	/// </summary>
	/// <param name="bankId">
	/// A <see cref="uint"/> representing the unique identifier of the sound bank.  
	/// Must correspond to an existing bank.
	/// </param>
	/// <param name="sound">
	/// The <see cref="Sound"/> to check for playback status. Must not be <c>null</c>.
	/// </param>
	/// <returns>
	/// <c>true</c> if the sound is currently playing in the specified bank; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="Exception">
	/// Thrown if the specified bank does not exist.
	/// </exception>
	/// <remarks>
	/// This method attempts to locate the bank in the internal collection.  
	/// If found, it calls <see cref="SoundBank.IsSoundPlaying(Sound)"/> to determine whether the sound is active.  
	/// If the bank does not exist, an exception is thrown.
	/// </remarks>
	public bool IsPlaying(uint bankId, Sound sound)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception($"Bank ID {bankId} not found in sound banks.");

		return bank.IsSoundPlaying(sound);
	}

	/// <summary>
	/// Creates and plays a new sound instance with the specified playback parameters.
	/// </summary>
	/// <param name="sound">
	/// The <see cref="Sound"/> to be played. Must not be <c>null</c>.
	/// </param>
	/// <param name="volume">
	/// The initial volume level for the instance, clamped between 0.0 (silent) and 1.0 (full volume).  
	/// Defaults to 1.0.
	/// </param>
	/// <param name="pan">
	/// The initial stereo pan for the instance, clamped between -1.0 (left) and 1.0 (right).  
	/// Defaults to 0.0 (center).
	/// </param>
	/// <param name="pitch">
	/// The initial pitch adjustment for the instance, clamped between -3.0 and 3.0.  
	/// Defaults to 1.0 (original pitch).
	/// </param>
	/// <returns>
	/// A <see cref="SoundInstance"/> representing the newly created and playing sound.
	/// </returns>
	/// <remarks>
	/// This method calls <see cref="Sound.CreateInstance"/> to generate a new <see cref="SoundInstance"/>.  
	/// The instance’s <c>Volume</c>, <c>Pan</c>, and <c>Pitch</c> properties are set before playback begins.  
	/// A log entry is written to indicate the sound and instance identifiers along with the playback parameters.
	/// </remarks>
	public SoundInstance Play(Sound sound, float volume = 1f, float pan = 0, float pitch = 1f)
	{
		var inst = sound.CreateInstance();

		inst.Volume = volume;
		inst.Pan = pan;
		inst.Pitch = pitch;
		inst.Play();

		Logger.Instance.Log(LogLevel.Info,
			$"Playing sound {sound.Id} (Instance: {inst.Id}) with Volume={volume}, Pan={pan}, Pitch={pitch}.");

		return inst;
	}

	internal void Clear()
	{
		if (_banks.Count == 0)
			return;

		foreach (var kv in _banks)
			kv.Value.Clear();
		_banks.Clear();
	}
}
