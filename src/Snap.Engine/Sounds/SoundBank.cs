namespace Snap.Engine.Sounds;

/// <summary>
/// Provides centralized management of audio assets such as sound effects and music.
/// </summary>
/// <remarks>
/// The <c>SoundBank</c> class is responsible for loading, caching, and retrieving audio resources.
/// It is declared <c>sealed</c> to prevent inheritance and ensure consistent audio handling.
/// Typical usage involves registering sounds by key and retrieving them for playback.
/// </remarks>
public sealed class SoundBank
{
	private class SoundInstanceWrapped
	{
		public SoundInstance Instance;
		public DateTime LastAccessFrame;
	}

	private float _volume = 1.0f, _pan = 0f, _pitch = 0f;
	private DateTime _lastMaintenceCheck = DateTime.Now;
	private readonly float _evictAfterMinutes;
	private readonly Dictionary<Sound, List<SoundInstanceWrapped>> _instances = new(128);

	/// <summary>Occurs when a new sound instance is created and added to the bank.</summary>
	public Action<SoundBank, SoundInstance> OnInstanceAdded;

	/// <summary>Occurs when a sound instance is stopped and removed from the bank.</summary>
	public Action<SoundBank, SoundInstance> OnInstanceRemoved;

	/// <summary>
	/// Gets a read-only dictionary mapping each <see cref="Sound"/> to its active <see cref="SoundInstance"/>s.
	/// </summary>
	/// <remarks>
	/// This property flattens the internal collection of <see cref="SoundInstanceWrapped"/> objects into their underlying
	/// <see cref="SoundInstance"/> values, filtering out any null entries.  
	/// A new dictionary is constructed on each access to ensure immutability and encapsulation of the internal state.  
	/// Consumers receive a <see cref="ReadOnlyDictionary{TKey,TValue}"/> that cannot be modified.
	/// </remarks>
	/// <returns>
	/// A <see cref="ReadOnlyDictionary{TKey,TValue}"/> where each key is a <see cref="Sound"/> and each value is a
	/// <see cref="List{T}"/> of active <see cref="SoundInstance"/>s associated with that sound.
	/// </returns>
	public ReadOnlyDictionary<Sound, List<SoundInstance>> Instances
	{
		get
		{
			Dictionary<Sound, List<SoundInstance>> flattened = new(_instances.Count);

			foreach (var pair in _instances)
			{
				Sound key = pair.Key;
				List<SoundInstanceWrapped> wrapper = pair.Value;
				List<SoundInstance> instanceList = new(pair.Value.Count);

				foreach (var wrapped in wrapper)
				{
					if (wrapped?.Instance != null)
						instanceList.Add(wrapped.Instance);
				}

				flattened[key] = instanceList;
			}

			return new ReadOnlyDictionary<Sound, List<SoundInstance>>(flattened);
		}
	}

	/// <summary>
	/// Gets the number of sound instances that are currently valid.
	/// </summary>
	/// <remarks>
	/// This property flattens all <see cref="SoundInstanceWrapped"/> collections in <c>_instances</c> and counts
	/// the number of entries where <see cref="SoundInstance.IsValid"/> is <c>true</c>.
	/// It provides a quick way to determine how many sound instances are currently active and usable.
	/// </remarks>
	public int Count => _instances
		.SelectMany(x => x.Value)
		.Count(x => x.Instance.IsPlaying);

	/// <summary>
	/// Gets the unique identifier assigned to this sound manager or entity.
	/// </summary>
	/// <remarks>
	/// The identifier is set internally and exposed as a read-only property.  
	/// It can be used to distinguish between multiple sound managers or entities in the system.
	/// </remarks>
	public uint Id { get; private set; }

	/// <summary>
	/// Gets or sets the stereo pan value for this sound instance.
	/// </summary>
	/// <remarks>
	/// The value is clamped between -1.0 (full left) and 1.0 (full right).  
	/// Setting this property triggers <c>Update()</c> to apply the change.
	/// </remarks>
	public float Pan
	{
		get => _pan;
		set
		{
			if (MathF.Abs(_pan - value) < 0.001f)
				return;
			_pan = Math.Clamp(value, -1f, 1f);

			if (Id == 0) return;

			foreach (var soundPair in _instances)
			{
				foreach (var wrapped in soundPair.Value)
				{
					wrapped.Instance.Pan = _pan;
				}
			}

			Logger.Instance.Log(LogLevel.Info, $"SoundBank {Id} pan set to {_pan}");
		}
	}

