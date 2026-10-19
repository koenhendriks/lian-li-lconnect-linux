using System;
using System.IO;
using LConnectCore;
using SkiaSharp;

namespace LConnectService.Products.TLLCD;

// The original showed an embedded Lian Li logo (Resources.LogoImage400x400) when a screen had no media.
// That bitmap is not redistributed: 'lconnectctl assets import' extracts it from the user's own install into
// assets/logo-400x400.jpg. Without it we draw a plain dark screen.
internal static class DefaultScreen
{
	private static byte[] cached;

	public static string LogoPath => Path.Combine(LConnectApp.Path.AssetsDirectory, "logo-400x400.jpg");

	public static byte[] GetJpeg()
	{
		if (cached != null)
		{
			return cached;
		}
		try
		{
			if (File.Exists(LogoPath))
			{
				cached = MediaConverter.GetImageBlock(LogoPath, 0, horizontal: false);
				return cached;
			}
		}
		catch
		{
		}
		using SKBitmap sKBitmap = new SKBitmap(400, 400);
		using (SKCanvas sKCanvas = new SKCanvas(sKBitmap))
		{
			sKCanvas.Clear(new SKColor(10, 10, 14));
			using SKPaint paint = new SKPaint
			{
				Color = new SKColor(70, 70, 80),
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				StrokeWidth = 6f
			};
			sKCanvas.DrawCircle(200f, 200f, 120f, paint);
		}
		cached = MediaConverter.EncodeJpeg(sKBitmap, 90);
		return cached;
	}
}
