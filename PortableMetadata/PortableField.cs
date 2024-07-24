using System.Collections.Generic;

namespace MetadataSerialization;

public class PortableField(string name, PortableComplexType type, PortableComplexType signature) {
	public string Name { get; set; } = name;

	// TypeDefOrRef
	public PortableComplexType Type { get; set; } = type;

	// FieldSig
	public PortableComplexType Signature { get; set; } = signature;

	public override string ToString() {
		return Name;
	}
}

public sealed class PortableFieldDef(string name, PortableComplexType type, PortableComplexType signature, int attributes,
	byte[]? initialValue, PortableConstant? constant, IList<PortableCustomAttribute>? customAttributes)
	: PortableField(name, type, signature) {
	public int Attributes { get; set; } = attributes;

	public byte[]? InitialValue { get; set; } = initialValue;

	public PortableConstant? Constant { get; set; } = constant;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}
