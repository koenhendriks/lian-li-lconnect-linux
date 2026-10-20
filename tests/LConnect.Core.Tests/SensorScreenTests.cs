using System;
using System.IO;
using LConnectCore.Products.TLLCD;
using LConnectService.Products.TLLCD;
using SkiaSharp;
using Xunit;

namespace LConnect.Core.Tests;

public class SensorScreenTests
{
	// Expected values come from running the page's own rgbToHsb/hsbToRgb in node.
	[Theory]
	[InlineData(255, 0, 0, 0, 100, 100, 255, 234, 128, 102, 0, 0)]
	[InlineData(0, 0, 255, 240, 100, 100, 234, 128, 255, 0, 0, 102)]
	[InlineData(0, 200, 80, 144, 100, 79, 128, 226, 255, 0, 102, 41)]
	[InlineData(255, 255, 255, 0, 0, 100, 255, 234, 128, 102, 0, 0)]
	[InlineData(0, 0, 0, 0, 0, 0, 255, 234, 128, 102, 0, 0)]
	[InlineData(123, 45, 210, 269, 79, 83, 255, 128, 215, 50, 0, 102)]
	[InlineData(250, 140, 10, 33, 97, 99, 207, 255, 128, 102, 57, 0)]
	public void RingOneColoursMatchThePage(int r, int g, int b, int h, int s, int v, int startR, int startG, int startB, int trackR, int trackG, int trackB)
	{
		int[] hsb = SensorScreenRenderer.RgbToHsb(new SKColor((byte)r, (byte)g, (byte)b));
		Assert.Equal(new int[3] { h, s, v }, hsb);
		Assert.Equal(new SKColor((byte)startR, (byte)startG, (byte)startB), SensorScreenRenderer.HsbToRgb((hsb[0] + 50) % 360, 50, 100));
		Assert.Equal(new SKColor((byte)trackR, (byte)trackG, (byte)trackB), SensorScreenRenderer.HsbToRgb(hsb[0], 100, 40));
	}

	[Fact]
	public void RingOneDrawsTrackValueAndText()
	{
		SensorScreenRenderer renderer = new SensorScreenRenderer();
		renderer.Configure(new TLLCDSensorSetting
		{
			Type = TLLCDSensorType.CPUTemp,
			Style = 0,
			BackgroundColor1 = new int[3] { 255, 0, 0 },
			TextColor1 = new int[3] { 255, 255, 255 },
			TextColor2 = new int[3] { 255, 255, 255 },
			TextColor3 = new int[3] { 255, 255, 255 }
		});
		renderer.SetReading(new SensorReading("CPU", 55, "TEMP", "°C", 50.0));
		using SKBitmap frame = SKBitmap.Decode(renderer.LatestFrame);
		Assert.Equal(400, frame.Width);
		// 50% runs clockwise from the top to the bottom: the right side is lit, the left is the dark red track.
		assertNear(new SKColor(255, 176, 96), frame.GetPixel(364, 200));
		assertNear(new SKColor(102, 0, 0), frame.GetPixel(36, 200));
		assertNear(SKColors.Black, frame.GetPixel(200, 90));
		// CI has neither the bundle nor system Noto fonts; the ring is checked either way.
		if (SensorScreenRenderer.HasFonts)
		{
			Assert.True(countBright(frame, 100, 165, 300, 235) > 2000, "value text missing");
		}
		renderer.SetProgress(0.0);
		using SKBitmap frame2 = SKBitmap.Decode(renderer.LatestFrame);
		assertNear(new SKColor(102, 0, 0), frame2.GetPixel(364, 200));
		// Progress 0 still shows the first dot at the top, in the start colour.
		assertNear(new SKColor(255, 234, 128), frame2.GetPixel(200, 36));
	}

	// Needs the imported bundle for the segment mask; points sit mid-segment, 175 px out at 7.5 degrees past a divider.
	[Fact]
	public void RingTwoFillsSegmentsWithTheGradient()
	{
		if (!File.Exists(Path.Combine(SensorScreenRenderer.AssetDirectory, "index.html")))
		{
			return;
		}
		using SKBitmap frame = render(1, new int[3] { 255, 0, 0 }, 50.0);
		assertNear(new SKColor(221, 69, 0), frame.GetPixel(373, 223));
		assertNear(new SKColor(77, 0, 0), frame.GetPixel(27, 177));
		// Between the segments the mask is opaque black.
		assertNear(SKColors.Black, frame.GetPixel(375, 200));
	}

