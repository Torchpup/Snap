namespace System;

/// <summary>
/// Provides extension methods for the <see cref="Sound"/> class to simplify audio playback operations.
/// These extensions offer convenient shortcuts for common sound manipulation patterns while
/// internally utilizing the sound instance pool for optimal performance.
/// </summary>
/// <remarks>
/// <para>
/// The extension methods encapsulate common audio workflows such as playing sounds with
/// specific configurations (volume, pitch, looping), stopping all instances of a sound,
/// and checking playback status. They provide a more fluent and readable API for
/// working with sounds in game code.
/// </para>
/// <para>
/// All playback methods internally use <see cref="SoundInstancePool"/> to manage
/// sound instances efficiently, ensuring proper resource management and performance.
/// </para>
/// </remarks>
public static class SoundExtentions
{
	/// <summary>
	/// Plays the sound once with the specified audio properties and automatically manages the instance.
	/// This method creates a temporary sound instance, configures it, plays it, and automatically
	/// releases it when playback completes.
	/// </summary>
	/// <param name="sound">The sound to play.</param>
	/// <param name="volume">The playback volume, where 1.0 is full volume and 0.0 is silent. Default is 1.0.</param>
	/// <param name="pan">The stereo panning, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered. Default is 0.0.</param>
	/// <param name="pitch">The playback pitch multiplier, where 1.0 is normal speed, 2.0 is one octave higher, and 0.5 is one octave lower. Default is 1.0.</param>
	/// <returns>The created sound instance, which will be automatically released when playback finishes.</returns>
	/// <remarks>
	/// <para>
	/// This method is ideal for one-shot sound effects where you don't need to maintain
	/// manual control over the instance lifecycle. The sound instance is automatically
	/// returned to the pool when playback completes.
	/// </para>
	/// <para>
	/// For sounds that need manual control (pausing, stopping, or monitoring), use
	/// <see cref="Sound.CreateInstance"/> or <see cref="SoundInstance.Play()"/> instead.
	/// </para>
	/// </remarks>
	public static SoundInstance PlayOneShot(this Sound sound, float volume = 1f, float pan = 0f, float pitch = 1f)
	{
		var instance = sound.CreateInstance();

		instance.Volume = volume;
		instance.Pan = pan;
		instance.Pitch = pitch;
		instance.Play();

		return instance;
	}

	/// <summary>
	/// Retrieves a sound bank using an enumeration value as its identifier.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <param name="bankId">The enumeration value representing the bank identifier.</param>
	/// <returns>The sound bank associated with the specified identifier.</returns>
	/// <remarks>
	/// This extension provides a type-safe way to access sound banks using enums,
	/// improving code readability and preventing string-based identifier errors.
	/// </remarks>
	public static SoundBank GetBank(this SoundManager manager, Enum bankId)
		=> manager.GetBank(bankId);

	/// <summary>
	/// Retrieves a sound bank using a numeric identifier.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <param name="bankId">The numeric identifier of the bank.</param>
	/// <returns>The sound bank associated with the specified numeric identifier.</returns>
	public static SoundBank GetBank(this SoundManager manager, uint bankId)
		=> manager.GetBank(bankId);

	/// <summary>
	/// Plays a sound through a bank specified by an enumeration identifier.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <param name="bankId">The enumeration value representing the target bank.</param>
	/// <param name="sound">The sound to play.</param>
	/// <returns>The created sound instance.</returns>
	/// <remarks>
	/// This extension simplifies playing sounds through specific banks while
	/// maintaining type-safe bank selection via enumerations.
	/// </remarks>
	public static SoundInstance Play(this SoundManager manager, Enum bankId, Sound sound)
		=> manager.Play(bankId, sound);

	/// <summary>
	/// Plays a sound through a bank specified by a numeric identifier.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <param name="bankId">The numeric identifier of the target bank.</param>
	/// <param name="sound">The sound to play.</param>
	/// <returns>The created sound instance.</returns>
	public static SoundInstance Play(this SoundManager manager, uint bankId, Sound sound)
		=> manager.Play(bankId, sound);

	/// <summary>
	/// Sets the overall volume for a sound bank.
	/// </summary>
	/// <param name="bank">The sound bank to configure.</param>
	/// <param name="volume">The volume level, where 1.0 is full volume and 0.0 is silent.</param>
	/// <remarks>
	/// This extension provides a fluent interface for setting bank volume.
	/// All sounds played through this bank will be affected by this volume setting.
	/// </remarks>
	public static void SetVolume(this SoundBank bank, float volume)
		=> bank.Volume = volume;

