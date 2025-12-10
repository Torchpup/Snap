namespace Snap.Engine.Sounds;

public sealed class SoundManager
{
	private const int MaxSoundBanks = 16;

	private readonly Dictionary<uint, SoundBank> _banks = new(MaxSoundBanks);

	public static SoundManager Instance { get; private set; }
	public int Count => _banks.Count;
	public int PlayCount => _banks.Sum(x => x.Value.Count);
	public float EvictAfterMinutes { get; set; } = 15f;

	internal SoundManager()
	{
		Instance ??= this;

		// create master channel....
		_banks.Add(0, new SoundBank(0, EvictAfterMinutes));
	}


	public void AddBank(Enum bankId, float volume = 1f, float pan = 0f, float pitch = 1f) =>
		AddBank(Convert.ToUInt32(bankId), volume, pan, pitch);
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

	public bool RemoveBank(Enum bankId) => RemoveBank(Convert.ToUInt32(bankId));
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

	public SoundInstance Play(Enum bankId, Sound sound) => Play(Convert.ToUInt32(bankId), sound);
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

	public SoundBank GetBank(Enum bankId) => GetBank(Convert.ToUInt32(bankId));
	public SoundBank GetBank(uint bankId)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception($"Bank ID: {bankId} not found in sound banks.");

		return bank;
	}

	public bool TryGetBank(Enum bankId, out SoundBank bank) => TryGetBank(Convert.ToUInt32(bankId), out bank);
	public bool TryGetBank(uint bankId, out SoundBank bank)
	{
		bank = GetBank(bankId);

		return bank != null;
	}


	public bool StopAll(Enum bankId) => StopAll(Convert.ToUInt32(bankId));
	public bool StopAll(uint bankId)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception();

		Logger.Instance.Log(LogLevel.Info, $"Sound channel ID: {bankId} stopped all sounds.");

		return bank.Clear();
	}

	public bool Stop(Enum bankId, Sound sound) => Stop(Convert.ToUInt32(bankId), sound);
	public bool Stop(uint bankId, Sound sound)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception();

		Logger.Instance.Log(LogLevel.Info,
			$"Sound channel ID {bankId} stopped sound ID {sound.Id}.");

		return bank.Remove(sound);
	}

	public bool IsPlayingConditonal(Enum bankId, Sound input, out Sound output) =>
		IsPlayingConditonal(Convert.ToUInt32(bankId), input, out output);
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

	public bool IsPlaying(Enum bankId, Sound sound) => IsPlaying(Convert.ToUInt32(bankId), sound);
	public bool IsPlaying(uint bankId, Sound sound)
	{
		if (!_banks.TryGetValue(bankId, out var bank))
			throw new Exception($"Bank ID {bankId} not found in sound banks.");

		return bank.IsSoundPlaying(sound);
	}

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
