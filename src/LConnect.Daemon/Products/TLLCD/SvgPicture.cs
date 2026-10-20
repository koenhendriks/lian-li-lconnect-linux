using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace LConnectService.Products.TLLCD;

// Just enough SVG for the sensor bundle's artwork: g, path, line, circle and rect with translate/rotate/scale/matrix
// transforms, presentation attributes and simple ".class { ... }" rules from a <style> block. Anything else is
// skipped rather than guessed at.
internal sealed class SvgPicture : IDisposable
{
	private static readonly Regex ruleRegex = new Regex("([^{}]+)\\{([^}]*)\\}");

	private static readonly Regex transformRegex = new Regex("(\\w+)\\s*\\(([^)]*)\\)");

	private static readonly string[] presentationAttributes = new string[9] { "fill", "stroke", "stroke-width", "opacity", "fill-opacity", "stroke-opacity", "stroke-linecap", "stroke-linejoin", "fill-rule" };

	private readonly Dictionary<string, Dictionary<string, string>> classes = new Dictionary<string, Dictionary<string, string>>();

	public SKPicture Picture { get; }

	public SKSize Size { get; }

	private SvgPicture(XElement root)
	{
		XNamespace ns = root.Name.Namespace;
		foreach (XElement style in root.Descendants(ns + "style"))
		{
			parseStyle(style.Value);
		}
		float width = number(root.Attribute("width")?.Value, 0f);
		float height = number(root.Attribute("height")?.Value, 0f);
		float[] viewBox = numbers(root.Attribute("viewBox")?.Value);
		if (viewBox.Length == 4 && (width <= 0f || height <= 0f))
		{
			width = viewBox[2];
			height = viewBox[3];
		}
		Size = new SKSize(width, height);
		using SKPictureRecorder recorder = new SKPictureRecorder();
		SKCanvas canvas = recorder.BeginRecording(new SKRect(0f, 0f, width, height));
		if (viewBox.Length == 4 && viewBox[2] > 0f && viewBox[3] > 0f)
		{
			canvas.Scale(width / viewBox[2], height / viewBox[3]);
			canvas.Translate(0f - viewBox[0], 0f - viewBox[1]);
		}
		drawChildren(canvas, root, new Dictionary<string, string>());
		Picture = recorder.EndRecording();
	}

	public static SvgPicture Load(string path)
	{
		return new SvgPicture(XDocument.Load(path).Root);
	}

	public static SvgPicture Parse(string xml)
	{
		return new SvgPicture(XDocument.Parse(xml).Root);
	}

	public void Dispose()
	{
		Picture.Dispose();
	}

	private void parseStyle(string css)
	{
		css = Regex.Replace(css, "/\\*.*?\\*/", string.Empty, RegexOptions.Singleline);
		foreach (Match rule in ruleRegex.Matches(css))
		{
			Dictionary<string, string> declarations = parseDeclarations(rule.Groups[2].Value);
			foreach (string selector in rule.Groups[1].Value.Split(',').Select((string s) => s.Trim()))
			{
				if (selector.StartsWith(".") && !selector.Contains(' '))
				{
					string name = selector.Substring(1);
					if (!classes.TryGetValue(name, out var properties))
					{
						properties = (classes[name] = new Dictionary<string, string>());
					}
					foreach (KeyValuePair<string, string> declaration in declarations)
					{
						properties[declaration.Key] = declaration.Value;
					}
				}
			}
		}
	}

	private static Dictionary<string, string> parseDeclarations(string text)
	{
		Dictionary<string, string> declarations = new Dictionary<string, string>();
		foreach (string declaration in text.Split(';'))
		{
			int colon = declaration.IndexOf(':');
			if (colon > 0)
			{
				declarations[declaration.Substring(0, colon).Trim()] = declaration.Substring(colon + 1).Trim();
			}
		}
		return declarations;
	}

	private void drawChildren(SKCanvas canvas, XElement parent, Dictionary<string, string> inherited)
	{
		foreach (XElement child in parent.Elements())
		{
			draw(canvas, child, inherited);
		}
	}

	// Cascade: inherited values, then class rules, then presentation attributes, then the style attribute.
	// Opacity is not inherited by CSS, but multiplying it down the tree gives the same result for these files.
	private Dictionary<string, string> resolveStyle(XElement element, Dictionary<string, string> inherited)
	{
		Dictionary<string, string> style = new Dictionary<string, string>(inherited);
		style.Remove("opacity");
		string classNames = element.Attribute("class")?.Value;
		if (classNames != null)
		{
			foreach (string name in classNames.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (classes.TryGetValue(name, out var properties))
				{
					foreach (KeyValuePair<string, string> property in properties)
					{
						style[property.Key] = property.Value;
					}
				}
			}
		}
		foreach (string attribute in presentationAttributes)
		{
			string value = element.Attribute(attribute)?.Value;
			if (value != null)
			{
				style[attribute] = value;
			}
		}
		string inline = element.Attribute("style")?.Value;
		if (inline != null)
		{
			foreach (KeyValuePair<string, string> declaration in parseDeclarations(inline))
			{
				style[declaration.Key] = declaration.Value;
			}
		}
		float opacity = number(style.GetValueOrDefault("opacity"), 1f) * number(inherited.GetValueOrDefault("group-opacity"), 1f);
		style["group-opacity"] = opacity.ToString(CultureInfo.InvariantCulture);
		return style;
	}

