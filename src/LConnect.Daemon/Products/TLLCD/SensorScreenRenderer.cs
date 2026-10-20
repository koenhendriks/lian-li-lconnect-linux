using System;
using System.IO;
using System.Linq;
using LConnectCore;
using LConnectCore.Products.TLLCD;
using SkiaSharp;

namespace LConnectService.Products.TLLCD;

// Type is printed above the value and Label below it; Progress (0-100) drives the ring and may be fractional.
internal readonly record struct SensorReading(string Type, int Value, string Label, string Unit, double Progress);

// Replaces CefSharp.OffScreen. The original rendered Lian Li's Vue page (assets/tl-sensor) in an off-screen browser
// and screenshotted it at ~25 fps; this draws the same screens with Skia. Geometry, colours and text layout follow
// the page's p5.js sketches and CSS, and the bundle's fonts, masks and artwork are used as they are, so frames match
// what Chromium rendered from the page. A frame is only drawn when the setting or a value changes; the LCD stream
// resends the last JPEG.
internal sealed class SensorScreenRenderer
{
	private const int Size = 400;

	// The sketches walk dots 0.01 rad apart from round(-PI/2, 2) * 100 = -157 (straight up) to 471 a turn later.
	private const int FirstIndex = -157;

	private const int LastIndex = 471;

	private static readonly Lazy<SKTypeface> regularFont = new Lazy<SKTypeface>(() => loadFont("NotoSans-Regular", SKFontStyle.Normal));

	private static readonly Lazy<SKTypeface> boldFont = new Lazy<SKTypeface>(() => loadFont("NotoSans-Bold", SKFontStyle.Bold));

	private static readonly Lazy<SKBitmap> ringTwoMask = new Lazy<SKBitmap>(() => loadImage("ring_two_mask_round"));

	private static readonly Lazy<SKBitmap> ringFourOutside = new Lazy<SKBitmap>(() => loadImage("ring_four_mask_outside"));

	private static readonly Lazy<SKBitmap> ringFourInside = new Lazy<SKBitmap>(() => loadImage("ring_four_mask_inside"));

	private static readonly Lazy<SKBitmap> ringFiveMask = new Lazy<SKBitmap>(() => loadImage("ring_five_mask"));

	private static readonly Lazy<SvgPicture> ringThreeRange = new Lazy<SvgPicture>(() => loadSvg("ring_three_guideline_range"));

	private static readonly Lazy<SvgPicture> ringThreeNeedle = new Lazy<SvgPicture>(() => loadSvg("ring_three_guidelines"));

	// Sizes from the page's .text_type / .text_value / .text_unit rules and each style's overrides.
	private readonly record struct TextStyle(float TypeSize, bool TypeBold, float ValueSize, float LabelSize, bool LabelBold);

	private static readonly TextStyle ringOneText = new TextStyle(30f, TypeBold: false, 91f, 31f, LabelBold: false);

	private static readonly TextStyle ringFourText = new TextStyle(32f, TypeBold: false, 85f, 31f, LabelBold: false);

	private static readonly TextStyle ringFiveText = new TextStyle(32f, TypeBold: true, 82f, 35f, LabelBold: true);

	private readonly object gate = new object();

	private TLLCDSensorSetting setting;

	private SensorReading? reading;

	private double progress;

	private volatile byte[] latestFrame;

	public static string AssetDirectory => Path.Combine(LConnectApp.Path.AssetsDirectory, "tl-sensor");

	public byte[] LatestFrame => latestFrame;

	internal static bool HasFonts => boldFont.Value.GlyphCount > 0;

