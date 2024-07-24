using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using MetadataSerialization;

sealed class NJPortableTokenConverter : JsonConverter<PortableToken?> {
	public override PortableToken? ReadJson(JsonReader reader, Type objectType, PortableToken? existingValue, bool hasExistingValue, JsonSerializer serializer) {
		if (reader.Value is long index)
			return (int)index;
		else if (reader.Value is string name)
			return name;
		else
			return null;
	}

	public override void WriteJson(JsonWriter writer, PortableToken? value, JsonSerializer serializer) {
		if (value is not PortableToken token)
			writer.WriteNull();
		else if (token.Name is string name)
			writer.WriteValue(name);
		else
			writer.WriteValue(token.Index);
	}
}

sealed class NJPortableComplexTypeConverter : JsonConverter<PortableComplexType?> {
	public override PortableComplexType? ReadJson(JsonReader reader, Type objectType, PortableComplexType? existingValue, bool hasExistingValue, JsonSerializer serializer) {
		return reader.Value is string type ? PortableComplexType.Parse(type) : null;
	}

	public override void WriteJson(JsonWriter writer, PortableComplexType? value, JsonSerializer serializer) {
		if (value is PortableComplexType type)
			writer.WriteValue(type.ToString());
		else
			writer.WriteNull();
	}
}

sealed class NJPortableMetadataObjectPropertyRemover : DefaultContractResolver {
	protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
		var property = base.CreateProperty(member, memberSerialization);
		if (property.PropertyType == typeof(object))
			property.Ignored = true;
		return property;
	}
}
