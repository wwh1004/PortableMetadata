using System.Collections.Generic;
using System.Diagnostics;
using System;
using ElementType2 = MetadataSerialization.PortableComplexTypeFormatter.ElementType;

namespace MetadataSerialization;

public class PortableMethod(string name, PortableComplexType type, PortableComplexType signature) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	// TypeDefOrRef
	public PortableComplexType Type { get; set; } = type;

	// MethodSig
	public PortableComplexType Signature { get; set; } = signature;

	public override string ToString() {
		return Name;
	}
}

public struct PortableParameter(string name, int sequence, int attributes, PortableConstant? constant, IList<PortableCustomAttribute>? customAttributes) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	public int Sequence { get; set; } = sequence;

	public int Attributes { get; set; } = attributes;

	public PortableConstant? Constant { get; set; } = constant;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}

public struct PortableInstruction(string opCode, object? operand) {
	public string OpCode { get; set; } = opCode ?? throw new ArgumentNullException(nameof(opCode));

	// int/long/float/double/string/int[]/PortableComplexType
	// bool/char/sbyte/byte/short/ushort -> int
	public object? Operand { get; set; } = operand;

	[Obsolete("Reserved for deserialization.")]
	public long? PrimitiveValue {
		readonly get => PrimitivesHelper.ToSlot(Operand);
		set {
			Debug.Assert(OpCode is not null, "We assume OpCode is set before setting Operand.");
			if (value is null)
				return;
			int type;
			switch (OpCode) {
			case "ldc.i8":
				type = (int)ElementType2.I8;
				break;
			case "ldc.r4":
				type = (int)ElementType2.R4;
				break;
			case "ldc.r8":
				type = (int)ElementType2.R8;
				break;
			default:
				type = (int)ElementType2.I4;
				break;
			}
			if (PrimitivesHelper.FromSlot(value, type) is object slot)
				Operand = slot;
		}
	}

	[Obsolete("Reserved for deserialization.")]
	public string? StringValue {
		readonly get => Operand as string;
		set {
			Debug.Assert(OpCode is not null, "We assume OpCode is set before setting Operand.");
			if (OpCode == "ldstr" && value is string s)
				Operand = s;
		}
	}

	// Newtonsoft.Json's default behavior of deserializing List<T> is appending to the existing list instead of replacing it. Use fixed-size array can avoid some potential issues.
	[Obsolete("Reserved for deserialization.")]
	public int[]? Int32ArrayValue {
		readonly get => Operand as int[];
		set {
			Debug.Assert(OpCode is not null, "We assume OpCode is set before setting Operand.");
			if (OpCode == "switch" && value is int[] v)
				Operand = v;
		}
	}

	[Obsolete("Reserved for deserialization.")]
	public PortableComplexType? TypeValue {
		readonly get => Operand is PortableComplexType type ? type : default(PortableComplexType?);
		set {
			Debug.Assert(OpCode is not null, "We assume OpCode is set before setting Operand.");
			if (value is PortableComplexType type)
				Operand = type;
		}
	}
}

public struct PortableExceptionHandler(int tryStart, int tryEnd, int filterStart, int handlerStart, int handlerEnd, PortableComplexType? catchType, int handlerType) {
	public int TryStart { get; set; } = tryStart;

	public int TryEnd { get; set; } = tryEnd;

	public int FilterStart { get; set; } = filterStart;

	public int HandlerStart { get; set; } = handlerStart;

	public int HandlerEnd { get; set; } = handlerEnd;

	// TypeDefOrRef
	public PortableComplexType? CatchType { get; set; } = catchType;

	public int HandlerType { get; set; } = handlerType;
}

public struct PortableMethodBody(IList<PortableInstruction> instructions, IList<PortableExceptionHandler> exceptionHandlers, IList<PortableComplexType> variables) {
	public IList<PortableInstruction> Instructions { get; set; } = instructions;

	public IList<PortableExceptionHandler> ExceptionHandlers { get; set; } = exceptionHandlers;

	// TypeSig
	public IList<PortableComplexType> Variables { get; set; } = variables;
}


public struct PortableImplMap(string name, string module, int attributes) {
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	public string Module { get; set; } = module ?? throw new ArgumentNullException(nameof(module));

	public int Attributes { get; set; } = attributes;

	public override readonly string ToString() {
		return Name;
	}
}

public class PortableMethodDef(string name, PortableComplexType type, PortableComplexType signature, int attributes, int implAttributes,
	IList<PortableParameter> parameters, PortableMethodBody? body, IList<PortableToken>? overrides, PortableImplMap? implMap,
	IList<PortableGenericParameter>? genericParameters, IList<PortableCustomAttribute>? customAttributes)
	: PortableMethod(name, type, signature) {
	public int Attributes { get; set; } = attributes;

	public int ImplAttributes { get; set; } = implAttributes;

	public IList<PortableParameter> Parameters { get; set; } = parameters;

	public PortableMethodBody? Body { get; set; } = body;

	public IList<PortableToken>? Overrides { get; set; } = overrides;

	public PortableImplMap? ImplMap { get; set; } = implMap;

	public IList<PortableGenericParameter>? GenericParameters { get; set; } = genericParameters;

	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}
