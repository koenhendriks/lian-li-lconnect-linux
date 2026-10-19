using System;
using System.Text.Json;
using System.Text.Json.Serialization;

// Minimal stand-in for WPF's System.Windows.Media.Color so the decompiled code compiles unchanged on Linux.
// The JSON shape matches what System.Text.Json produces for the WPF type, so settings files round-trip
// between this port and the Windows L-Connect 3 install.
namespace System.Windows.Media;

[JsonConverter(typeof(ColorJsonConverter))]
public struct Color : IEquatable<Color>
{
	public byte A { get; set; }

	public byte R { get; set; }

	public byte G { get; set; }

	public byte B { get; set; }

	public float ScA => A / 255f;

	public float ScR => SRgbToScRgb(R);

	public float ScG => SRgbToScRgb(G);

	public float ScB => SRgbToScRgb(B);

	public static Color FromArgb(byte a, byte r, byte g, byte b)
	{
		return new Color { A = a, R = r, G = g, B = b };
	}

	public static Color FromRgb(byte r, byte g, byte b)
	{
		return FromArgb(255, r, g, b);
	}

	// Same transfer function WPF uses for its ScRGB properties.
	private static float SRgbToScRgb(byte value)
	{
		float num = value / 255f;
		if (num <= 0.04045f)
		{
			return num / 12.92f;
		}
		return (float)Math.Pow((num + 0.055) / 1.055, 2.4);
	}

	public bool Equals(Color other)
	{
		return A == other.A && R == other.R && G == other.G && B == other.B;
	}

	public override bool Equals(object obj)
	{
		return obj is Color other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(A, R, G, B);
	}

	public static bool operator ==(Color left, Color right)
	{
		return left.Equals(right);
	}

	public static bool operator !=(Color left, Color right)
	{
		return !left.Equals(right);
	}

	public override string ToString()
	{
		return $"#{A:X2}{R:X2}{G:X2}{B:X2}";
	}
}

public static class Colors
{
	public static Color Black => Color.FromRgb(0, 0, 0);

	public static Color White => Color.FromRgb(255, 255, 255);

	public static Color Red => Color.FromRgb(255, 0, 0);

	// WPF's named Green is #008000, not #00FF00.
	public static Color Green => Color.FromRgb(0, 128, 0);

	public static Color Blue => Color.FromRgb(0, 0, 255);

	public static Color Yellow => Color.FromRgb(255, 255, 0);

	public static Color Cyan => Color.FromRgb(0, 255, 255);

	public static Color Orange => Color.FromRgb(255, 165, 0);
}

public sealed class ColorJsonConverter : JsonConverter<Color>
{
	public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException("Expected object for Color");
		}
		Color result = Color.FromArgb(255, 0, 0, 0);
		while (reader.Read())
		{
			if (reader.TokenType == JsonTokenType.EndObject)
			{
				return result;
			}
			string name = reader.GetString();
			reader.Read();
			switch (name)
			{
			case "A":
				result.A = reader.GetByte();
				break;
			case "R":
				result.R = reader.GetByte();
				break;
			case "G":
				result.G = reader.GetByte();
				break;
			case "B":
				result.B = reader.GetByte();
				break;
			default:
				reader.Skip();
				break;
			}
		}
		throw new JsonException("Unterminated Color object");
	}

	public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteNull("ColorContext");
		writer.WriteNumber("A", value.A);
		writer.WriteNumber("R", value.R);
		writer.WriteNumber("G", value.G);
		writer.WriteNumber("B", value.B);
		writer.WriteNumber("ScA", value.ScA);
		writer.WriteNumber("ScR", value.ScR);
		writer.WriteNumber("ScG", value.ScG);
		writer.WriteNumber("ScB", value.ScB);
		writer.WriteEndObject();
	}
}