	/// <summary>
	/// Configures a sound instance to automatically dispose itself when playback finishes.
	/// </summary>
	/// <param name="instance">The sound instance to configure.</param>
	/// <returns>The same sound instance for method chaining.</returns>
	/// <remarks>
	/// This extension method attaches an event handler that automatically calls
	/// <see cref="SoundInstance.Dispose()"/> when the sound completes playback.
	/// Useful for fire-and-forget sound effects where you don't need to manage
	/// the instance lifecycle manually.
	/// </remarks>
	public static SoundInstance AutoDispose(this SoundInstance instance)
	{
		instance.OnPlaybackFinished += (inst) => inst.Dispose();
		return instance;
	}

	/// <summary>
	/// Sets the volume for a sound instance using a fluent interface.
	/// </summary>
	/// <param name="instance">The sound instance to configure.</param>
	/// <param name="volume">The volume level, where 1.0 is full volume and 0.0 is silent.</param>
	/// <returns>The same sound instance for method chaining.</returns>
	/// <remarks>
	/// This method allows for fluent configuration of sound properties.
	/// The volume change takes effect immediately if the sound is currently playing.
	/// </remarks>
	public static SoundInstance WithVolume(this SoundInstance instance, float volume)
	{
		instance.Volume = volume;
		return instance;
	}

	/// <summary>
	/// Sets the stereo panning for a sound instance using a fluent interface.
	/// </summary>
	/// <param name="instance">The sound instance to configure.</param>
	/// <param name="pan">The stereo panning, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered.</param>
	/// <returns>The same sound instance for method chaining.</returns>
	/// <remarks>
	/// This method allows for fluent configuration of sound properties.
	/// The panning change takes effect immediately if the sound is currently playing.
	/// </remarks>
	public static SoundInstance WithPan(this SoundInstance instance, float pan)
	{
		instance.Pan = pan;
		return instance;
	}

	/// <summary>
	/// Sets the pitch for a sound instance using a fluent interface.
	/// </summary>
	/// <param name="instance">The sound instance to configure.</param>
	/// <param name="pitch">The pitch multiplier, where 1.0 is normal speed, 2.0 is one octave higher, and 0.5 is one octave lower.</param>
	/// <returns>The same sound instance for method chaining.</returns>
	/// <remarks>
	/// This method allows for fluent configuration of sound properties.
	/// The pitch change takes effect immediately if the sound is currently playing.
	/// </remarks>
	public static SoundInstance WithPitch(this SoundInstance instance, float pitch)
	{
		instance.Pitch = pitch;
		return instance;
	}

	/// <summary>
	/// Plays a sound instance with the specified audio properties.
	/// </summary>
	/// <param name="instance">The sound instance to play.</param>
	/// <param name="volume">The playback volume, where 1.0 is full volume and 0.0 is silent.</param>
	/// <param name="pan">The stereo panning, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered. Default is 0.0.</param>
	/// <param name="pitch">The playback pitch multiplier, where 1.0 is normal speed, 2.0 is one octave higher, and 0.5 is one octave lower. Default is 1.0.</param>
	/// <returns>The same sound instance for method chaining.</returns>
	/// <remarks>
	/// This method provides a fluent interface for configuring and immediately playing a sound instance.
	/// Useful when you have an existing instance that you want to play with specific settings.
	/// </remarks>
	public static SoundInstance PlayWith(this SoundInstance instance, float volume, float pan = 0f, float pitch = 1f)
	{
		instance.Volume = volume;
		instance.Pan = pan;
		instance.Pitch = pitch;
		instance.Play();

		return instance;
	}

	/// <summary>
	/// Plays a sound once and automatically disposes the instance when finished.
	/// </summary>
	/// <param name="sound">The sound to play.</param>
	/// <param name="volume">The playback volume, where 1.0 is full volume and 0.0 is silent. Default is 1.0.</param>
	/// <param name="pan">The stereo panning, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered. Default is 0.0.</param>
	/// <param name="pitch">The playback pitch multiplier, where 1.0 is normal speed. Default is 0.0.</param>
	/// <remarks>
	/// This method is the ultimate convenience for fire-and-forget sound playback.
	/// It handles instance creation, configuration, playback, and disposal automatically.
	/// Ideal for UI sounds, one-shot effects, or any sound where you don't need to track the instance.
	/// </remarks>
	public static void PlayAndForget(this Sound sound, float volume = 1f, float pan = 0f, float pitch = 1f)
		=> sound.PlayOneShot(volume, pan, pitch).AutoDispose();

	/// <summary>
	/// Plays a sound through a specific bank and automatically disposes the instance when finished.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <param name="bankId">The numeric identifier of the target bank.</param>
	/// <param name="sound">The sound to play.</param>
	/// <param name="volume">The playback volume, where 1.0 is full volume and 0.0 is silent. Default is 1.0.</param>
	/// <param name="pan">The stereo panning, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered. Default is 0.0.</param>
	/// <param name="pitch">The playback pitch multiplier, where 1.0 is normal speed. Default is 1.0.</param>
	/// <remarks>
	/// This method combines bank-based playback with automatic instance management.
	/// Useful for categorized sounds where you want both bank organization and automatic cleanup.
	/// </remarks>
	public static void PlayAndForget(this SoundManager manager,
		uint bankId, Sound sound, float volume = 1f, float pan = 0f, float pitch = 1f)
	{
		var inst = manager.Play(bankId, sound);
		inst.Volume = volume;
		inst.Pan = pan;
		inst.Pitch = pitch;
		inst.AutoDispose();
	}

