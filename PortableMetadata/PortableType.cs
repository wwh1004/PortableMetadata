using System;
using System.Collections.Generic;

namespace MetadataSerialization;

/// <summary>
/// Represents a portable type.
/// </summary>
/// <param name="name">The name of the type.</param>
/// <param name="namespace">The namespace of the type.</param>
/// <param name="assembly">The assembly of the type.</param>
/// <param name="enclosingNames">The enclosing names of the type.</param>
public class PortableType(string name, string @namespace, string? assembly, IList<string>? enclosingNames) {
	/// <summary>
	/// Gets or sets the name of the type.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the namespace of the type.
	/// </summary>
	public string Namespace { get; set; } = @namespace ?? throw new ArgumentNullException(nameof(@namespace));

	/// <summary>
	/// Gets or sets the assembly of the type.
	/// </summary>
	public string? Assembly { get; set; } = assembly;

	/// <summary>
	/// Gets or sets the enclosing names of the type.
	/// </summary>
	public IList<string>? EnclosingNames { get; set; } = enclosingNames;

	/// <summary>
	/// Returns the name of the type.
	/// </summary>
	/// <returns>The name of the type.</returns>
	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents the layout of a type.
/// </summary>
/// <param name="packingSize">The packing size of the type.</param>
/// <param name="classSize">The size of the type.</param>
public struct PortableClassLayout(int packingSize, int classSize) {
	/// <summary>
	/// Gets or sets the packing size of the type.
	/// </summary>
	public int PackingSize { get; set; } = packingSize;

	/// <summary>
	/// Gets or sets the size of the type.
	/// </summary>
	public int ClassSize { get; set; } = classSize;
}

/// <summary>
/// Represents a portable property.
/// </summary>
/// <param name="name">The name of the property.</param>
/// <param name="signature">The signature of the property.</param>
/// <param name="attributes">The attributes of the property.</param>
/// <param name="getMethod">The get method of the property.</param>
/// <param name="setMethod">The set method of the property.</param>
/// <param name="customAttributes">The custom attributes of the property.</param>
public struct PortableProperty(string name, PortableComplexType signature, int attributes, PortableToken? getMethod, PortableToken? setMethod, IList<PortableCustomAttribute>? customAttributes) {
	/// <summary>
	/// Gets or sets the name of the property.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the signature of the property.
	/// </summary>
	/// <remarks>PropertySig</remarks>
	public PortableComplexType Signature { get; set; } = signature;

	/// <summary>
	/// Gets or sets the attributes of the property.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the get method of the property.
	/// </summary>
	public PortableToken? GetMethod { get; set; } = getMethod;

	/// <summary>
	/// Gets or sets the set method of the property.
	/// </summary>
	public PortableToken? SetMethod { get; set; } = setMethod;

	/// <summary>
	/// Gets or sets the custom attributes of the property.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	/// <summary>
	/// Returns the name of the property.
	/// </summary>
	/// <returns>The name of the property.</returns>
	public override readonly string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents a portable event.
/// </summary>
/// <param name="name">The name of the event.</param>
/// <param name="type">The type of the event.</param>
/// <param name="attributes">The attributes of the event.</param>
/// <param name="addMethod">The add method of the event.</param>
/// <param name="removeMethod">The remove method of the event.</param>
/// <param name="invokeMethod">The invoke method of the event.</param>
/// <param name="customAttributes">The custom attributes of the event.</param>
public struct PortableEvent(string name, PortableComplexType type, int attributes, PortableToken? addMethod, PortableToken? removeMethod, PortableToken? invokeMethod, IList<PortableCustomAttribute>? customAttributes) {
	/// <summary>
	/// Gets or sets the name of the event.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the type of the event.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public PortableComplexType Type { get; set; } = type;

	/// <summary>
	/// Gets or sets the attributes of the event.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the add method of the event.
	/// </summary>
	public PortableToken? AddMethod { get; set; } = addMethod;

	/// <summary>
	/// Gets or sets the remove method of the event.
	/// </summary>
	public PortableToken? RemoveMethod { get; set; } = removeMethod;

	/// <summary>
	/// Gets or sets the invoke method of the event.
	/// </summary>
	public PortableToken? InvokeMethod { get; set; } = invokeMethod;

	/// <summary>
	/// Gets or sets the custom attributes of the event.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	/// <summary>
	/// Returns the name of the event.
	/// </summary>
	/// <returns>The name of the event.</returns>
	public override readonly string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents a portable type definition.
/// </summary>
/// <param name="name">The name of the type.</param>
/// <param name="namespace">The namespace of the type.</param>
/// <param name="assembly">The assembly of the type.</param>
/// <param name="enclosingNames">The enclosing names of the type.</param>
/// <param name="attributes">The attributes of the type.</param>
/// <param name="baseType">The base type of the type.</param>
/// <param name="interfaces">The interfaces implemented by the type.</param>
/// <param name="classLayout">The class layout of the type.</param>
/// <param name="genericParameters">The generic parameters of the type.</param>
/// <param name="customAttributes">The custom attributes of the type.</param>
public sealed class PortableTypeDef(string name, string @namespace, string? assembly, IList<string>? enclosingNames, int attributes,
	PortableComplexType? baseType, IList<PortableComplexType>? interfaces, PortableClassLayout? classLayout,
	IList<PortableGenericParameter>? genericParameters, IList<PortableCustomAttribute>? customAttributes)
	: PortableType(name, @namespace, assembly, enclosingNames) {
	/// <summary>
	/// Gets or sets the attributes of the type.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the base type of the type.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public PortableComplexType? BaseType { get; set; } = baseType;

	/// <summary>
	/// Gets or sets the interfaces implemented by the type.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public IList<PortableComplexType>? Interfaces { get; set; } = interfaces;

	/// <summary>
	/// Gets or sets the class layout of the type.
	/// </summary>
	public PortableClassLayout? ClassLayout { get; set; } = classLayout;

	/// <summary>
	/// Gets or sets the generic parameters of the type.
	/// </summary>
	public IList<PortableGenericParameter>? GenericParameters { get; set; } = genericParameters;

	/// <summary>
	/// Gets or sets the custom attributes of the type.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	/// <summary>
	/// Gets or sets the nested types of the type.
	/// </summary>
	public IList<PortableToken>? NestedTypes { get; set; }

	/// <summary>
	/// Gets or sets the fields of the type.
	/// </summary>
	public IList<PortableToken>? Fields { get; set; }

	/// <summary>
	/// Gets or sets the methods of the type.
	/// </summary>
	public IList<PortableToken>? Methods { get; set; }

	/// <summary>
	/// Gets or sets the properties of the type.
	/// </summary>
	public IList<PortableProperty>? Properties { get; set; }

	/// <summary>
	/// Gets or sets the events of the type.
	/// </summary>
	public IList<PortableEvent>? Events { get; set; }
}
