using System.Collections.Generic;
using System.Diagnostics;
using System;
using ElementType2 = MetadataSerialization.PortableComplexTypeFormatter.ElementType;

namespace MetadataSerialization;

/// <summary>
/// Represents a portable method.
/// </summary>
/// <param name="name">The name of the method.</param>
/// <param name="type">The declaring type of the method.</param>
/// <param name="signature">The signature of the method.</param>
public class PortableMethod(string name, PortableComplexType type, PortableComplexType signature) {
	/// <summary>
	/// Gets or sets the name of the method.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the declaring type of the method.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public PortableComplexType Type { get; set; } = type;

	/// <summary>
	/// Gets or sets the signature of the method.
	/// </summary>
	/// <remarks>MethodSig</remarks>
	public PortableComplexType Signature { get; set; } = signature;

	/// <summary>
	/// Returns the name of the method.
	/// </summary>
	/// <returns>The name of the method.</returns>
	public override string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents a portable parameter.
/// </summary>
/// <param name="name">The name of the parameter.</param>
/// <param name="sequence">The sequence of the parameter.</param>
/// <param name="attributes">The attributes of the parameter.</param>
/// <param name="constant">The constant value of the parameter.</param>
/// <param name="customAttributes">The custom attributes of the parameter.</param>
public struct PortableParameter(string name, int sequence, int attributes, PortableConstant? constant, IList<PortableCustomAttribute>? customAttributes) {
	/// <summary>
	/// Gets or sets the name of the parameter.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the sequence of the parameter.
	/// </summary>
	public int Sequence { get; set; } = sequence;

	/// <summary>
	/// Gets or sets the attributes of the parameter.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the constant value of the parameter.
	/// </summary>
	public PortableConstant? Constant { get; set; } = constant;

	/// <summary>
	/// Gets or sets the custom attributes of the parameter.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}

/// <summary>
/// Represents a portable instruction.
/// </summary>
/// <param name="opCode">The opcode of the instruction.</param>
/// <param name="operand">The operand of the instruction.</param>
public struct PortableInstruction(string opCode, object? operand) {
	/// <summary>
	/// Gets or sets the opcode of the instruction.
	/// </summary>
	public string OpCode { get; set; } = opCode ?? throw new ArgumentNullException(nameof(opCode));

	/// <summary>
	/// Gets or sets the operand of the instruction.
	/// </summary>
	/// <remarks>
	/// The operand can be of type <see langword="int"/>, <see langword="long"/>, <see langword="float"/>, <see langword="double"/>, <see langword="string"/>, <see langword="int[]"/>, or <see cref="PortableComplexType"/>.
	/// For <see langword="bool"/>, <see langword="char"/>, <see langword="sbyte"/>, <see langword="byte"/>, <see langword="short"/>, <see langword="ushort"/> types, they should be stored as <see langword="int"/>.
	/// </remarks>
	public object? Operand { get; set; } = operand;

	/// <summary/>
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

	/// <summary/>
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
	/// <summary/>
	[Obsolete("Reserved for deserialization.")]
	public int[]? Int32ArrayValue {
		readonly get => Operand as int[];
		set {
			Debug.Assert(OpCode is not null, "We assume OpCode is set before setting Operand.");
			if (OpCode == "switch" && value is int[] v)
				Operand = v;
		}
	}

	/// <summary/>
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

/// <summary>
/// Represents a portable method.
/// </summary>
/// <param name="tryStart">The start index of the try block.</param>
/// <param name="tryEnd">The end index of the try block.</param>
/// <param name="filterStart">The start index of the filter block.</param>
/// <param name="handlerStart">The start index of the handler block.</param>
/// <param name="handlerEnd">The end index of the handler block.</param>
/// <param name="catchType">The catch type of the exception handler.</param>
/// <param name="handlerType">The type of the exception handler.</param>
public struct PortableExceptionHandler(int tryStart, int tryEnd, int filterStart, int handlerStart, int handlerEnd, PortableComplexType? catchType, int handlerType) {
	/// <summary>
	/// Gets or sets the start index of the try block.
	/// </summary>
	public int TryStart { get; set; } = tryStart;

	/// <summary>
	/// Gets or sets the end index of the try block.
	/// </summary>
	public int TryEnd { get; set; } = tryEnd;

	/// <summary>
	/// Gets or sets the start index of the filter block.
	/// </summary>
	public int FilterStart { get; set; } = filterStart;

	/// <summary>
	/// Gets or sets the start index of the handler block.
	/// </summary>
	public int HandlerStart { get; set; } = handlerStart;

	/// <summary>
	/// Gets or sets the end index of the handler block.
	/// </summary>
	public int HandlerEnd { get; set; } = handlerEnd;

	/// <summary>
	/// Gets or sets the catch type of the exception handler.
	/// </summary>
	/// <remarks>TypeDefOrRef</remarks>
	public PortableComplexType? CatchType { get; set; } = catchType;

