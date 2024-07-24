using System;
using System.Collections.Generic;

namespace MetadataSerialization;

public class PortableType(string name, string @namespace, string? assembly, IList<string>? enclosingNames) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	public string Namespace { get; set; } = @namespace ?? throw new ArgumentNullException(nameof(@namespace));

	// AssemblyRef
	public string? Assembly { get; set; } = assembly;

	public IList<string>? EnclosingNames { get; set; } = enclosingNames;

	public override string ToString() {
		return Name;
	}
}

public struct PortableClassLayout(int packingSize, int classSize) {
	public int PackingSize { get; set; } = packingSize;

	public int ClassSize { get; set; } = classSize;
}

public struct PortableProperty(string name, PortableComplexType signature, int attributes, PortableToken? getMethod, PortableToken? setMethod, IList<PortableCustomAttribute>? customAttributes) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	public PortableComplexType Signature { get; set; } = signature;

	public int Attributes { get; set; } = attributes;

	public PortableToken? GetMethod { get; set; } = getMethod;

	public PortableToken? SetMethod { get; set; } = setMethod;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	public override readonly string ToString() {
		return Name;
	}
}

public struct PortableEvent(string name, PortableComplexType type, int attributes, PortableToken? addMethod, PortableToken? removeMethod, PortableToken? invokeMethod, IList<PortableCustomAttribute>? customAttributes) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	public PortableComplexType Type { get; set; } = type;

	public int Attributes { get; set; } = attributes;

	public PortableToken? AddMethod { get; set; } = addMethod;

	public PortableToken? RemoveMethod { get; set; } = removeMethod;

	public PortableToken? InvokeMethod { get; set; } = invokeMethod;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	public override readonly string ToString() {
		return Name;
	}
}

public sealed class PortableTypeDef(string name, string @namespace, string? assembly, IList<string>? enclosingNames, int attributes,
	PortableComplexType? baseType, IList<PortableComplexType>? interfaces, PortableClassLayout? classLayout,
	 IList<PortableGenericParameter>? genericParameters, IList<PortableCustomAttribute>? customAttributes)
	: PortableType(name, @namespace, assembly, enclosingNames) {
	public int Attributes { get; set; } = attributes;

	// TypeDefOrRef
	public PortableComplexType? BaseType { get; set; } = baseType;

	// TypeDefOrRef
	public IList<PortableComplexType>? Interfaces { get; set; } = interfaces;

	public PortableClassLayout? ClassLayout { get; set; } = classLayout;

	public IList<PortableGenericParameter>? GenericParameters { get; set; } = genericParameters;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;

	public IList<PortableToken>? NestedTypes { get; set; }

	public IList<PortableToken>? Fields { get; set; }

	public IList<PortableToken>? Methods { get; set; }

	public IList<PortableProperty>? Properties { get; set; }

	public IList<PortableEvent>? Events { get; set; }
}
