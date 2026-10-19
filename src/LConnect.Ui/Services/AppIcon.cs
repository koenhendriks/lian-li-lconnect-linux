using System;
using System.IO;
using Avalonia.Controls;
using SkiaSharp;

namespace LConnectUi.Services;

// A plain generated fan glyph for the window and tray (no vendor artwork in the source tree).
public static class AppIcon
{
	private static byte[] png;

	public static byte[] Png => png ??= render(64);

	public static WindowIcon Create()
	{
		return new WindowIcon(new MemoryStream(Png));
	}

	private static byte[] render(int size)
	{
		using SKBitmap sKBitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
		using (SKCanvas sKCanvas = new SKCanvas(sKBitmap))
		{
			sKCanvas.Clear(SKColors.Transparent);
			float num = size / 2f;
			using SKPaint sKPaint = new SKPaint
			{
				IsAntialias = true,
				Color = new SKColor(30, 34, 40)
			};
			sKCanvas.DrawCircle(num, num, num - 1f, sKPaint);
			using SKPaint sKPaint2 = new SKPaint
			{
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = size / 16f,
				Color = new SKColor(46, 155, 255)
			};
			sKCanvas.DrawCircle(num, num, num - size / 12f, sKPaint2);
			using SKPaint sKPaint3 = new SKPaint
			{
				IsAntialias = true,
				Color = new SKColor(220, 236, 255)
			};
			for (int i = 0; i < 3; i++)
			{
				sKCanvas.Save();
				sKCanvas.RotateDegrees(i * 120f, num, num);
				using SKPath sKPath = new SKPath();
				sKPath.MoveTo(num, num);
				sKPath.CubicTo(num - size * 0.05f, num - size * 0.25f, num + size * 0.18f, num - size * 0.36f, num + size * 0.22f, num - size * 0.2f);
				sKPath.CubicTo(num + size * 0.18f, num - size * 0.1f, num + size * 0.08f, num - size * 0.04f, num, num);
				sKPath.Close();
				sKCanvas.DrawPath(sKPath, sKPaint3);
				sKCanvas.Restore();
			}
			sKPaint.Color = new SKColor(46, 155, 255);
			sKCanvas.DrawCircle(num, num, size / 10f, sKPaint);
		}
		using SKImage sKImage = SKImage.FromBitmap(sKBitmap);
		using SKData sKData = sKImage.Encode(SKEncodedImageFormat.Png, 100);
		return sKData.ToArray();
	}
}
