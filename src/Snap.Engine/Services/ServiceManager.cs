namespace Snap.Engine.Services;

/// <summary>
/// Base class for all game services.
/// </summary>
public class Service
{
	/// <summary>
	/// Initializes the service. Override this method to provide custom initialization logic.
	/// </summary>
	public virtual void Iniitialize() { }
}

/// <summary>
/// Manages the registration and retrieval of game services using a singleton pattern.
/// </summary>
public sealed class ServiceManager
{
	private readonly Dictionary<Type, Service> _services = new(10);

	/// <summary>
	/// Gets the singleton instance of the <see cref="ServiceManager"/>.
	/// </summary>
	public static ServiceManager Instance { get; private set; }

	/// <summary>
	/// Gets the number of registered services.
	/// </summary>
	public int Count => _services.Count;

	internal ServiceManager() => Instance ??= this;

	/// <summary>
	/// Registers a game service with the manager.
	/// </summary>
	/// <param name="service">The service to register. Must not be <see langword="null"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="service"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown if a service of the same type is already registered.</exception>
	/// <remarks>
	/// This method also initializes the service via <see cref="Service.Iniitialize"/> and notifies the <see cref="BeaconManager"/>.
	/// </remarks>
	public void RegisterService(Service service)
	{
		if (service == null)
			throw new ArgumentNullException(nameof(service));
		if (_services.ContainsKey(service.GetType()))
			throw new ArgumentException(nameof(service));

		_services[service.GetType()] = service;

		BeaconManager.Initialize(service);
		service?.Iniitialize();
	}

	/// <summary>
	/// Retrieves a service of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of the service to retrieve.</typeparam>
	/// <returns>The requested service.</returns>
	public T GetService<T>() where T : Service
	{
		if (!_services.TryGetValue(typeof(T), out var service))
			return default;

		return (T)service;
	}

	/// <summary>
	/// Attempts to retrieve a service of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of the service to retrieve.</typeparam>
	/// <param name="service">When this method returns, contains the service of type <typeparamref name="T"/>, or <see langword="null"/> if not found.</param>
	/// <returns><see langword="true"/> if the service was found; otherwise, <see langword="false"/>.</returns>
	public bool TryGetService<T>(out T service) where T : Service
	{
		service = GetService<T>();

		return service != null;
	}
}
