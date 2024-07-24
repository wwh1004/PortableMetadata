using System.Collections.Generic;

namespace MetadataSerialization;

/// <summary>
/// Represents a portable field.
/// </summary>
/// <param name="name">The name of the field.</param>
/// <param name="type">The declaring type of the field.</param>
/// <param name="signature">The signature of the field.</param>
public class PortableField(string name, PortableComplexType type, PortableComplexType signature) {
	/// <summary>
	/// Gets or sets the name of the field.
	/// </summary>
	public string Name { get; set; } = name;

	/// <summary>
	/// Gets or sets the declaring type of the field.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public PortableComplexType Type { get; set; } = type;

	/// <summary>
	/// Gets or sets the signature of the field.
	/// </summary>
	/// <remarks>FieldSig</remarks>
	public PortableComplexType Signature { get; set; } = signature;

	/// <summary>
	/// Returns the name of the field.
	/// </summary>
	/// <returns>The name of the field.</returns>
	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents a portable field definition.
/// </summary>
/// <param name="name">The name of the field.</param>
/// <param name="type">The declaring type of the field.</param>
/// <param name="signature">The signature of the field.</param>
/// <param name="attributes">The attributes of the field.</param>
/// <param name="initialValue">The initial value of the field.</param>
/// <param name="constant">The constant value of the field.</param>
/// <param name="customAttributes">The custom attributes of the field.</param>
public sealed class PortableFieldDef(string name, PortableComplexType type, PortableComplexType signature, int attributes,
	byte[]? initialValue, PortableConstant? constant, IList<PortableCustomAttribute>? customAttributes)
	: PortableField(name, type, signature) {
	/// <summary>
	/// Gets or sets the attributes of the field.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the initial value of the field.
	/// </summary>
	public byte[]? InitialValue { get; set; } = initialValue;

	/// <summary>
	/// Gets or sets the constant value of the field.
	/// </summary>
	public PortableConstant? Constant { get; set; } = constant;

	/// <summary>
	/// Gets or sets the custom attributes of the field.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}