	// The fonts ship with the imported tl-sensor bundle; the system Noto Sans is the same family. SkiaSharp's
	// NoDependencies build has no fontconfig, so a family name alone would not resolve.
	private static SKTypeface loadFont(string name, SKFontStyle style)
	{
		string bundle = Path.Combine(AssetDirectory, "assets");
		string[] candidates = new string[2]
		{
			Directory.Exists(bundle) ? Directory.EnumerateFiles(bundle, name + "-*.ttf").FirstOrDefault() : null,
			"/usr/share/fonts/noto/" + name + ".ttf"
		};
		foreach (string path in candidates)
		{
			SKTypeface typeface = (File.Exists(path) ? SKTypeface.FromFile(path) : null);
			if (typeface != null)
			{
				return typeface;
			}
		}
		SKTypeface fallback = SKTypeface.FromFamilyName("Noto Sans", style) ?? SKTypeface.Default;
		if (fallback.GlyphCount == 0)
		{
			Helpers.EventLogHelper.Warn(name + " not found; LCD sensor screens show no text. Import the assets or install noto-fonts.");
		}
		return fallback;
	}

	// The masks only ship with the imported bundle; without one a style still draws, just without its overlay.
	private static SKBitmap loadImage(string name)
	{
		string path = findAsset(name, ".png");
		return (path != null) ? SKBitmap.Decode(path) : null;
	}

	private static SvgPicture loadSvg(string name)
	{
		string path = findAsset(name, ".svg");
		try
		{
			return (path != null) ? SvgPicture.Load(path) : null;
		}
		catch (Exception ex)
		{
			Helpers.EventLogHelper.Warn("LCD sensor asset " + path + " unreadable: " + ex.Message);
			return null;
		}
	}

	// Bundle files carry a content hash: ring_two_mask_round-93af6a28.png.
	private static string findAsset(string name, string extension)
	{
		string bundle = Path.Combine(AssetDirectory, "assets");
		string path = (Directory.Exists(bundle) ? Directory.EnumerateFiles(bundle, name + "-*" + extension).FirstOrDefault() : null);
		if (path == null)
		{
			Helpers.EventLogHelper.Warn("LCD sensor asset " + name + " missing; run 'lconnectctl assets import'.");
		}
		return path;
	}

	public void Configure(TLLCDSensorSetting setting)
	{
		lock (gate)
		{
			this.setting = setting;
			render();
		}
	}

	public void SetReading(SensorReading reading)
	{
		lock (gate)
		{
			this.reading = reading;
			progress = reading.Progress;
			render();
		}
	}

	public void SetProgress(double progress)
	{
		lock (gate)
		{
			this.progress = progress;
			render();
		}
	}

	private void render()
	{
		using SKBitmap bitmap = new SKBitmap(Size, Size);
		using (SKCanvas canvas = new SKCanvas(bitmap))
		{
			canvas.Clear(SKColors.Black);
			if (setting != null)
			{
				switch (setting.Style)
				{
				case 1:
					drawRingTwo(canvas);
					drawTexts(canvas, ringOneText);
					break;
				case 2:
					drawRingThree(canvas);
					drawRingThreeTexts(canvas);
					break;
				case 3:
					drawRingFour(canvas);
					drawTexts(canvas, ringFourText);
					break;
				case 4:
					drawRingFive(canvas);
					drawTexts(canvas, ringFiveText);
					break;
				default:
					// Like the page, an unknown style falls back to RingOne.
					drawRingOne(canvas);
					drawTexts(canvas, ringOneText);
					break;
				}
			}
		}
		latestFrame = MediaConverter.EncodeJpeg(bitmap, 90);
	}

