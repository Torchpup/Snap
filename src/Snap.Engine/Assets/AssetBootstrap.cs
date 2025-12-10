using Snap.Content.Abstractions;

namespace Snap.Engine.Assets;

/// <summary>
/// Provides utility methods for initializing and managing the content/asset system.
/// </summary>
public static class AssetBootstrap
{
	/// <summary>
	/// Initializes the content system with a default <see cref="FileSystemContentProvider"/>.
	/// This ensures that asset loading "just works" out of the box.
	/// </summary>
	/// <param name="contentRoot">
	/// The root directory for content, relative to the application base.
	/// If <c>null</c>, uses <see cref="EngineSettings.AppContentRoot"/>.
	/// </param>
	/// <param name="warnIfEmpty">
	/// If <c>true</c>, logs a warning if no files are found in the content root.
	/// </param>
	/// <remarks>
	/// This method sets up a <see cref="CompositeContentProvider"/> with a filesystem provider
	/// and registers it with the <see cref="AssetManager"/>. If the content root is empty,
	/// a warning is logged to guide first-time users.
	/// </remarks>
	public static void InitDefault(string contentRoot = null, bool warnIfEmpty = true)
	{
		contentRoot ??= EngineSettings.Instance.AppContentRoot;

		var composite = new CompositeContentProvider(
			new FileSystemContentProvider(contentRoot)
		);

		AssetManager.SetProvider(composite, disposeOld: true, clearCaches: true);

		if (warnIfEmpty)
		{
			// Soft check: don’t crash first-time users—just nudge them.
			if (!composite.List("").Any())
			{
				Logger.Instance?.Log(LogLevel.Warning,
					$"No content found under '{contentRoot}'. " +
					$"Add assets there (e.g., Graphics/, Sound/, etc.).");
			}
			else
			{
				Logger.Instance?.Log(LogLevel.Info,
					$"Content provider initialized (filesystem): '{contentRoot}'.");
			}
		}
	}

	/// <summary>
	/// Switches the content root to a new directory at runtime.
	/// The new provider is still filesystem-based.
	/// </summary>
	/// <param name="newContentRoot">
	/// The new root directory for content, relative to the application base.
	/// </param>
	/// <remarks>
	/// This method creates a new <see cref="CompositeContentProvider"/> with a filesystem provider
	/// for the specified directory and registers it with the <see cref="AssetManager"/>,
	/// disposing of the old provider and clearing caches.
	/// </remarks>
	public static void SwitchContentRoot(string newContentRoot)
	{
		var composite = new CompositeContentProvider(
			new FileSystemContentProvider(newContentRoot)
		);
		AssetManager.SetProvider(composite, disposeOld: true, clearCaches: true);
		Logger.Instance?.Log(LogLevel.Info, $"Switched content root to '{newContentRoot}'.");
	}
}