	/// <summary>
	/// Gets or sets the volume level for this sound instance.
	/// </summary>
	/// <remarks>
	/// The value is clamped between 0.0 (silent) and 1.0 (full volume).  
	/// Setting this property triggers <c>Update()</c> to apply the change.
	/// </remarks>
	public float Volume
	{
		get => _volume;
		set
		{
			if (MathF.Abs(_volume - value) < 0.001f)
				return;
			_volume = Math.Clamp(value, 0f, 1f);

			if (Id > 0)
			{
				foreach (var soundPair in _instances)
				{
					foreach (var wrapped in soundPair.Value)
					{
						wrapped.Instance.Volume = _volume;
					}
				}
			}
			else
				SFML.Audio.Listener.GlobalVolume = Math.Clamp(_volume * 100f, 0f, 100f);

			Logger.Instance.Log(LogLevel.Info, $"SoundBank {Id} volume set to {_volume}");
		}
	}

	/// <summary>
	/// Gets or sets the pitch adjustment for this sound instance.
	/// </summary>
	/// <remarks>
	/// The value is clamped between -3.0 and 3.0, where 0.0 represents the original pitch.  
	/// Negative values lower the pitch, positive values raise it.  
	/// Setting this property triggers <c>Update()</c> to apply the change.
	/// </remarks>
	public float Pitch
	{
		get => _pitch;
		set
		{
			if (MathF.Abs(_pitch - value) < 0.001f)
				return;
			_pitch = Math.Clamp(value, -3f, 3f);

			if (Id == 0) return;

			foreach (var soundPair in _instances)
			{
				foreach (var wrapped in soundPair.Value)
				{
					wrapped.Instance.Pitch = _pitch;
				}
			}

			Logger.Instance.Log(LogLevel.Info, $"SoundBank {Id} pitch set to {_pitch}");
		}
	}

	internal bool Clear()
	{
		bool anyRemoved = false;
		int count = 0;

		foreach (var kv in _instances)
		{
			if (kv.Value == null || kv.Value.Count == 0)
				continue;

			for (int i = kv.Value.Count - 1; i >= 0; i--)
			{
				var item = kv.Value[i];
				if (item == null) continue;

				UnsubscribeFromInstance(item.Instance);

				item.Instance.Stop();
				item.Instance.Dispose();

				anyRemoved = true;
				count++;
			}
		}
		_instances.Clear();

		Logger.Instance.Log(LogLevel.Info, $"Sound channel {Id} cleared {count} sound instances.");

		return anyRemoved;
	}

	private void PerformQuickMaintenance()
	{
		DateTime now = DateTime.Now;
		if ((now - _lastMaintenceCheck).TotalSeconds < 30)
			return;

		_lastMaintenceCheck = now;

		int removedCount = 0;
		TimeSpan threshold = TimeSpan.FromMinutes(_evictAfterMinutes);

		var toRemove = new List<(Sound, SoundInstanceWrapped)>();
		foreach (var soundPair in _instances)
		{
			var instances = soundPair.Value;
			for (int i = instances.Count - 1; i >= 0; i--)
			{
				var wrapped = instances[i];

				if (!wrapped.Instance.IsValid || (now - wrapped.LastAccessFrame) >= threshold)
				{
					// wrapped.Instance.Dispose();
					// instances.RemoveAt(i);
					toRemove.Add((soundPair.Key, wrapped));
					// removedCount++;
				}
			}

			// if (instances.Count == 0)
			// {
			// 	_instances.Remove(soundPair.Key);
			// }
		}

		// Now perform the removals
		foreach (var (sound, wrapped) in toRemove)
		{
			if (!_instances.TryGetValue(sound, out var instances))
				continue;

			wrapped.Instance.Dispose();
			instances.Remove(wrapped);
			removedCount++;

			if (instances.Count == 0)
				_instances.Remove(sound);
		}

		if (removedCount > 0)
		{
			Logger.Instance.Log(LogLevel.Info,
				$"SoundBank {Id} safety cleanup removed {removedCount} instances");
		}
	}

