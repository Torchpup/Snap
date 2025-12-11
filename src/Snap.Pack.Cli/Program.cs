using Snap.Pack.Core;

static int Usage()
{
	Console.WriteLine("Usage:");
	Console.WriteLine("  snap-pack build <inputDir> -o <outFile> [--brotli|--deflate|--no-compress] [--min-savings <0..1>]");
	Console.WriteLine("  snap-pack list <pakFile>");
	Console.WriteLine("  snap-pack extract <pakFile> -o <outDir>");
	Console.WriteLine("  snap-pack verify <pakFile>");
	return 1;
}

if (args.Length == 0) return Usage();

switch (args[0])
{
	case "build":
		{
			var input = args.ElementAtOrDefault(1) ?? ".";
			string outFile = "Content/game.spack";
			int oi = Array.IndexOf(args, "-o");
			if (oi >= 0 && oi + 1 < args.Length) outFile = args[oi + 1];

			bool brotli = args.Contains("--brotli");
			bool deflate = args.Contains("--deflate");
			bool noComp = args.Contains("--no-compress");

			// default: brotli unless user said otherwise
			if (noComp) { brotli = false; deflate = false; }
			else if (!brotli && !deflate) { brotli = true; }

			double minSavings = 0.03;
			int msIdx = Array.IndexOf(args, "--min-savings");
			if (msIdx >= 0 && msIdx + 1 < args.Length && double.TryParse(args[msIdx + 1], out var v))
				minSavings = Math.Clamp(v, 0, 1);

			// encryption flags
			bool encrypt = args.Contains("--encrypt");
			byte[]? key = null;
			int keyIdx = Array.IndexOf(args, "--key");
			if (keyIdx >= 0 && keyIdx + 1 < args.Length)
			{
				var hex = args[keyIdx + 1];
				try
				{
					key = Convert.FromHexString(hex);
				}
				catch
				{
					Console.WriteLine("Invalid key format. Must be a valid hex string.");
					return 1;
				}
			}

			if (encrypt && (key == null || key.Length != 32))
			{
				Console.WriteLine("Encrypt=true requires --key <64 hex chars for 32 bytes AES-256 key>");
				return 1;
			}

			PackBuilder.Build(new Snap.Pack.Core.PackOptions
			{
				InputDir = input,
				OutFile = outFile,
				UseBrotli = brotli,
				UseDeflate = deflate,
				MinSavingsRatio = minSavings,
				Encrypt = encrypt,
				Key = key
			});

			Console.WriteLine($"Built {outFile}");
			return 0;
		}

	case "list":
		{
			var pak = args.ElementAtOrDefault(1);
			if (pak is null) return Usage();
			PackBuilder.List(pak, Console.Out);
			return 0;
		}

	case "extract":
		{
			var pak = args.ElementAtOrDefault(1);
			if (pak is null) return Usage();
			string outDir = "extract";
			int oi = Array.IndexOf(args, "-o");
			if (oi >= 0 && oi + 1 < args.Length) outDir = args[oi + 1];

			PackBuilder.ExtractAll(pak, outDir);
			Console.WriteLine($"Extracted to {outDir}");
			return 0;
		}

	case "verify":
		{
			var pak = args.ElementAtOrDefault(1);
			if (pak is null) return Usage();

			byte[]? key = null;
			int keyIdx = Array.IndexOf(args, "--key");
			if (keyIdx >= 0 && keyIdx + 1 < args.Length)
				key = Convert.FromHexString(args[keyIdx + 1]);

			PackReader.Verify(pak, Console.Out, key);
			return 0;
		}

	default:
		return Usage();
}