	/// <summary>
	/// Plays all sounds in a collection simultaneously with the specified audio properties.
	/// </summary>
	/// <param name="sounds">The collection of sounds to play.</param>
	/// <param name="volume">The playback volume for all sounds, where 1.0 is full volume and 0.0 is silent. Default is 1.0.</param>
	/// <param name="pan">The stereo panning for all sounds, where -1.0 is fully left, 1.0 is fully right, and 0.0 is centered. Default is 0.0.</param>
	/// <param name="pitch">The playback pitch multiplier for all sounds, where 1.0 is normal speed, 2.0 is one octave higher, and 0.5 is one octave lower. Default is 1.0.</param>
	/// <returns>A list containing all created sound instances for potential further manipulation.</returns>
	/// <remarks>
	/// This method is useful for playing sound groups like weapon clips, environmental ambience layers,
	/// or musical chord components. Each sound receives its own instance with identical audio properties.
	/// The method returns all instances for optional tracking, but they are not auto-disposed.
	/// </remarks>
	public static List<SoundInstance> PlayAll(this IEnumerable<Sound> sounds,
	float volume = 1f, float pan = 0f, float pitch = 1f)
	{
		return sounds.Select(s => s.PlayOneShot(volume, pan, pitch)).ToList();
	}

	/// <summary>
	/// Stops playback of all sound instances currently active within this sound bank.
	/// </summary>
	/// <param name="bank">The sound bank to silence.</param>
	/// <remarks>
	/// This method iterates through all sounds registered with the bank and stops their
	/// active instances. Useful for scenarios like pausing game audio, switching scenes,
	/// or implementing audio mute functionality for a specific category of sounds.
	/// Stopped instances remain in the bank and can be restarted later.
	/// </remarks>
	public static void StopAll(this SoundBank bank)
    {
        foreach(var soundPair in bank.Instances)
        {
            foreach(var instance in soundPair.Value)
				instance.Stop();
        }
	}

	/// <summary>
	/// Generates a formatted string containing comprehensive audio system statistics.
	/// </summary>
	/// <param name="manager">The sound manager instance.</param>
	/// <returns>
	/// A formatted string displaying key audio statistics including:
	/// bank count, instance pool usage, active playback count, and a summary
	/// of instance distribution across individual sounds (showing up to 5 sounds).
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method provides a convenient summary of the audio system's current state
	/// for debugging, profiling, or monitoring purposes. The statistics are retrieved
	/// in a thread-safe manner and formatted for human-readable output.
	/// </para>
	/// <para>
	/// The output includes:
	/// <list type="bullet">
	///   <item><description>Number of sound banks</description></item>
	///   <item><description>Active and available instance counts</description></item>
	///   <item><description>Currently playing instance count from the manager</description></item>
	///   <item><description>A summary of instance distribution per sound (first 5 sounds shown)</description></item>
	/// </list>
	/// If more than 5 sounds have instances, the summary includes an ellipsis with remaining count.
	/// </para>
	/// </remarks>
	public static string GetAudioStats(this SoundManager manager)
	{
		var (active, available, perSound) = SoundInstancePool.GetStats();

		string perSoundSummary = string.Join(", ",
			perSound.Select(kv => $"Sound {kv.Key}: {kv.Value}").Take(5));

		if (perSound.Count > 5)
			perSoundSummary += $", ... ({perSound.Count - 5} more)";

		return
			$"Banks: {manager.Count}, Instances: {active}/{available}, " +
			$"Playing: {manager.PlayCount}, Sounds: [{perSoundSummary}]";
	}

	/// <summary>
	/// Logs diagnostic information about currently playing sounds within this bank.
	/// </summary>
	/// <param name="bank">The sound bank to analyze.</param>
	/// <remarks>
	/// This method iterates through all sounds in the bank and logs information
	/// about sounds that have active playback instances. Useful for debugging
	/// audio issues, identifying resource leaks, or monitoring audio playback
	/// during development. Only sounds with currently playing instances are logged.
	/// </remarks>
	public static void LogPlayingSounds(this SoundBank bank)
    {
        foreach(var soundPair in bank.Instances)
        {
			var playing = soundPair.Value.Where(x => x.IsPlaying).ToList();
            if(playing.Count > 0)
            {
				Logger.Instance.Log(LogLevel.Info,
					$"Bank {bank.Id}: Sound {soundPair.Key.Id} has {playing.Count} playing instances.");
			}
		}
    }
}
