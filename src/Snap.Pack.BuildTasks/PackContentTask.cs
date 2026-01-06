using System.ComponentModel.DataAnnotations;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Snap.Pack.Core;

namespace Snap.Pack.BuildTasks;

public sealed class PackContentTask : Microsoft.Build.Utilities.Task
{
	[Required] public string InputDir { get; set; } = "";
	[Required] public string OutFile { get; set; } = "";
	public string Compress { get; set; } = "brotli";
	public string MinSavings { get; set; } = "0.03";
	public string? KeyHex { get; set; }
	public bool Overwrite { get; set; } = true;

	public override bool Execute()
	{
		try
		{
			var opts = new PackOptions
			{
				InputDir = InputDir,
				OutFile = OutFile,
				Overwrite = Overwrite,
				UseBrotli = string.Equals(Compress, "brotli", StringComparison.OrdinalIgnoreCase),
				UseDeflate = string.Equals(Compress, "deflate", StringComparison.OrdinalIgnoreCase),
				MinSavingsRatio = double.TryParse(MinSavings, out var r) ? r : 0.03,
				Encrypt = !string.IsNullOrWhiteSpace(KeyHex),
				Key = string.IsNullOrWhiteSpace(KeyHex) ? null : Convert.FromHexString(KeyHex!)
			};

			PackBuilder.Build(opts);
			Log.LogMessage(MessageImportance.High, $"Built pack: {OutFile}");
			return true;
		}
		catch (Exception ex)
		{
			Log.LogErrorFromException(ex, true);
			return false;
		}
	}
}
