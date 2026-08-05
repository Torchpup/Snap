namespace Snap.Engine.Saves;

/// <summary>
/// Contains metadata for serialized content, including checksum, compression status, timestamp, and version.
/// </summary>
public sealed class ContentTypeWriterReaderMetadata
{
	/// <summary>
	/// Gets the hexadecimal checksum of the serialized data.
	/// </summary>
	public string Checksum { get; internal set; }

	/// <summary>
	/// Gets a value indicating whether the serialized data is compressed.
	/// </summary>
	public bool IsCompressed { get; internal set; }

	/// <summary>
	/// Gets the UTC timestamp when the data was serialized.
	/// </summary>
	public DateTime Timestamp { get; internal set; }

	/// <summary>
	/// Gets the version of the serialized data format.
	/// </summary>
	public int Version { get; internal set; }

	internal ContentTypeWriterReaderMetadata() { }

	/// <summary>
	/// Creates a new instance of <see cref="ContentTypeWriterReaderMetadata"/> for the given data.
	/// </summary>
	/// <param name="data">The data to generate metadata for.</param>
	/// <param name="isCompressed">Whether the data is compressed.</param>
	/// <param name="version">The version of the serialized data format. Defaults to 1.</param>
	/// <returns>A new <see cref="ContentTypeWriterReaderMetadata"/> instance.</returns>
	public static ContentTypeWriterReaderMetadata Create(byte[] data, bool isCompressed, int version = 1)
	{
		if (data == null)
			throw new ArgumentNullException(nameof(data), "Data cannot be null.");

		if (data.Length == 0)
			throw new ArgumentException("Data cannot be empty.", nameof(data));

		return new ContentTypeWriterReaderMetadata
		{
			Checksum = ComputeChecksumHex(data),
			IsCompressed = isCompressed,
			Timestamp = DateTime.UtcNow,
			Version = version
		};
	}

	/// <summary>
	/// Computes a hexadecimal SHA256 checksum for the given data.
	/// </summary>
	/// <param name="data">The data to compute the checksum for.</param>
	/// <returns>The checksum as a hexadecimal string, prefixed with "0x".</returns>
	public static string ComputeChecksumHex(byte[] data)
	{
		byte[] hash = SHA256.HashData(data);
		return $"0x{Convert.ToHexString(hash).ToUpper()}";
	}
}

/// <summary>
/// Provides abstract base functionality for reading and writing content of type <typeparamref name="T"/> 
/// with optional AES encryption, Deflate compression, and SHA256/HMAC integrity verification.
/// </summary>
/// <typeparam name="T">The type of content to serialize/deserialize.</typeparam>
public abstract class ContentTypeWriterReader<T>
{
	private const int HmacSize = 32; // SHA256 produces 32 bytes

	private readonly byte[] _magicHeader = [0x53, 0x4E, 0x41, 0x50]; // SNAP
	private readonly string _encryptionKey;

	/// <summary>
	/// Gets the metadata associated with the last save/load operation.
	/// </summary>
	public ContentTypeWriterReaderMetadata Metadata { get; internal set; }

	/// <summary>
	/// Override to control compression level. Default is Optimal.
	/// </summary>
	protected virtual System.IO.Compression.CompressionLevel GetCompressionLevel() => System.IO.Compression.CompressionLevel.Optimal;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContentTypeWriterReader{T}"/> class.
	/// </summary>
	/// <param name="encryptionKey">Optional encryption key. If <see langword="null"/>, data will not be encrypted.</param>
	protected ContentTypeWriterReader(string encryptionKey = null)
	{
		_encryptionKey = encryptionKey;
	}

	/// <summary>
	/// When implemented in a derived class, writes the specified value to the provided writer.
	/// </summary>
	/// <param name="value">The value to write.</param>
	/// <param name="writer">The writer to use for serialization.</param>
	public abstract void Write(T value, ContentTypeWriter writer);

	/// <summary>
	/// When implemented in a derived class, reads a value of type <typeparamref name="T"/> from the provided reader.
	/// </summary>
	/// <param name="reader">The reader to use for deserialization.</param>
	/// <returns>The deserialized value.</returns>
	public abstract T Read(ContentTypeReader reader);