	/// <summary>
	/// Gets or sets the type of the exception handler.
	/// </summary>
	public int HandlerType { get; set; } = handlerType;
}

/// <summary>
/// Represents a portable method body.
/// </summary>
/// <param name="instructions">The list of instructions in the method body.</param>
/// <param name="exceptionHandlers">The list of exception handlers in the method body.</param>
/// <param name="variables">The list of variables in the method body.</param>
/// <param name="maxStack">The max stack value of the method body.</param>
/// <param name="initLocals">The init locals flag.</param>
public struct PortableMethodBody(IList<PortableInstruction> instructions, IList<PortableExceptionHandler> exceptionHandlers, IList<PortableComplexType> variables, int maxStack, bool initLocals) {
	/// <summary>
	/// Gets or sets the list of instructions in the method body.
	/// </summary>
	public IList<PortableInstruction> Instructions { get; set; } = instructions;

	/// <summary>
	/// Gets or sets the list of exception handlers in the method body.
	/// </summary>
	public IList<PortableExceptionHandler> ExceptionHandlers { get; set; } = exceptionHandlers;

	/// <summary>
	/// Gets or sets the list of variables in the method body.
	/// </summary>
	public IList<PortableComplexType> Variables { get; set; } = variables;

	/// <summary>
	/// Gets or sets the max stack value of the method body.
	/// </summary>
	public int MaxStack { get; set; } = maxStack;

	/// <summary>
	/// Gets or sets the init locals flag.
	/// </summary>
	public bool InitLocals { get; set; } = initLocals;
}

/// <summary>
/// Represents a portable PInvoke mapping.
/// </summary>
/// <param name="name">The name of the PInvoke method.</param>
/// <param name="module">The module name of the PInvoke method.</param>
/// <param name="attributes">The attributes of the PInvoke method.</param>
public struct PortableImplMap(string name, string module, int attributes) {
	/// <summary>
	/// Gets or sets the name of the PInvoke method.
	/// </summary>
	public string Name { get; set; } = name ?? throw new ArgumentNullException(nameof(name));

	/// <summary>
	/// Gets or sets the module name of the PInvoke method.
	/// </summary>
	public string Module { get; set; } = module ?? throw new ArgumentNullException(nameof(module));

	/// <summary>
	/// Gets or sets the attributes of the PInvoke method.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Returns the name of the PInvoke method.
	/// </summary>
	/// <returns>The name of the PInvoke method.</returns>
	public override readonly string ToString() {
		return Name;
	}
}

/// <summary>
/// Represents a portable method definition.
/// </summary>
/// <param name="name">The name of the method.</param>
/// <param name="type">The declaring type of the method.</param>
/// <param name="signature">The signature of the method.</param>
/// <param name="attributes">The attributes of the method.</param>
/// <param name="implAttributes">The implementation attributes of the method.</param>
/// <param name="parameters">The parameters of the method.</param>
/// <param name="body">The method body.</param>
/// <param name="overrides">The overridden methods.</param>
/// <param name="implMap">The PInvoke mapping information.</param>
/// <param name="genericParameters">The generic parameters of the method.</param>
/// <param name="customAttributes">The custom attributes of the method.</param>
public class PortableMethodDef(string name, PortableComplexType type, PortableComplexType signature, int attributes, int implAttributes,
	IList<PortableParameter> parameters, PortableMethodBody? body, IList<PortableToken>? overrides, PortableImplMap? implMap,
	IList<PortableGenericParameter>? genericParameters, IList<PortableCustomAttribute>? customAttributes)
	: PortableMethod(name, type, signature) {
	/// <summary>
	/// Gets or sets the attributes of the method.
	/// </summary>
	public int Attributes { get; set; } = attributes;

	/// <summary>
	/// Gets or sets the implementation attributes of the method.
	/// </summary>
	public int ImplAttributes { get; set; } = implAttributes;

	/// <summary>
	/// Gets or sets the parameters of the method.
	/// </summary>
	public IList<PortableParameter> Parameters { get; set; } = parameters;

	/// <summary>
	/// Gets or sets the method body.
	/// </summary>
	public PortableMethodBody? Body { get; set; } = body;

	/// <summary>
	/// Gets or sets the overridden methods.
	/// </summary>
	/// <remarks>MethodDefOrRef</remarks>
	public IList<PortableToken>? Overrides { get; set; } = overrides;

	/// <summary>
	/// Gets or sets the PInvoke mapping information.
	/// </summary>
	public PortableImplMap? ImplMap { get; set; } = implMap;

	/// <summary>
	/// Gets or sets the generic parameters of the method.
	/// </summary>
	public IList<PortableGenericParameter>? GenericParameters { get; set; } = genericParameters;

	/// <summary>
	/// Gets or sets the custom attributes of the method.
	/// </summary>
	public IList<PortableCustomAttribute>? CustomAttributes { get; set; } = customAttributes;
}
