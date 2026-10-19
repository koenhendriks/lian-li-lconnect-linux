using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LConnect3cs.Products;

internal class RPMPhaseInfoDictionaryJsonConverter : JsonConverter<Dictionary<RPMPhaseKey, RPMPhaseInfo>>
{
	public override Dictionary<RPMPhaseKey, RPMPhaseInfo> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException();
		}
		Dictionary<RPMPhaseKey, RPMPhaseInfo> dictionary = new Dictionary<RPMPhaseKey, RPMPhaseInfo>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				throw new JsonException();
			}
			string? key = reader.GetString();
			if (!reader.Read())
			{
				throw new JsonException();
			}
			if (RPMPhaseKey.TryParse(key, out var phaseKey))
			{
				RPMPhaseInfo value = JsonSerializer.Deserialize<RPMPhaseInfo>(ref reader);
				dictionary.Add(phaseKey, value);
			}
		}
		return dictionary;
	}

	public override void Write(Utf8JsonWriter writer, Dictionary<RPMPhaseKey, RPMPhaseInfo> value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		foreach (KeyValuePair<RPMPhaseKey, RPMPhaseInfo> item in value)
		{
			writer.WritePropertyName(item.Key.ToString());
			JsonSerializer.Serialize(writer, item.Value);
		}
		writer.WriteEndObject();
	}
}