	private void draw(SKCanvas canvas, XElement element, Dictionary<string, string> inherited)
	{
		string localName = element.Name.LocalName;
		if (localName == "defs" || localName == "style" || localName == "title")
		{
			return;
		}
		Dictionary<string, string> style = resolveStyle(element, inherited);
		canvas.Save();
		applyTransform(canvas, element.Attribute("transform")?.Value);
		SKPath path = null;
		switch (localName)
		{
		case "g":
			drawChildren(canvas, element, style);
			break;
		case "path":
			path = SKPath.ParseSvgPathData(element.Attribute("d")?.Value ?? string.Empty);
			break;
		case "line":
			path = new SKPath();
			path.MoveTo(attribute(element, "x1"), attribute(element, "y1"));
			path.LineTo(attribute(element, "x2"), attribute(element, "y2"));
			break;
		case "circle":
			path = new SKPath();
			path.AddCircle(attribute(element, "cx"), attribute(element, "cy"), attribute(element, "r"));
			break;
		case "rect":
			path = new SKPath();
			path.AddRect(SKRect.Create(attribute(element, "x"), attribute(element, "y"), attribute(element, "width"), attribute(element, "height")));
			break;
		}
		if (path != null)
		{
			using (path)
			{
				drawShape(canvas, path, style, localName != "line");
			}
		}
		canvas.Restore();
	}

	// Lines have no area, so only their stroke shows.
	private static void drawShape(SKCanvas canvas, SKPath path, Dictionary<string, string> style, bool fill)
	{
		if (style.GetValueOrDefault("fill-rule") == "evenodd")
		{
			path.FillType = SKPathFillType.EvenOdd;
		}
		float opacity = number(style.GetValueOrDefault("group-opacity"), 1f);
		if (fill && tryColor(style.GetValueOrDefault("fill", "#000"), number(style.GetValueOrDefault("fill-opacity"), 1f) * opacity, out var fillColor))
		{
			using SKPaint paint = new SKPaint
			{
				IsAntialias = true,
				Style = SKPaintStyle.Fill,
				Color = fillColor
			};
			canvas.DrawPath(path, paint);
		}
		if (tryColor(style.GetValueOrDefault("stroke", "none"), number(style.GetValueOrDefault("stroke-opacity"), 1f) * opacity, out var strokeColor))
		{
			using SKPaint paint = new SKPaint
			{
				IsAntialias = true,
				Style = SKPaintStyle.Stroke,
				Color = strokeColor,
				StrokeWidth = number(style.GetValueOrDefault("stroke-width")?.Replace("px", string.Empty), 1f),
				StrokeCap = style.GetValueOrDefault("stroke-linecap") switch
				{
					"round" => SKStrokeCap.Round, 
					"square" => SKStrokeCap.Square, 
					_ => SKStrokeCap.Butt, 
				},
				StrokeJoin = style.GetValueOrDefault("stroke-linejoin") switch
				{
					"round" => SKStrokeJoin.Round, 
					"bevel" => SKStrokeJoin.Bevel, 
					_ => SKStrokeJoin.Miter, 
				}
			};
			canvas.DrawPath(path, paint);
		}
	}

	private static float attribute(XElement element, string name)
	{
		return number(element.Attribute(name)?.Value, 0f);
	}

	private static void applyTransform(SKCanvas canvas, string transform)
	{
		if (string.IsNullOrEmpty(transform))
		{
			return;
		}
		foreach (Match function in transformRegex.Matches(transform))
		{
			float[] args = numbers(function.Groups[2].Value);
			switch (function.Groups[1].Value)
			{
			case "translate":
				canvas.Translate(args.ElementAtOrDefault(0), args.ElementAtOrDefault(1));
				break;
			case "rotate":
				if (args.Length >= 3)
				{
					canvas.RotateDegrees(args[0], args[1], args[2]);
				}
				else
				{
					canvas.RotateDegrees(args.ElementAtOrDefault(0));
				}
				break;
			case "scale":
				canvas.Scale(args.ElementAtOrDefault(0), (args.Length > 1) ? args[1] : args.ElementAtOrDefault(0));
				break;
			case "matrix":
				if (args.Length == 6)
				{
					SKMatrix matrix = new SKMatrix(args[0], args[2], args[4], args[1], args[3], args[5], 0f, 0f, 1f);
					canvas.Concat(ref matrix);
				}
				break;
			}
		}
	}

	private static bool tryColor(string value, float opacity, out SKColor color)
	{
		color = SKColors.Transparent;
		if (string.IsNullOrEmpty(value) || value == "none" || value == "transparent")
		{
			return false;
		}
		value = value switch
		{
			"white" => "#fff",
			"black" => "#000",
			_ => value,
		};
		if (!SKColor.TryParse(value, out color))
		{
			return false;
		}
		color = color.WithAlpha((byte)Math.Round(color.Alpha * Math.Clamp(opacity, 0f, 1f)));
		return true;
	}

	private static float number(string value, float fallback)
	{
		if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return result;
	}

	private static float[] numbers(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return Array.Empty<float>();
		}
		return (from Match m in Regex.Matches(value, "-?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][-+]?\\d+)?")
			select float.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
	}
}
