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
	private readonly float _evictAfterMinutes;
	private readonly Dictionary<Sound, List<SoundInstanceWrapped>> _instances = new(128);

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
	/// Gets the number of sound instances that are currently invalid.
	/// </summary>
	/// <remarks>
	/// This property flattens all <see cref="SoundInstanceWrapped"/> collections in <c>_instances</c> and counts
	/// the number of entries where <see cref="SoundInstance.IsValid"/> is <c>false</c>.  
	/// It provides a quick way to determine how many sound instances have expired or are no longer usable.
	/// </remarks>
	public int Count => _instances.SelectMany(x => x.Value).Count(x => !x.Instance.IsValid);

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
			if (_pan == value)
				return;
			_pan = Math.Clamp(value, -1f, 1f);

			Update();
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
			if (_volume == value)
				return;
			_volume = Math.Clamp(value, 0f, 1f);

			Update();
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
			if (_pitch == value)
				return;
			_pitch = Math.Clamp(value, -3f, 3f);

			Update();
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


	private void EvictSound(List<SoundInstanceWrapped> items)
	{
		if (items.Count == 0)
			return;

		DateTime now = DateTime.UtcNow;
		TimeSpan evictAfter = TimeSpan.FromMinutes(_evictAfterMinutes);
		var toEvict = new List<SoundInstanceWrapped>(items.Count);

		for (int i = items.Count - 1; i >= 0; i--)
		{
			var inst = items[i];

			if (inst.Instance.IsValid)
				continue;

			var age = now - inst.LastAccessFrame;
			if (age >= evictAfter)
			{
				inst.Instance.Dispose();
				toEvict.Add(inst);
			}
		}

		if (toEvict.Count > 0)
		{
			for (int i = toEvict.Count - 1; i >= 0; i--)
				items.Remove(toEvict[i]);

			Logger.Instance.Log(LogLevel.Info, $"Sound bank evicted {toEvict.Count} sound instances.");
		}
	}

	private void Update()
	{
		if (_instances.Count == 0)
			return;

		// flatten the dictionary into one big list of items
		var flat = _instances
			.SelectMany(x => x.Value)
			.ToList();

		EvictSound(flat);

		foreach (var item in flat)
		{
			item.Instance.Volume = _volume;
			item.Instance.Pan = _pan;
			item.Instance.Pitch = _pitch;
		}
	}

	internal SoundInstance Add(Sound sound)
	{
		if (sound == null)
			return null;
		if (!_instances.TryGetValue(sound, out var instances))
			_instances[sound] = instances = [];

		// clear dead old instances:
		EvictSound(instances);

		var inst = sound.CreateInstance();
		inst.Volume = Volume;
		inst.Pan = Pan;
		inst.Pitch = Pitch;
		inst.Play();

		instances.Add(new SoundInstanceWrapped { Instance = inst, LastAccessFrame = DateTime.UtcNow });

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
			if (item == null || item.Instance.IsValid) continue;

			item.Instance.Stop();
			item.Instance.Dispose();
		}

		Logger.Instance.Log(LogLevel.Info, $"Sound channel {Id} removed sound ID {sound.Id}.");

		return _instances.Remove(sound);
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
}
