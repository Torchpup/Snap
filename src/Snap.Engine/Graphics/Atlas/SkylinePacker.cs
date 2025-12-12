namespace Snap.Engine.Graphics.Atlas;

/// <summary>
/// A rectangle packing utility that uses the "skyline" algorithm to efficiently place 
/// rectangular regions within a fixed-size 2D space.
/// </summary>
/// <remarks>
/// <para>
/// The skyline algorithm tracks the top profile ("skyline") of already-placed rectangles 
/// and inserts new rectangles in a way that minimizes wasted space and fragmentation.
/// </para>
/// <para>
/// This implementation is used by <see cref="AtlasPage"/> to pack texture regions into 
/// a single <see cref="SFTexture"/>.
/// </para>
/// <para>
/// The packer operates within a fixed-size container defined at construction time.  
/// Once no space can be found for a requested rectangle, the pack attempt will fail.
/// </para>
/// </remarks>
public sealed class SkylinePacker
{
	internal class Node(int x, int y, int width)
	{
		public int X = x;
		public int Y = y;
		public int Width = width;
	}

	private readonly int _pageWidth;
	private readonly int _pageHeight;
	private readonly List<Node> _nodes;

	internal SkylinePacker(int pageWidth, int pageHeight)
	{
		_pageWidth = pageWidth;
		_pageHeight = pageHeight;
		_nodes = [new(0, 0, pageWidth)];
	}

	/// <summary>
	/// Resets the skyline packer to its initial empty state.
	/// </summary>
	/// <remarks>
	/// Clears all existing placement data and reinitializes the skyline to a single 
	/// horizontal segment spanning the full width of the packing area at height 0.  
	/// After calling this method, all previously packed rectangles are forgotten.
	/// </remarks>
	public void Reset()
	{
		_nodes.Clear();
		_nodes.Add(new Node(0, 0, _pageWidth));
	}

	/// <summary>
	/// Attempts to insert a rectangle of the given size into the packing area.
	/// </summary>
	/// <param name="w">The width of the rectangle to insert, in pixels.</param>
	/// <param name="h">The height of the rectangle to insert, in pixels.</param>
	/// <returns>
	/// An <see cref="SFRectI"/> representing the placement coordinates of the packed rectangle 
	/// if successful; otherwise, <c>null</c> if no suitable space was found.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Uses the skyline algorithm to find the placement that results in the lowest 
	/// possible top Y coordinate, breaking ties by preferring the leftmost X position.
	/// </para>
	/// <para>
	/// If a placement is found, the skyline is updated and adjacent nodes are merged 
	/// to maintain efficiency.
	/// </para>
	/// </remarks>
	public SFRectI? Insert(int w, int h)
	{
		int bestIndex = -1, bestX = 0, bestY = int.MaxValue;

		// find the skyline node giving the lowest y (and then leftmost)
		for (int i = 0; i < _nodes.Count; i++)
		{
			int y = Fit(i, w, h);
			if (y >= 0 && (y < bestY || y == bestY && _nodes[i].X < bestX))
			{
				bestIndex = i;
				bestX = _nodes[i].X;
				bestY = y;
			}
		}

		if (bestIndex == -1)
			return null;

		var rect = new SFRectI(bestX, bestY, w, h);

		AddSkylineLevel(bestIndex, rect);
		Merge();

		return rect;
	}

	private int Fit(int index, int w, int h)
	{
		var node = _nodes[index];

		if (node.X + w > _pageWidth)
			return -1;

		// int x = node.X;
		int width = w;
		int y = node.Y;
		int i = index;

		while (width > 0)
		{
			if (i >= _nodes.Count) return -1;
			y = Math.Max(y, _nodes[i].Y);
			if (y + h > _pageHeight) return -1;
			width -= _nodes[i].Width;
			i++;
		}
		return y;
	}

	private void AddSkylineLevel(int index, SFRectI rect)
	{
		// var node = _nodes[index];
		var newNode = new Node(rect.Left, rect.Top + rect.Height, rect.Width);

		_nodes.Insert(index, newNode);

		// shrink or remove overlapping nodes
		for (int i = index + 1; i < _nodes.Count; i++)
		{
			var n = _nodes[i];

			if (n.X < newNode.X + newNode.Width)
			{
				int shrink = newNode.X + newNode.Width - n.X;

				n.X += shrink;
				n.Width -= shrink;

				if (n.Width <= 0)
				{
					_nodes.RemoveAt(i);
					i--;
				}
			}
			else
			{
				break;
			}
		}
	}

	private void Merge()
	{
		for (int i = 0; i < _nodes.Count - 1; i++)
		{
			var a = _nodes[i];
			var b = _nodes[i + 1];

			if (a.Y == b.Y)
			{
				a.Width += b.Width;
				_nodes.RemoveAt(i + 1);
				i--;
			}
		}
	}
}