	internal SoundInstance Add(Sound sound)
	{
		if (sound == null)
			return null;
		if (!_instances.TryGetValue(sound, out var instances))
		{
			instances = [];
			_instances[sound] = instances;
		}

		// clear dead old instances:
		PerformQuickMaintenance();
		// EvictSound(instances);

		var inst = sound.CreateInstance();

		SubscribeToInstance(inst);

		inst.Volume = Volume;
		inst.Pan = Pan;
		inst.Pitch = Pitch;

		instances.Add(new SoundInstanceWrapped { Instance = inst, LastAccessFrame = DateTime.Now });

		OnInstanceAdded?.Invoke(this, inst);

		inst.Play();

		Logger.Instance.Log(LogLevel.Info, $"Sound channel ID {Id} added instance ID {inst.Id} from sound ID {sound.Id}.");

		return inst;
	}

	internal bool Remove(Sound sound)
	{
		if (_instances.Count == 0)
			return false;
		if (!_instances.TryGetValue(sound, out var inst))
			return false;

		for (int i = inst.Count - 1; i >= 0; i--)
		{
			var item = inst[i];
			if (item == null) continue;

			item.Instance.Stop();
			item.Instance.Dispose();
			UnsubscribeFromInstance(item.Instance);
		}

		var removed = _instances.Remove(sound);

		Logger.Instance.Log(LogLevel.Info, $"Sound channel {Id} removed sound ID {sound.Id}.");

		return removed;
	}

	internal SoundBank(uint id, float evictAfterMinutes, float volume = 1f, float pan = 0f, float pitch = 1f)
	{
		Id = id;
		_evictAfterMinutes = evictAfterMinutes;

		Volume = volume;
		Pan = pan;
		Pitch = pitch;
	}

	internal bool IsSoundPlaying(Sound sound)
	{
		if (_instances.Count == 0)
			return false;
		if (!_instances.TryGetValue(sound, out var instances))
			return false;

		return instances.Any(x => x.Instance.IsPlaying);
	}

	internal SoundInstance GetSound(Sound sound)
	{
		if (sound == null)
			throw new Exception();
		if (!_instances.TryGetValue(sound, out var instances))
			return null;

		return instances
			.Where(x => x.Instance.IsPlaying)
			.Select(x => x.Instance)
			.FirstOrDefault();
	}




	private void SubscribeToInstance(SoundInstance instance)
	{
		instance.OnPlaybackFinished += HandleSoundFinished;
		instance.OnDisposed += HandleSoundDisposed;
	}
	private void UnsubscribeFromInstance(SoundInstance instance)
	{
		instance.OnPlaybackFinished -= HandleSoundFinished;
		instance.OnDisposed -= HandleSoundDisposed;
	}

	private void HandleSoundFinished(SoundInstance instance)
	{
		// O(1) removal instead of scanning loops!
		RemoveInstanceDirect(instance);

		OnInstanceRemoved?.Invoke(this, instance);

		Logger.Instance.Log(LogLevel.Info,
			$"Sound {instance.Id} finished playing in bank {Id}");
	}

	private void HandleSoundDisposed(SoundInstance instance)
	{
		RemoveInstanceDirect(instance);
	}

	private bool RemoveInstanceDirect(SoundInstance instance)
	{
		bool removed = false;
		var keysToRemove = new List<Sound>();

		foreach (var kv in _instances)
		{
			var instances = kv.Value;
			for (int i = instances.Count - 1; i >= 0; i--)
			{
				var wrapped = instances[i];

				// Skip if wrapper is null
				if (wrapped == null)
				{
					instances.RemoveAt(i);
					continue;
				}

				// Skip if instance was recycled by pool (now belongs to different sound)
				if (wrapped.Instance == null || wrapped.Instance.Sound == null ||
					wrapped.Instance.Sound.Id != kv.Key.Id)
				{
					instances.RemoveAt(i);
					continue;
				}

				if (instances[i].Instance == instance)
				{
					instances.RemoveAt(i);  // Remove FIRST
					removed = true;

					if (instances.Count == 0)
					{
						keysToRemove.Add(kv.Key);
					}

					UnsubscribeFromInstance(instance);  // Unsubscribe AFTER

					break;
				}
			}

			if (removed) break;
		}

		foreach (var key in keysToRemove)
			_instances.Remove(key);

		return removed;
	}
}