	// ProgressRingOneShowMode: 52 px dots stamped on a 164 px radius; the track covers every index, the value those up
	// to the progress index (at most 470). Stamping overlapping dots is a thick arc with round caps, which is how it
	// is drawn here.
	private void drawRingOne(SKCanvas canvas)
	{
		const float dot = 52f;
		const float radius = (Size - dot) / 2f - 10f;
		SKColor endColor = toColor(setting.BackgroundColor1, SKColors.Black);
		int hue = RgbToHsb(endColor)[0];
		SKColor startColor = HsbToRgb((hue + 50) % 360, 50, 100);
		SKPoint center = new SKPoint(Size / 2f, Size / 2f);
		using SKPaint ring = new SKPaint
		{
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = dot,
			Color = HsbToRgb(hue, 100, 40)
		};
		canvas.DrawCircle(center, radius, ring);
		// The page draws no dot at all for a negative Progress and stops at the last dot past 100.
		if (progress < 0.0)
		{
			return;
		}
		double index = Math.Min(progressIndex(), LastIndex - 1);
		float startAngle = FirstIndex * 0.01f;
		float endAngle = (float)index * 0.01f;
		using SKPaint cap = new SKPaint
		{
			IsAntialias = true,
			Color = startColor
		};
		canvas.DrawCircle(dotCenter(center, radius, startAngle), dot / 2f, cap);
		if (endAngle > startAngle)
		{
			// t = 0 at the first dot and 1 a full turn later, like the page's map(index, first, last, 0, 1).
			ring.Color = SKColors.White;
			ring.Shader = SKShader.CreateSweepGradient(center, new SKColor[2] { startColor, endColor }, new float[2] { 0f, 1f }, SKMatrix.CreateRotation(startAngle, center.X, center.Y));
			SKRect oval = new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);
			canvas.DrawArc(oval, startAngle * 180f / MathF.PI, (endAngle - startAngle) * 180f / MathF.PI, useCenter: false, ring);
		}
		cap.Color = lerp(startColor, endColor, (index - FirstIndex) / (LastIndex - FirstIndex));
		canvas.DrawCircle(dotCenter(center, radius, endAngle), dot / 2f, cap);
	}

	// ProgressRingTwoShowMode: a dark disk in the background hue, a pie filled from the hue to hue + 90 that ends
	// 0.01 rad past the progress index, and a mask that is opaque everywhere except the 24 ring segments, which also
	// hides the pie's rim.
	private void drawRingTwo(SKCanvas canvas)
	{
		int hue = RgbToHsb(toColor(setting.BackgroundColor1, SKColors.Black))[0];
		using SKPaint paint = new SKPaint
		{
			IsAntialias = true,
			Color = hsv(hue, 100, 30)
		};
		canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f, paint);
		if (progress >= 0.0)
		{
			double index = Math.Min(progressIndex(), LastIndex - 1);
			drawPie(canvas, Size / 2f, (float)(index * 0.01 + 0.01), hsv(hue, 100, 100), hsv((hue + 90) % 360, 100, 100), hardRim: false);
		}
		drawMask(canvas, ringTwoMask.Value);
	}

	// ProgressRingThreeShowMode, in degrees: a 380 px disk in the background colour with p5's default 1 px black
	// outline, the tick artwork centred at a third of the height minus 10 and stretched to 90.8% of the width (p5
	// keeps its natural height), Type drawn on the canvas, and the needle drawn 38 px above the centre after rotating
	// from -89 (left) to 90 (right). The page ceils the angle to whole degrees; fractions keep the needle gliding.
	private void drawRingThree(SKCanvas canvas)
	{
		using SKPaint paint = new SKPaint
		{
			IsAntialias = true,
			Color = toColor(setting.BackgroundColor1, SKColors.Black)
		};
		canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f - 10f, paint);
		paint.Style = SKPaintStyle.Stroke;
		paint.Color = SKColors.Black;
		paint.StrokeWidth = 1f;
		canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f - 10f, paint);
		SvgPicture range = ringThreeRange.Value;
		if (range != null)
		{
			drawPicture(canvas, range, new SKPoint(Size / 2f, Size / 3f - 10f), new SKSize(Size * 0.908f, range.Size.Height));
		}
		// p5 draws loadFont() text as unhinted glyph outlines on the alphabetic baseline.
		if (reading.HasValue && !string.IsNullOrEmpty(reading.Value.Type))
		{
			using SKFont font = new SKFont(boldFont.Value, 30f)
			{
				Edging = SKFontEdging.Antialias,
				Subpixel = true,
				Hinting = SKFontHinting.None
			};
			paint.Style = SKPaintStyle.Fill;
			paint.Color = toColor(setting.TextColor1, SKColors.White);
			canvas.DrawText(reading.Value.Type, (Size - measure(font, reading.Value.Type, paint)) / 2f, Size / 3f - 20f, font, paint);
		}
		SvgPicture needle = ringThreeNeedle.Value;
		if (needle != null)
		{
			canvas.Save();
			canvas.Translate(Size / 2f, Size / 2f);
			canvas.RotateDegrees((float)(-89.0 + Math.Clamp(progress, 0.0, 100.0) * 1.79));
			drawPicture(canvas, needle, new SKPoint(0f, -38f), needle.Size);
			canvas.Restore();
		}
	}

	// .ring_three_pb: the value and Label (26 px, 10 px above it) sit at the bottom, 26 px up from the edge.
	private void drawRingThreeTexts(SKCanvas canvas)
	{
		if (reading.HasValue)
		{
			SensorReading value = reading.Value;
			float labelTop = Size - 26f - (string.IsNullOrEmpty(value.Label) ? 0f : 26f);
			drawLine(canvas, valueText(value), boldFont.Value, 91f, labelTop - 10f - 91f, toColor(setting.TextColor2, SKColors.White));
			drawLine(canvas, value.Label, regularFont.Value, 26f, labelTop, toColor(setting.TextColor3, SKColors.White));
		}
	}

	// ProgressRingFourShowMode: the canvas in BackgroundColor2 under the outer tick mask, a 177 px pie from
	// BackgroundColor1 to BackgroundColor2, then a 140 px black disk, a 130 px BackgroundColor2 disk and the inner
	// mask. Both colours make a round trip through the page's HSB values.
	private void drawRingFour(SKCanvas canvas)
	{
		SKColor color1 = hsbRoundTrip(setting.BackgroundColor1);
		SKColor color2 = hsbRoundTrip(setting.BackgroundColor2);
		canvas.Clear(color2);
		drawMask(canvas, ringFourOutside.Value);
		drawPieBelowProgress(canvas, 177f, color1, color2);
		using SKPaint paint = new SKPaint
		{
			IsAntialias = true,
			Color = SKColors.Black
		};
		canvas.DrawCircle(Size / 2f, Size / 2f, 140f, paint);
		paint.Color = color2;
		canvas.DrawCircle(Size / 2f, Size / 2f, 130f, paint);
		drawMask(canvas, ringFourInside.Value);
	}

	// ProgressRingFiveShowMode: a dark grey disk (HSB 0, 0, 32) with p5's default 1 px black outline, a full-size pie
	// from BackgroundColor1 to BackgroundColor2, and the mask with the glossy centre on top.
	private void drawRingFive(SKCanvas canvas)
	{
		using SKPaint paint = new SKPaint
		{
			IsAntialias = true,
			Color = hsv(0, 0, 32)
		};
		canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f, paint);
		paint.Style = SKPaintStyle.Stroke;
		paint.Color = SKColors.Black;
		paint.StrokeWidth = 1f;
		canvas.DrawCircle(Size / 2f, Size / 2f, Size / 2f, paint);
		drawPieBelowProgress(canvas, Size / 2f, hsbRoundTrip(setting.BackgroundColor1), hsbRoundTrip(setting.BackgroundColor2));
		drawMask(canvas, ringFiveMask.Value);
	}

	// RingFour and RingFive only take dots below the progress index, so 0 draws nothing and the pie ends at the
	// progress angle itself.
	private void drawPieBelowProgress(SKCanvas canvas, float radius, SKColor from, SKColor to)
	{
		double index = progressIndex();
		if (index > FirstIndex)
		{
			drawPie(canvas, radius, (float)(index * 0.01), from, to, hardRim: true);
		}
	}

	private double progressIndex()
	{
		return FirstIndex + Math.Clamp(progress, 0.0, 100.0) * (LastIndex - FirstIndex) / 100.0;
	}

	// arc(..., PIE) from straight up to endAngle (radians) over createConicGradient(-HALF_PI): the gradient runs one
	// full turn from the top. The page redraws every wedge up to the progress each frame, so anti-aliasing coverage
	// piles up on the shared rim until any touched pixel is fully painted; hardRim reproduces that with an aliased
	// pass half a pixel wider, under an anti-aliased pass that keeps the moving end edge smooth.
	private static void drawPie(SKCanvas canvas, float radius, float endAngle, SKColor from, SKColor to, bool hardRim)
	{
		SKPoint center = new SKPoint(Size / 2f, Size / 2f);
		float startAngle = -MathF.PI / 2f;
		float startDegrees = startAngle * 180f / MathF.PI;
		float sweepDegrees = Math.Min((endAngle - startAngle) * 180f / MathF.PI, 360f);
		using SKShader shader = SKShader.CreateSweepGradient(center, new SKColor[2] { from, to }, new float[2] { 0f, 1f }, SKMatrix.CreateRotation(startAngle, center.X, center.Y));
		using SKPaint paint = new SKPaint
		{
			Shader = shader
		};
		if (hardRim)
		{
			paint.IsAntialias = false;
			canvas.DrawArc(new SKRect(center.X - radius - 0.5f, center.Y - radius - 0.5f, center.X + radius + 0.5f, center.Y + radius + 0.5f), startDegrees, sweepDegrees, useCenter: true, paint);
		}
		paint.IsAntialias = true;
		canvas.DrawArc(new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius), startDegrees, sweepDegrees, useCenter: true, paint);
	}

	private static SKColor hsbRoundTrip(int[] rgb)
	{
		int[] hsb = RgbToHsb(toColor(rgb, SKColors.Black));
		return hsv(hsb[0], hsb[1], hsb[2]);
	}

	private static void drawPicture(SKCanvas canvas, SvgPicture picture, SKPoint center, SKSize size)
	{
		canvas.Save();
		canvas.Translate(center.X - size.Width / 2f, center.Y - size.Height / 2f);
		canvas.Scale(size.Width / picture.Size.Width, size.Height / picture.Size.Height);
		canvas.DrawPicture(picture.Picture);
		canvas.Restore();
	}

	private static void drawMask(SKCanvas canvas, SKBitmap mask)
	{
		if (mask != null)
		{
			using SKPaint paint = new SKPaint
			{
				FilterQuality = SKFilterQuality.Medium
			};
			canvas.DrawBitmap(mask, new SKRect(0f, 0f, Size, Size), paint);
		}
	}

	// p5's HSB colour, as the sketches set it with colorMode(HSB, 360, 100, 100); p5 rounds the channels where
	// SKColor.FromHsv truncates.
	private static SKColor hsv(int hue, int saturation, int brightness)
	{
		double s = saturation / 100.0;
		double v = brightness / 100.0;
		double h = hue % 360 / 60.0;
		int sector = (int)h;
		double f = h - sector;
		double p = v * (1.0 - s);
		double q = v * (1.0 - s * f);
		double t = v * (1.0 - s * (1.0 - f));
		(double r, double g, double b) = sector switch
		{
			0 => (v, t, p), 
			1 => (q, v, p), 
			2 => (p, v, t), 
			3 => (p, q, v), 
			4 => (t, p, v), 
			_ => (v, p, q), 
		};
		return new SKColor((byte)Math.Round(r * 255.0), (byte)Math.Round(g * 255.0), (byte)Math.Round(b * 255.0));
	}

	private static SKPoint dotCenter(SKPoint center, float radius, float angle)
	{
		return new SKPoint(center.X + radius * MathF.Cos(angle), center.Y + radius * MathF.Sin(angle));
	}

	// The page's .text_wrap: a 400 px flex column centred both ways holding Type (30 px, 12 px below), the value with
	// its unit (bold 91 px) and Label (31 px, 16 px above), each with line-height 1. An empty div has no line box
	// but keeps its margin.
	private void drawTexts(SKCanvas canvas, TextStyle style)
	{
		if (!reading.HasValue)
		{
			return;
		}
		SensorReading value = reading.Value;
		float typeHeight = (string.IsNullOrEmpty(value.Type) ? 0f : style.TypeSize);
		float labelHeight = (string.IsNullOrEmpty(value.Label) ? 0f : style.LabelSize);
		float top = (Size - (typeHeight + 12f + style.ValueSize + 16f + labelHeight)) / 2f;
		float valueTop = top + typeHeight + 12f;
		drawLine(canvas, value.Type, style.TypeBold ? boldFont.Value : regularFont.Value, style.TypeSize, top, toColor(setting.TextColor1, SKColors.White));
		drawLine(canvas, valueText(value), boldFont.Value, style.ValueSize, valueTop, toColor(setting.TextColor2, SKColors.White));
		drawLine(canvas, value.Label, style.LabelBold ? boldFont.Value : regularFont.Value, style.LabelSize, valueTop + style.ValueSize + 16f, toColor(setting.TextColor3, SKColors.White));
	}

	// Vue prints the unit only for non-FAN readings with a truthy value, so a 0 shows without it.
	private static string valueText(SensorReading value)
	{
		return value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ((value.Type != "FAN" && value.Value != 0) ? value.Unit : string.Empty);
	}

	// With line-height equal to the font size the glyphs sit centred in the line box: CSS splits the difference
	// between the line height and ascent + descent above and below. Chromium rounds ascent and descent to whole
	// pixels and floors the half-leading, and hints slightly; doing the same keeps every line within 0.1 px of it.
	private static void drawLine(SKCanvas canvas, string text, SKTypeface typeface, float size, float top, SKColor color)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		using SKFont font = new SKFont(typeface, size)
		{
			Edging = SKFontEdging.Antialias,
			Subpixel = true,
			Hinting = SKFontHinting.Slight
		};
		using SKPaint paint = new SKPaint
		{
			IsAntialias = true,
			Color = color
		};
		SKFontMetrics metrics = font.Metrics;
		float ascent = MathF.Round(0f - metrics.Ascent);
		float descent = MathF.Round(metrics.Descent);
		float baseline = top + MathF.Floor((size - (ascent + descent)) / 2f) + ascent;
		canvas.DrawText(text, (Size - measure(font, text, paint)) / 2f, baseline, font, paint);
	}

	private static float measure(SKFont font, string text, SKPaint paint)
	{
		ushort[] glyphs = new ushort[font.CountGlyphs(text)];
		font.GetGlyphs(text, glyphs);
		return font.MeasureText(glyphs, paint);
	}

	private static SKColor toColor(int[] rgb, SKColor fallback)
	{
		if (rgb == null || rgb.Length < 3)
		{
			return fallback;
		}
		return new SKColor((byte)Math.Clamp(rgb[0], 0, 255), (byte)Math.Clamp(rgb[1], 0, 255), (byte)Math.Clamp(rgb[2], 0, 255));
	}

	private static SKColor lerp(SKColor from, SKColor to, double t)
	{
		t = Math.Clamp(t, 0.0, 1.0);
		return new SKColor((byte)Math.Round(from.Red + (to.Red - from.Red) * t), (byte)Math.Round(from.Green + (to.Green - from.Green) * t), (byte)Math.Round(from.Blue + (to.Blue - from.Blue) * t));
	}

	// The page's rgbToHsb/hsbToRgb (useColorConverter), rounding up the same way so derived colours match exactly.
	internal static int[] RgbToHsb(SKColor color)
	{
		double r = color.Red / 255.0;
		double g = color.Green / 255.0;
		double b = color.Blue / 255.0;
		double max = Math.Max(r, Math.Max(g, b));
		double delta = max - Math.Min(r, Math.Min(g, b));
		double sector = ((delta == 0.0) ? 0.0 : ((max == r) ? ((g - b) / delta) : ((max == g) ? (2.0 + (b - r) / delta) : (4.0 + (r - g) / delta))));
		return new int[3]
		{
			(int)Math.Ceiling(60.0 * ((sector < 0.0) ? (sector + 6.0) : sector)),
			(int)Math.Ceiling((max == 0.0) ? 0.0 : (delta / max * 100.0)),
			(int)Math.Ceiling(max * 100.0)
		};
	}

	internal static SKColor HsbToRgb(int hue, int saturation, int brightness)
	{
		double s = saturation / 100.0;
		double b = brightness / 100.0;
		byte channel(int n)
		{
			double k = (n + hue / 60.0) % 6.0;
			return (byte)Math.Ceiling(255.0 * (b * (1.0 - s * Math.Max(0.0, Math.Min(Math.Min(k, 4.0 - k), 1.0)))));
		}
		return new SKColor(channel(5), channel(3), channel(1));
	}
}