	/// <summary>
	/// Saves the specified value to a file.
	/// </summary>
	/// <param name="filename">The relative filename to save to. Must not be rooted or contain path traversal characters.</param>
	/// <param name="saveFile">The value to save.</param>
	/// <exception cref="UnauthorizedAccessException">
	/// Thrown if the filename is rooted or attempts to escape the save directory.
	/// </exception>
	public void Save(string filename, T saveFile)
	{
		using MemoryStream ms = new();
		using ContentTypeWriter writer = new(ms);

		// Serialize all data:
		Write(saveFile, writer);
		byte[] rawData = ms.ToArray();

		// Encryption
		byte[] encryptionData = EncryptData(rawData);

		// Compression:
		byte[] compressedData = CompressData(encryptionData);
		bool useCompression = compressedData.Length < encryptionData.Length;
		byte[] finalData = useCompression ? compressedData : encryptionData;

		ContentTypeWriterReaderMetadata metadata =
			ContentTypeWriterReaderMetadata.Create(finalData, useCompression, version: 1);

		using MemoryStream headerStream = new();
		using BinaryWriter headerWriter = new(headerStream);

		headerWriter.Write(_magicHeader);
		headerWriter.Write(metadata.Checksum);
		headerWriter.Write(metadata.IsCompressed);
		headerWriter.Write(metadata.Timestamp.Ticks);
		headerWriter.Write(metadata.Version);
		headerWriter.Write(finalData);

		// File.WriteAllBytes(CreateFinalPath(filename), headerStream.ToArray());
		byte[] finalBytes = headerStream.ToArray();
		string finalPath = CreateFinalPath(filename);
		string tempPath = finalPath + ".tmp";

		using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			fs.Write(finalBytes, 0, finalBytes.Length);
		}
		File.Move(tempPath, finalPath, overwrite: true);

