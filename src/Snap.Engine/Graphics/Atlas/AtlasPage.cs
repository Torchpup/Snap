namespace Snap.Engine.Graphics.Atlas;

/// <summary>
/// Represents a single texture atlas page used for packing multiple smaller textures into one.
/// </summary>
public sealed class AtlasPage
{
	/// <summary>
	/// Gets the zero-based index of this atlas page within the atlas manager.
	/// </summary>
	public int PageIndex { get; }

	/// <summary>
	/// Gets the skyline-based rectangle packer used to place textures in this page.
	/// </summary>
	public SkylinePacker Packer { get; }

	/// <summary>
	/// Gets the texture associated with this atlas page.
	/// </summary>
	public SFTexture Texture { get; }

	/// <summary>
	/// Gets the total number of pixels currently used in this page.
	/// </summary>
	public long UsedPixels { get; private set; }

	internal AtlasPage(int pageSize, int pageIndex)
	{
		PageIndex = pageIndex;
		Packer = new SkylinePacker(pageSize, pageSize);
		// Texture = new SFTexture((uint)pageSize, (uint)pageSize);
		Texture = new SFTexture(new SFVectU((uint)pageSize, (uint)pageSize));
		UsedPixels = 0;
	}

	/// <summary>
	/// Attempts to pack the given source rectangle from a texture into this atlas page.
	/// </summary>
	/// <param name="srcTexture">The source texture containing the image to pack.</param>
	/// <param name="srcRect">The rectangle region within the source texture to pack.</param>
	/// <param name="handle">
	/// When this method returns <c>true</c>, contains the <see cref="AtlasHandle"/> describing
	/// the placement of the texture in the atlas.
	/// </param>
	/// <returns>
	/// <c>true</c> if the rectangle was successfully packed into this page; otherwise, <c>false</c>.
	/// </returns>
	/// <remarks>
	/// If successful, the pixels are copied into the atlas texture at the placement location.
	/// </remarks>
	public bool TryPack(SFTexture srcTexture, SFRectI srcRect, out AtlasHandle handle)
	{
		var placement = Packer.Insert(srcRect.Width, srcRect.Height);
		if (!placement.HasValue)
		{
			handle = default;
			return false;
		}

		var dst = placement.Value;

		// Copy only the sub-rect via SFImage
		var img = srcTexture.CopyToImage();
		// var region = new SFImage((uint)srcRect.Width, (uint)srcRect.Height);
		var region = new SFImage(new SFVectU((uint)srcRect.Width, (uint)srcRect.Height));
		region.Copy(
			img,
			// 0, 0,
			new(0, 0),

			// new SFRectI(
			// 	(int)srcRect.Left, (int)srcRect.Top,
			// 	(int)srcRect.Width, (int)srcRect.Height
			// ),
			new SFRectI(
				new(srcRect.Left, srcRect.Top),
				new(srcRect.Width, srcRect.Height)
			),
			false
		);
		// Texture.Update(region, (uint)dst.Left, (uint)dst.Top);
		Texture.Update(region, new((uint)dst.Left, (uint)dst.Top));

		UsedPixels += (long)srcRect.Width * srcRect.Height;

		handle = new AtlasHandle(PageIndex, dst);
		return true;
	}

	/// <summary>
	/// Lazily removes a region from the atlas page, decrementing the used pixel count.
	/// </summary>
	/// <param name="rect">The rectangle region to remove.</param>
	/// <remarks>
	/// This method does not actually free space for reuse in the <see cref="SkylinePacker"/>.
	/// It only adjusts <see cref="UsedPixels"/> for tracking purposes.
	/// </remarks>
	public void RemoveLazy(SFRectI rect)
	{
		// no-op under SkylinePacker

		var area = (long)rect.Width * rect.Height;
		UsedPixels = Math.Max(0, UsedPixels - area);  // avoid going negative
	}
}
