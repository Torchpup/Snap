using Snap.Content.Abstractions.Interfaces;

namespace Snap.Content.Abstractions;

public sealed class FileSystemContentProvider : IContentProvider
{
	private readonly string _rootAbs;

	public FileSystemContentProvider(string root)
	{
		if (string.IsNullOrWhiteSpace(root)) root = "Content";
		_rootAbs = Path.GetFullPath(
			Path.IsPathRooted(root) ? root : Path.Combine(AppContext.BaseDirectory, root));
	}

	private string MapFile(string logical)
	{
		if (string.IsNullOrWhiteSpace(logical))
			throw new ArgumentException(nameof(logical));

		var rel = logical.Replace('\\', '/').TrimStart('/');
		var full = Path.GetFullPath(Path.Combine(_rootAbs, rel.Replace('/', Path.DirectorySeparatorChar)));

		if (!full.StartsWith(_rootAbs, StringComparison.OrdinalIgnoreCase))
			throw new UnauthorizedAccessException($"Path escapes content root: {logical}");

		return full;
	}

	private string MapDirAllowRoot(string logicalOrEmpty)
	{
		if (string.IsNullOrWhiteSpace(logicalOrEmpty))
			return _rootAbs;

		var rel = logicalOrEmpty.Replace('\\', '/').TrimStart('/');
		var full = Path.GetFullPath(Path.Combine(_rootAbs, rel.Replace('/', Path.DirectorySeparatorChar)));

		if (!full.StartsWith(_rootAbs, StringComparison.OrdinalIgnoreCase))
			throw new UnauthorizedAccessException($"Path escapes content root: {logicalOrEmpty}");

		return full;
	}

	public bool Exists(string path) => File.Exists(MapFile(path));

	public Stream OpenRead(string path) =>
		new FileStream(MapFile(path), FileMode.Open, FileAccess.Read, FileShare.Read);

	public IEnumerable<string> List(string folder)
	{
		var dir = MapDirAllowRoot(folder);   // ← allow "" to mean the root
		if (!Directory.Exists(dir)) yield break;

		foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
		{
			// return logical, content-root-relative paths with forward slashes
			var rel = Path.GetRelativePath(_rootAbs, f)
						  .Replace('\\', '/')
						  .TrimStart('/');
			yield return rel;
		}
	}
}