		Metadata = metadata;
	}

	private string CreateFinalPath(string filename)
	{
		if (Path.IsPathRooted(filename))
			throw new UnauthorizedAccessException("Save filename must be relative.");

		var invalid = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).ToArray();
		var safeName = new string(filename.Where(c => !invalid.Contains(c)).ToArray());

		var baseDir = Game.Instance.ApplicationSaveFolder;
		var combined = Path.Combine(baseDir, safeName);
		var fullPath = Path.GetFullPath(combined);
		var normalizedBase = Path.GetFullPath(baseDir)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;

		if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
		{
			throw new UnauthorizedAccessException(
				$"Invalid save path: '{filename}'. Cannot escape save directory.");
		}

		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		return fullPath;
	}

	/// <summary>
	/// Loads a value of type <typeparamref name="T"/> from the specified file.
	/// </summary>
	/// <param name="filename">The relative filename to load from. Must not be rooted or contain path traversal characters.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="InvalidDataException">
	/// Thrown if the file is not a valid save file, the checksum is invalid, or the file is corrupted.
	/// </exception>
	/// <exception cref="EndOfStreamException">
	/// Thrown if the file is truncated or incomplete.
	/// </exception>
	/// <exception cref="UnauthorizedAccessException">
	/// Thrown if the filename is rooted or attempts to escape the save directory.
	/// </exception>
	public T Load(string filename)
	{
		byte[] fileData;
		using (var fs = new FileStream(CreateFinalPath(filename), FileMode.Open, FileAccess.Read, FileShare.Read))
		{
			fileData = new byte[fs.Length];
			int bytesRead = 0;
			while (bytesRead < fileData.Length)
			{
				int read = fs.Read(fileData, bytesRead, fileData.Length - bytesRead);
				if (read == 0)
					throw new EndOfStreamException("Unexpected end of file.");
				bytesRead += read;
			}
		}

		using MemoryStream memoryStream = new(fileData);
		using BinaryReader reader = new(memoryStream);

		byte[] magic = reader.ReadBytes(4);
		if (!magic.SequenceEqual(_magicHeader))
			throw new InvalidDataException("Invalid save file - magic header mismatch.");

		string checksum = reader.ReadString();
		bool isCompressed = reader.ReadBoolean();
		long timestamp = reader.ReadInt64();
		int version = reader.ReadInt32();
		byte[] rawData = reader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));

		// Verify Integrity
		string computedChecksum = ContentTypeWriterReaderMetadata.ComputeChecksumHex(rawData);
		if (checksum != computedChecksum)
			throw new InvalidDataException("Save file corrupted - checksum verification failed.");

		// Compress if necessary
		byte[] finalData = isCompressed ? DecompressData(rawData) : rawData;

		// Decrypt:
		byte[] decryptedData = DecryptData(finalData);

		using MemoryStream ms = new(decryptedData);
		using ContentTypeReader readerContent = new(ms);

		Metadata = new ContentTypeWriterReaderMetadata
		{
			Checksum = checksum,
			IsCompressed = isCompressed,
			Timestamp = new DateTime(timestamp, DateTimeKind.Utc),
			Version = version
		};

		return Read(readerContent);
	}

	private byte[] EncryptData(byte[] rawData)
	{
		if (string.IsNullOrEmpty(_encryptionKey))
			return rawData;

		using Aes aes = Aes.Create();

		// Generate random salt
		byte[] salt = new byte[32];
		using (var rng = RandomNumberGenerator.Create())
		{
			rng.GetBytes(salt);
		}

		var key = GenerateKey(_encryptionKey, salt);
		aes.Key = key;
		aes.GenerateIV();

		using ICryptoTransform encryptor = aes.CreateEncryptor();
		using MemoryStream ms = new();

		// Write salt first, then IV
		ms.Write(salt, 0, salt.Length);
		ms.Write(aes.IV, 0, aes.IV.Length);

		// Encrypt the data
		using var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
		cryptoStream.Write(rawData, 0, rawData.Length);
		cryptoStream.FlushFinalBlock();

		// Get the encrypted data
		byte[] encryptedData = ms.ToArray();

		// Compute HMAC over salt + IV + encrypted data
		byte[] hmac = ComputeHmac(key, encryptedData);

		// Combine HMAC + encrypted data
		using MemoryStream result = new();
		result.Write(hmac, 0, hmac.Length);
		result.Write(encryptedData, 0, encryptedData.Length);

		return result.ToArray();
	}

	private byte[] DecryptData(byte[] encryptedData)
	{
		if (string.IsNullOrEmpty(_encryptionKey)) return encryptedData;

		using MemoryStream ms = new(encryptedData);

		// Read HMAC
		byte[] hmac = new byte[HmacSize];
		ms.Read(hmac, 0, hmac.Length);

		// Read salt
		byte[] salt = new byte[32];
		ms.Read(salt, 0, salt.Length);

		// Read IV
		byte[] iv = new byte[16];
		ms.Read(iv, 0, iv.Length);

		// Get the remaining encrypted data
		byte[] ciphertext = new byte[ms.Length - ms.Position];
		ms.Read(ciphertext, 0, ciphertext.Length);

		// Reconstruct the data for HMAC verification
		using MemoryStream hmacData = new();
		hmacData.Write(salt, 0, salt.Length);
		hmacData.Write(iv, 0, iv.Length);
		hmacData.Write(ciphertext, 0, ciphertext.Length);

		var key = GenerateKey(_encryptionKey, salt);

		// Verify HMAC
		byte[] computedHmac = ComputeHmac(key, hmacData.ToArray());
		if (!hmac.SequenceEqual(computedHmac))
			throw new InvalidDataException("Save file integrity check failed. The file may have been tampered with or corrupted.");

		using Aes aes = Aes.Create();
		aes.Key = key;
		aes.IV = iv;

		using ICryptoTransform decryptor = aes.CreateDecryptor();
		using MemoryStream outputStream = new();
		using (CryptoStream cryptoStream = new(new MemoryStream(ciphertext), decryptor, CryptoStreamMode.Read))
		{
			cryptoStream.CopyTo(outputStream);
		}

		return outputStream.ToArray();
	}

	private byte[] ComputeHmac(byte[] key, byte[] data)
	{
		using var hmac = new HMACSHA256(key);
		return hmac.ComputeHash(data);
	}

	private byte[] CompressData(byte[] data)
	{
		using MemoryStream output = new();
		var compressionLevel = GetCompressionLevel();
		using (DeflateStream compressionStream = new(output, compressionLevel, true))
		{
			compressionStream.Write(data, 0, data.Length);
		}
		return output.ToArray();
	}

	private byte[] DecompressData(byte[] data)
	{
		using MemoryStream input = new(data);
		using MemoryStream output = new();
		using DeflateStream decompressStream = new(input, CompressionMode.Decompress);

		decompressStream.CopyTo(output);

		return output.ToArray();
	}

	private static byte[] GenerateKey(string passPhrase, byte[] salt)
	{
		return Rfc2898DeriveBytes.Pbkdf2(
			passPhrase,
			salt,
			100000,
			HashAlgorithmName.SHA256,
			32 // 32 bytes = 256 bits for AES-256
		);
	}
}
