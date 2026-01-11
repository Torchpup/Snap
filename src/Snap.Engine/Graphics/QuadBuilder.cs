using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Snap.Engine.Graphics;

public static class QuadBuilder
{
	private const float TexelOffset = 0.05f;

	public static void BuildQuad(
		SFVertex[] output, // Must be length 6
		Rect2 dstRect,
		Rect2 srcRect,
		Color color,
		Vect2 origin,
		Vect2 scale,
		float rotation,
		TextureEffects effects,
		SFTexture texture = null)
	{
		if (output == null || output.Length < 6)
			throw new ArgumentException("Output array must have length 6", nameof(output));

		float pivotX = origin.X * dstRect.Width * scale.X;
		float pivotY = origin.Y * dstRect.Height * scale.Y;

		Span<Vect2> localPos = stackalloc Vect2[4];
		localPos[0] = new Vect2(-pivotX, -pivotY);
		localPos[1] = new Vect2(dstRect.Width * scale.X - pivotX, -pivotY);
		localPos[2] = new Vect2(dstRect.Width * scale.X - pivotX, dstRect.Height * scale.Y - pivotY);
		localPos[3] = new Vect2(-pivotX, dstRect.Height * scale.Y - pivotY);

		float cos = MathF.Cos(rotation);
		float sin = MathF.Sin(rotation);

		for (int i = 0; i < 4; i++)
		{
			float x = localPos[i].X;
			float y = localPos[i].Y;

			localPos[i] = new Vect2(
				cos * x - sin * y + dstRect.X + pivotX,
				sin * x + cos * y + dstRect.Y + pivotY
			);
		}

		float u1 = srcRect.Left;
		float v1 = srcRect.Top;
		float u2 = srcRect.Right;
		float v2 = srcRect.Bottom;

		if (effects.HasFlag(TextureEffects.FlipHorizontal))
			(u1, u2) = (u2, u1);
		if (effects.HasFlag(TextureEffects.FlipVertical))
			(v1, v2) = (v2, v1);

		if (texture != null && EngineSettings.Instance?.HalfTexelOffset == true)
		{
			float texelOffsetX = TexelOffset / texture.Size.X;
			float texelOffsetY = TexelOffset / texture.Size.Y;

			if (u1 < u2) { u1 += texelOffsetX; u2 -= texelOffsetX; }
			else { u1 -= texelOffsetX; u2 += texelOffsetX; }

			if (v1 < v2) { v1 += texelOffsetY; v2 -= texelOffsetY; }
			else { v1 -= texelOffsetY; v2 += texelOffsetY; }
		}

		// Build two triangles (6 vertices)
		output[0] = new SFVertex(new SFVectF(localPos[0].X, localPos[0].Y), color, new SFVectF(u1, v1));
		output[1] = new SFVertex(new SFVectF(localPos[1].X, localPos[1].Y), color, new SFVectF(u2, v1));
		output[2] = new SFVertex(new SFVectF(localPos[3].X, localPos[3].Y), color, new SFVectF(u1, v2));
		output[3] = new SFVertex(new SFVectF(localPos[1].X, localPos[1].Y), color, new SFVectF(u2, v1));
		output[4] = new SFVertex(new SFVectF(localPos[2].X, localPos[2].Y), color, new SFVectF(u2, v2));
		output[5] = new SFVertex(new SFVectF(localPos[3].X, localPos[3].Y), color, new SFVectF(u1, v2));
	}

	public static void BuildQuad(
		SFVertex[] output,
		Rect2 dstRect,
		Rect2 srcRect,
		Color color,
		int depth = 0)
	{
		BuildQuad(output, dstRect, srcRect, color,
				  Vect2.Zero, Vect2.One, 0f, TextureEffects.None, null);
	}

	public static void BuildQuad(
		SFVertex[] output,
		Vect2 position,
		Vect2 size,
		Rect2 srcRect,
		Color color)
	{
		BuildQuad(output, new Rect2(position, size), srcRect, color,
				  Vect2.Zero, Vect2.One, 0f, TextureEffects.None, null);
	}
}