	[Fact]
	public void RingThreeDrawsTheDialAndNeedle()
	{
		if (!File.Exists(Path.Combine(SensorScreenRenderer.AssetDirectory, "index.html")))
		{
			return;
		}
		using SKBitmap frame = render(2, new int[3] { 255, 0, 0 }, 50.0);
		assertNear(new SKColor(255, 0, 0), frame.GetPixel(100, 320));
		// The translucent white band of the tick artwork over the upper half.
		assertNear(new SKColor(255, 77, 77), frame.GetPixel(60, 150));
		// 50% points the needle straight up from the hub.
		assertNear(SKColors.White, frame.GetPixel(200, 200));
		assertNear(SKColors.White, frame.GetPixel(201, 110));
	}

	[Fact]
	public void RingFourSweepsFromTheFirstToTheSecondColour()
	{
		if (!File.Exists(Path.Combine(SensorScreenRenderer.AssetDirectory, "index.html")))
		{
			return;
		}
		using SKBitmap frame = render(3, new int[3] { 255, 0, 0 }, 50.0);
		// A quarter turn into the red-to-yellow gradient.
		assertNear(new SKColor(255, 64, 0), frame.GetPixel(365, 200));
		assertNear(SKColors.Black, frame.GetPixel(200, 200));
		using SKBitmap frame2 = render(3, new int[3] { 255, 0, 0 }, 0.0);
		Assert.True(frame2.GetPixel(365, 200).Red < 40, "0 should draw no pie");
	}

	[Fact]
	public void RingFiveFillsTheDiskAroundTheGlossyCentre()
	{
		if (!File.Exists(Path.Combine(SensorScreenRenderer.AssetDirectory, "index.html")))
		{
			return;
		}
		using SKBitmap frame = render(4, new int[3] { 255, 0, 0 }, 50.0);
		assertNear(new SKColor(255, 64, 0), frame.GetPixel(370, 200));
		// The unfilled half shows the dark grey disk (HSB 0, 0, 32).
		assertNear(new SKColor(82, 82, 82), frame.GetPixel(30, 200));
		Assert.True(frame.GetPixel(200, 200).Red > 200, "glossy centre missing");
	}

	[Fact]
	public void SvgPictureAppliesClassesAndTransforms()
	{
		using SvgPicture svg = SvgPicture.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\" viewBox=\"0 0 20 10\"><defs><style>.a, .b { fill: #fff; } .b { opacity: 0.5; }</style></defs><g transform=\"translate(10 0)\"><rect class=\"b\" width=\"10\" height=\"10\"/></g><path class=\"a\" d=\"M0,0H5V10H0Z\"/></svg>");
		Assert.Equal(new SKSize(20f, 10f), svg.Size);
		using SKBitmap bitmap = new SKBitmap(20, 10);
		using (SKCanvas canvas = new SKCanvas(bitmap))
		{
			canvas.Clear(SKColors.Black);
			canvas.DrawPicture(svg.Picture);
		}
		assertNear(SKColors.White, bitmap.GetPixel(2, 5));
		assertNear(SKColors.Black, bitmap.GetPixel(7, 5));
		assertNear(new SKColor(128, 128, 128), bitmap.GetPixel(15, 5));
	}

	private static SKBitmap render(int style, int[] background, double progress)
	{
		SensorScreenRenderer renderer = new SensorScreenRenderer();
		renderer.Configure(new TLLCDSensorSetting
		{
			Type = TLLCDSensorType.CPUTemp,
			Style = style,
			BackgroundColor1 = background,
			BackgroundColor2 = new int[3] { 255, 255, 0 }
		});
		renderer.SetReading(new SensorReading("", 0, "", "", progress));
		return SKBitmap.Decode(renderer.LatestFrame);
	}

	private static void assertNear(SKColor expected, SKColor actual)
	{
		int num = Math.Max(Math.Abs(expected.Red - actual.Red), Math.Max(Math.Abs(expected.Green - actual.Green), Math.Abs(expected.Blue - actual.Blue)));
		Assert.True(num <= 12, $"expected {expected}, got {actual}");
	}

	private static int countBright(SKBitmap bitmap, int left, int top, int right, int bottom)
	{
		int num = 0;
		for (int i = top; i < bottom; i++)
		{
			for (int j = left; j < right; j++)
			{
				if (bitmap.GetPixel(j, i).Red > 200)
				{
					num++;
				}
			}
		}
		return num;
	}
}
