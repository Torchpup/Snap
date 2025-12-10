namespace Snap.Engine.Sounds;

public enum SoundStatus
{
	Stopped,
	Paused,
	Playing
}

public class SoundInstance : IDisposable
{
	#region Fields
	private uint _soundId;
	private SFSound _sound;
	private float _volume, _pitch, _pan;
	private bool _isDisposed;
	#endregion

	#region Properties
	public uint Id { get; private set; }
	public bool IsPlaying => Status != SoundStatus.Stopped;
	public bool IsValid => _sound != null && !_sound.IsInvalid && _sound.Status != SFSoundStatus.Stopped;
	public SoundStatus Status => IsValid ? (SoundStatus)_sound.Status : SoundStatus.Stopped;

	public float Pan
	{
		get => _pan;
		set
		{
			if (_pan == value)
				return;
			_pan = Math.Clamp(value, -1f, 1f);

			if (IsValid)
			{
				if (_pan < 0f || _pan > 0f)
				{
					_sound.RelativeToListener = true;
					_sound.Attenuation = 0f;
					_sound.MinDistance = 0f;
				}
				else
				{
					_sound.RelativeToListener = false;
					_sound.Attenuation = 1f;
					_sound.MinDistance = 1f;
				}

				_sound.Position = new(_pan, 0, 0);
			}
		}
	}

	public float Volume
	{
		get => _volume;
		set
		{
			if (_volume == value)
				return;
			_volume = Math.Clamp(value, 0f, 1f);

			if (IsValid)
				_sound.Volume = Math.Clamp(_volume * 100f, 0f, 100f);
		}
	}

	public float Pitch
	{
		get => _pitch;
		set
		{
			if (_pitch == value)
				return;
			_pitch = Math.Clamp(value, -3f, 3f);

			if (IsValid)
				_sound.Pitch = _pitch;
		}
	}


	#endregion


	#region Constructor / Deconstructor
	internal SoundInstance(uint id, Sound sound, SFSoundBuffer buffer)
	{
		Id = id;
		_soundId = sound.Id;

		_sound = new SFSound(buffer)
		{
			// set volume to zero so it doesnt make any pops/weird 
			// sounds when it is intialized
			Volume = 0f,
			Loop = sound.IsLooped
		};
	}

	~SoundInstance() => Dispose();
	#endregion


	#region Play, Pause, Resume, Stop
	public void Play()
	{
		if (IsPlaying)
			return;

		if (_sound.IsInvalid)
			_sound = new(_sound.SoundBuffer);

		_sound.Volume = Math.Clamp(_volume * 100f, 0f, 100f);
		_sound.Position = new(_pan, 0, 0);
		_sound.Pitch = _pitch;
		_sound.Play();
	}

	public void Pause()
	{
		if (!IsValid || Status != SoundStatus.Playing) return;
		_sound.Pause();
	}

	public void Resume()
	{
		if (!IsValid || Status != SoundStatus.Paused) return;
		_sound.Play();
	}

	public void Stop()
	{
		if (!IsValid || Status == SoundStatus.Stopped) return;
		_sound.Stop();
	}
	#endregion


	#region IDispose
	protected virtual void Dispose(bool disposing)
	{
		if (!_isDisposed)
		{
			_sound.Dispose();
			Logger.Instance.Log(LogLevel.Info, $"Unloaded asset with ID {Id}, type: '{GetType().Name}' from Sound ID {_soundId}.");

			_isDisposed = true;
		}
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
	#endregion
}
