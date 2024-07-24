using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MetadataSerialization;

sealed class STJPortableTokenConverter : JsonConverter<PortableToken> {
	public override PortableToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int index))
			return (PortableToken)index;
		else if (reader.TokenType == JsonTokenType.String)
			return (PortableToken)reader.GetString()!;
		else
			throw new InvalidDataException();
	}

	public override void Write(Utf8JsonWriter writer, PortableToken value, JsonSerializerOptions options) {
		if (value.Name is string name)
			writer.WriteStringValue(name);
		else
			writer.WriteNumberValue(value.Index);
	}
}

sealed class STJPortableComplexTypeConverter : JsonConverter<PortableComplexType> {
	public override PortableComplexType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
		return reader.GetString() is string s ? PortableComplexType.Parse(s) : throw new InvalidDataException();
	}

	public override void Write(Utf8JsonWriter writer, PortableComplexType value, JsonSerializerOptions options) {
		writer.WriteStringValue(value.ToString());
	}
}

static class STJPortableMetadataObjectPropertyRemover {
	public static void RemoveObjectProperties(JsonTypeInfo typeInfo) {
		if (typeInfo.Kind != JsonTypeInfoKind.Object)
			return;
		// Don't use Properties.RemoteAt, which breaks the source generator mode.
		foreach (var property in typeInfo.Properties) {
			if (property.PropertyType == typeof(object)) {
				property.Get = null;
				property.Set = null;
			}
		}
	}
}
