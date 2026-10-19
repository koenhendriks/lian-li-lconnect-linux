using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media.Imaging;
using LConnectCore;
using LConnectCore.Products.TLLCD;
using SkiaSharp;

namespace LConnectUi.Services;

// Thumbnails for the LCD media library and the screen previews (first frame for videos, the block of a
// multi-fan canvas an LCD shows). Decoding runs on worker threads; results are cached per file and block.
public static class MediaPreview
{
	private static readonly ConcurrentDictionary<string, byte[]> frameCache = new ConcurrentDictionary<string, byte[]>();

	public static byte[] FirstFrame(TLLCDMediaType type, string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return null;
		}
		string key = type + "|" + path + "|" + File.GetLastWriteTimeUtc(path).Ticks;
		if (frameCache.TryGetValue(key, out var value))
		{
			return value;
		}
		try
		{
			value = (type == TLLCDMediaType.Image) ? File.ReadAllBytes(path) : MediaConverter.ConvertVideoFirstFrameToJpegs(path);
		}
		catch
		{
			value = null;
		}
		if (value != null)
		{
			frameCache[key] = value;
		}
		return value;
	}

	// Crops block 'block' (1-based) out of a canvas made for 'totalBlock' LCDs, like LCDScreen.AddImage did.
	public static byte[] CropBlock(byte[] jpeg, int block, int totalBlock, bool horizontal)
	{
		if (jpeg == null || totalBlock <= 1)
		{
			return jpeg;
		}
		try
		{
			using SKBitmap sKBitmap = SKBitmap.Decode(jpeg);
			if (sKBitmap == null)
			{
				return jpeg;
			}
			int num = Math.Clamp(block, 1, totalBlock) - 1;
			SKRectI sKRectI = horizontal ? new SKRectI(num * sKBitmap.Width / totalBlock, 0, (num + 1) * sKBitmap.Width / totalBlock, sKBitmap.Height) : new SKRectI(0, num * sKBitmap.Height / totalBlock, sKBitmap.Width, (num + 1) * sKBitmap.Height / totalBlock);
			using SKBitmap sKBitmap2 = new SKBitmap(sKRectI.Width, sKRectI.Height);
			if (!sKBitmap.ExtractSubset(sKBitmap2, sKRectI))
			{
				return jpeg;
			}
			using SKImage sKImage = SKImage.FromBitmap(sKBitmap2);
			using SKData sKData = sKImage.Encode(SKEncodedImageFormat.Jpeg, 85);
			return sKData.ToArray();
		}
		catch
		{
			return jpeg;
		}
	}

	public static Bitmap ToBitmap(byte[] bytes, int width = 160)
	{
		if (bytes == null || bytes.Length == 0)
		{
			return null;
		}
		try
		{
			using MemoryStream stream = new MemoryStream(bytes);
			return Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.MediumQuality);
		}
		catch
		{
			return null;
		}
	}
}
