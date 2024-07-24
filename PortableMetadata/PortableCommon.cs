using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ElementType2 = MetadataSerialization.PortableComplexTypeFormatter.ElementType;

namespace MetadataSerialization;

// Metadata token
public readonly struct PortableToken : IEquatable<PortableToken>, IComparable<PortableToken> {
	public static readonly char[] InvalidNameChars = ['(', ')', ',', '@'];

	public int Index { get; init; }

	public string? Name { get; init; }

	public PortableToken(int index) {
		Index = index;
		Name = null;
	}

	public PortableToken(string name) {
		if (string.IsNullOrEmpty(name))
			throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));

		Debug.Assert(name.IndexOfAny(InvalidNameChars) == -1, "Contains invalid characters.");
		Index = 0;
		Name = name;
	}

	public bool Equals(PortableToken other) {
		return Index == other.Index && Name == other.Name;
	}

	public override bool Equals(object? obj) {
		return obj is PortableToken other && Equals(other);
	}

	public int CompareTo(PortableToken other) {
		if (Name is not null) {
			// Named tokens are always lesser than unnamed tokens
			if (other.Name is null)
				return -1;
			return Name.CompareTo(other.Name);
		}
		else {
			// Unnamed tokens
			if (other.Name is not null)
				return 1;
			return Index.CompareTo(other.Index);
		}
	}

	public override int GetHashCode() {
		return Name is not null ? Name.GetHashCode() : Index;
	}

	public override string ToString() {
		return Name is not null ? Name : Index.ToString();
	}

	#region Operators
	public static implicit operator PortableToken(int index) { return new PortableToken(index); }
	public static implicit operator PortableToken(string name) { return new PortableToken(name); }
	public static explicit operator int(PortableToken token) { return token.Index; }
	public static explicit operator string(PortableToken token) { return token.Name ?? throw new InvalidCastException("Token is not named."); }
	public static bool operator ==(PortableToken x, PortableToken y) { return x.Equals(y); }
	public static bool operator !=(PortableToken x, PortableToken y) { return !x.Equals(y); }
	public static bool operator <(PortableToken x, PortableToken y) { return x.CompareTo(y) < 0; }
	public static bool operator >(PortableToken x, PortableToken y) { return x.CompareTo(y) > 0; }
	public static bool operator <=(PortableToken x, PortableToken y) { return x.CompareTo(y) <= 0; }
	public static bool operator >=(PortableToken x, PortableToken y) { return x.CompareTo(y) >= 0; }
	#endregion
}

public enum PortableComplexTypeKind : byte {
	Token,
	TypeSig,
	CallingConventionSig,
	Int32,
	MethodSpec,
	InlineType,
	InlineField,
	InlineMethod
}

// TypeDef/TypeRef/TypeSpec/TypeDefOrRef
// FieldDef/MemberRef
// MethodDef/MemberRef/MethodSpec/MethodDefOrRef
// TypeSig/CallingConventionSig
public struct PortableComplexType {
	public PortableComplexTypeKind Kind { get; set; }

	public PortableToken Token { get; set; }

	public byte Type { get; set; }

	public IList<PortableComplexType>? Arguments { get; set; }

	public readonly int GetInt32() {
		return Kind == PortableComplexTypeKind.Int32 ? Token.Index : throw new InvalidOperationException();
	}

	private PortableComplexType(PortableComplexTypeKind kind, PortableToken token, byte type, IList<PortableComplexType>? arguments) {
		Kind = kind;
		Token = token;
		Type = type;
		Arguments = arguments;
	}

	public static PortableComplexType CreateToken(PortableToken token) {
		return new PortableComplexType(PortableComplexTypeKind.Token, token, 0, null);
	}

	public static PortableComplexType CreateTypeSig(byte elementType, IList<PortableComplexType>? arguments) {
		return new PortableComplexType(PortableComplexTypeKind.TypeSig, default, elementType, arguments);
	}

	public static PortableComplexType CreateCallingConventionSig(byte callingConvention, IList<PortableComplexType>? arguments) {
		return new PortableComplexType(PortableComplexTypeKind.CallingConventionSig, default, callingConvention, arguments);
	}

	public static PortableComplexType CreateInt32(int value) {
		return new PortableComplexType(PortableComplexTypeKind.Int32, value, 0, null);
	}

	public static PortableComplexType CreateMethodSpec(PortableComplexType method, PortableComplexType instantiation) {
		return new PortableComplexType(PortableComplexTypeKind.MethodSpec, default, 0, [method, instantiation]);
	}

	public static PortableComplexType CreateInlineTokenOperand(PortableComplexTypeKind kind, PortableComplexType type) {
		if (kind < PortableComplexTypeKind.InlineType || kind > PortableComplexTypeKind.InlineMethod)
			throw new ArgumentOutOfRangeException(nameof(kind));
		return new PortableComplexType(kind, default, 0, [type]);
	}

	public static PortableComplexType Parse(string s) {
		if (string.IsNullOrEmpty(s))
			throw new ArgumentException($"'{nameof(s)}' cannot be null or empty.", nameof(s));

		return PortableComplexTypeFormatter.Parse(s);
	}

	public override readonly string ToString() {
		return PortableComplexTypeFormatter.ToString(this);
	}
}

public struct PortableConstant(int type, object? value) {
	public int Type { get; set; } = type;

	// bool/char/sbyte/byte/short/ushort/int/uint/long/ulong/float/double/string
	public object? Value { get; set; } = value;

	[Obsolete("Reserved for deserialization.")]
	public long? PrimitiveValue {
		readonly get => PrimitivesHelper.ToSlot(Value);
		set {
			if (PrimitivesHelper.FromSlot(value, Type) is object slot)
				Value = slot;
		}
	}

	[Obsolete("Reserved for deserialization.")]
	public string? StringValue {
		readonly get => Value as string;
		set {
			if (Type == 0x0E && value is string s)
				Value = s;
		}
	}

	public override readonly string? ToString() {
		return Value?.ToString();
	}
}

public struct PortableGenericParameter(string name, int attributes, int number, IList<PortableComplexType>? constraints) {
	public string Name { get; set; } = name;

	public int Attributes { get; set; } = attributes;

	public int Number { get; set; } = number;

	// TypeDefOrRef
	public IList<PortableComplexType>? Constraints { get; set; } = constraints;

	public override readonly string ToString() {
		return Name;
	}
}

public struct PortableCustomAttribute(PortableToken constructor, byte[] rawData) {
	// MethodDefOrRef
	public PortableToken Constructor { get; set; } = constructor;

	public byte[] RawData { get; set; } = rawData ?? throw new ArgumentNullException(nameof(rawData));
}

static class PortableComplexTypeFormatter {
	const char TokenNameQuote = '\'';

	#region Parse
	public static PortableComplexType Parse(string s) {
		if (string.IsNullOrEmpty(s))
			throw new ArgumentException($"'{nameof(s)}' cannot be null or empty.", nameof(s));

		int index = 0;
		return ReadType(s, ref index);
	}

	static PortableComplexType ReadType(string s, ref int index) {
		var next = ReadNext(s, ref index);
		if (TryParseToken(next, out var token))
			return PortableComplexType.CreateToken(token);
		if (EnumTryParse<ElementType2>(next, out var et))
			return PortableComplexType.CreateTypeSig((byte)et, ReadElementTypeArgs(s, ref index, et));
		if (EnumTryParse<CallingConvention>(next, out var cc))
			return PortableComplexType.CreateCallingConventionSig((byte)cc, ReadCallingConventionArgs(s, ref index, cc));
		if (EnumTryParse<SpecialType>(next, out var st))
			return ReadSpecialType(s, ref index, st);
		throw new InvalidDataException($"Invalid complex type beginning: {next}.");

#if NETFRAMEWORK && !NET40_OR_GREATER
		static bool EnumTryParse<TEnum>(string value, out TEnum result) where TEnum : struct {
			if (Enum.IsDefined(typeof(TEnum), value)) {
				result = (TEnum)Enum.Parse(typeof(TEnum), value);
				return true;
			}
			else {
				result = default;
				return false;
			}
		}
#else
		static bool EnumTryParse<TEnum>(string value, out TEnum result) where TEnum : struct {
			return Enum.TryParse<TEnum>(value, out result);
		}
#endif
	}

	static bool TryParseToken(string s, out PortableToken token) {
		if (s.Length > 2 && s[0] == TokenNameQuote && s[s.Length - 1] == TokenNameQuote) {
			token = s.Substring(1, s.Length - 2);
			return true;
		}
		if (int.TryParse(s, out int index)) {
			token = index;
			return true;
		}
		token = default;
		return false;
	}

	static List<PortableComplexType>? ReadElementTypeArgs(string s, ref int index, ElementType2 elementType) {
		switch (elementType) {
		case ElementType2.End:
		case ElementType2.Void:
		case ElementType2.Boolean:
		case ElementType2.Char:
		case ElementType2.I1:
		case ElementType2.U1:
		case ElementType2.I2:
		case ElementType2.U2:
		case ElementType2.I4:
		case ElementType2.U4:
		case ElementType2.I8:
		case ElementType2.U8:
		case ElementType2.R4:
		case ElementType2.R8:
		case ElementType2.String:
		case ElementType2.TypedByRef:
		case ElementType2.I:
		case ElementType2.U:
		case ElementType2.R:
		case ElementType2.Object:
		case ElementType2.Sentinel:
			return null;
		}

		if (!ReadUntil(s, ref index, '('))
			throw new InvalidDataException("Can't find '('.");

		List<PortableComplexType> arguments;
		switch (elementType) {
		case ElementType2.Ptr:
		case ElementType2.ByRef:
		case ElementType2.FnPtr:
		case ElementType2.SZArray:
		case ElementType2.Pinned:
			// et(next)
			arguments = [ReadType(s, ref index)];
			break;

		case ElementType2.ValueType:
		case ElementType2.Class:
			// et(next)
			arguments = [ReadType(s, ref index)];
			break;

		case ElementType2.Var:
		case ElementType2.MVar:
			// et(index)
			arguments = [ReadInt32(s, ref index)];
			break;

		case ElementType2.Array: {
			// et(next, rank, numSizes, .. sizes, numLowerBounds, .. lowerBounds)
			arguments = [];
			var nextType = ReadType(s, ref index);
			arguments.Add(nextType);
			arguments.Add(ReadInt32(s, ref index, out int rank));
			arguments.Add(ReadInt32(s, ref index, out int numSizes));
			for (int i = 0; i < numSizes; i++)
				arguments.Add(ReadInt32(s, ref index));
			arguments.Add(ReadInt32(s, ref index, out int numLowerBounds));
			for (int i = 0; i < numLowerBounds; i++)
				arguments.Add(ReadInt32(s, ref index));
			break;
		}

		case ElementType2.GenericInst: {
			// et(next, num, .. args)
			arguments = [];
			var nextType = ReadType(s, ref index);
			arguments.Add(nextType);
			arguments.Add(ReadInt32(s, ref index, out int num));
			for (int i = 0; i < num; i++)
				arguments.Add(ReadType(s, ref index));
			break;
		}

		case ElementType2.ValueArray:
			// et(next, size)
			arguments = [ReadType(s, ref index), ReadInt32(s, ref index)];
			break;

		case ElementType2.CModReqd:
		case ElementType2.CModOpt:
			// et(modifier, next)
			arguments = [ReadType(s, ref index), ReadType(s, ref index)];
			break;

		case ElementType2.Module:
			// et(index, next)
			arguments = [ReadInt32(s, ref index), ReadType(s, ref index)];
			break;

		default:
			throw new InvalidOperationException();
		}

		if (!ReadUntil(s, ref index, ')'))
			throw new InvalidDataException("Can't find ')'.");

		return arguments;
	}

	static List<PortableComplexType> ReadCallingConventionArgs(string s, ref int index, CallingConvention callingConvention) {
		if (!ReadUntil(s, ref index, '('))
			throw new InvalidDataException("Can't find '('.");

		List<PortableComplexType> arguments = [ReadInt32(s, ref index, out int flags)];
		switch (callingConvention) {
		case CallingConvention.Default:
		case CallingConvention.C:
		case CallingConvention.StdCall:
		case CallingConvention.ThisCall:
		case CallingConvention.FastCall:
		case CallingConvention.VarArg:
		case CallingConvention.Property:
		case CallingConvention.Unmanaged:
		case CallingConvention.NativeVarArg:
			// cc([numGPs], numParams, retType, .. params)
			bool generic = (flags & 0x10) != 0;
			if (generic) // GenericParam count
				arguments.Add(ReadInt32(s, ref index));
			arguments.Add(ReadInt32(s, ref index, out int parameterCount));
			var returnType = ReadType(s, ref index);
			arguments.Add(returnType);
			for (int i = 0; i < parameterCount; i++) {
				var sig = ReadType(s, ref index);
				if (sig.Type == (byte)ElementType2.Sentinel) {
					i--;
					continue;
				}
				arguments.Add(sig);
			}
			break;

		case CallingConvention.Field:
			// cc(fieldType)
			arguments.Add(ReadType(s, ref index));
			break;

		case CallingConvention.LocalSig:
			// cc(numLocals, .. locals)
			arguments.Add(ReadInt32(s, ref index, out int numLocals));
			for (int i = 0; i < numLocals; i++)
				arguments.Add(ReadType(s, ref index));
			break;

		case CallingConvention.GenericInstCC:
			// cc(numArgs, .. args)
			arguments.Add(ReadInt32(s, ref index, out int numArgs));
			for (int i = 0; i < numArgs; i++)
				arguments.Add(ReadType(s, ref index));
			break;

		default:
			throw new InvalidOperationException();
		}

		if (!ReadUntil(s, ref index, ')'))
			throw new InvalidDataException("Can't find ')'.");

		return arguments;
	}

	static PortableComplexType ReadSpecialType(string s, ref int index, SpecialType specialType) {
		if (!ReadUntil(s, ref index, '('))
			throw new InvalidDataException("Can't find '('.");

		PortableComplexType type;
		switch (specialType) {
		case SpecialType.Int32:
			type = PortableComplexType.CreateInt32(int.Parse(ReadNext(s, ref index)));
			break;
		case SpecialType.MethodSpec:
			type = PortableComplexType.CreateMethodSpec(ReadType(s, ref index), ReadType(s, ref index));
			break;
		case SpecialType.InlineType:
		case SpecialType.InlineField:
		case SpecialType.InlineMethod:
			type = PortableComplexType.CreateInlineTokenOperand(PortableComplexTypeKind.Int32 + (byte)specialType, ReadType(s, ref index));
			break;
		default:
			throw new InvalidOperationException();
		}

		if (!ReadUntil(s, ref index, ')'))
			throw new InvalidDataException("Can't find ')'.");

		return type;
	}

	static PortableComplexType ReadInt32(string s, ref int index) {
		return ReadInt32(s, ref index, out _);
	}

	static PortableComplexType ReadInt32(string s, ref int index, out int value) {
		var type = ReadType(s, ref index);
		value = type.GetInt32();
		return type;
	}

	static string ReadNext(string s, ref int index) {
		var sb = new StringBuilder();
		while (index < s.Length) {
			var c = s[index++];
			if (c == ',') {
				if (sb.Length == 0)
					continue; // Skip leading commas
				break;
			}
			if (c == '(' || c == ')') {
				index--;
				break;
			}
			sb.Append(c);
		}
		Debug.Assert(sb.Length != 0);
		var result = sb.ToString();
		return result;
	}

	static bool ReadUntil(string s, ref int index, char c) {
		while (index < s.Length) {
			if (s[index++] == c)
				return true;
		}
		return false;
	}
	#endregion

	#region ToString
	public static string ToString(PortableComplexType type) {
		var sb = new StringBuilder();
		WriteType(type, sb);
		return sb.ToString();
	}

	static void WriteType(PortableComplexType type, StringBuilder sb) {
		switch (type.Kind) {
		case PortableComplexTypeKind.Token:
			if (type.Token.Name is string name)
				sb.Append(TokenNameQuote).Append(name).Append(TokenNameQuote);
			else
				sb.Append(type.Token.Index);
			return;
		case PortableComplexTypeKind.TypeSig:
			var elementType = (ElementType2)type.Type;
			if (!Enum.IsDefined(typeof(ElementType2), elementType))
				throw new InvalidDataException($"Invalid element type: {type.Type}.");
			sb.Append(elementType.ToString());
			WriteArgs(type.Arguments, sb);
			return;
		case PortableComplexTypeKind.CallingConventionSig:
			var callingConvention = (CallingConvention)type.Type;
			if (!Enum.IsDefined(typeof(CallingConvention), callingConvention))
				throw new InvalidDataException($"Invalid calling convention: {type.Type}.");
			sb.Append(callingConvention.ToString());
			WriteArgs(type.Arguments, sb);
			return;
		default:
			var specialType = (SpecialType)(type.Kind - PortableComplexTypeKind.Int32);
			if (!Enum.IsDefined(typeof(SpecialType), specialType))
				throw new InvalidDataException($"Invalid complex type kind: {type.Kind}.");
			sb.Append(specialType);
			if (specialType == SpecialType.Int32)
				sb.Append('(').Append(type.GetInt32()).Append(')');
			else
				WriteArgs(type.Arguments, sb);
			return;
		}
	}

	static void WriteArgs(IList<PortableComplexType>? arguments, StringBuilder sb) {
		Debug.Assert(arguments is null || arguments.Count != 0, "Arguments should be null if empty.");
		if (arguments is null || arguments.Count == 0)
			return;
		sb.Append('(');
		foreach (var arg in arguments) {
			WriteType(arg, sb);
			sb.Append(',');
		}
		sb.Remove(sb.Length - 1, 1);
		sb.Append(')');
	}
	#endregion

	internal static PortableToken? GetScopeType(PortableComplexType type) {
		if (type.Kind != PortableComplexTypeKind.TypeSig)
			return null;

		var args = type.Arguments;
		switch ((ElementType2)type.Type) {
		case ElementType2.Ptr:
		case ElementType2.ByRef:
		case ElementType2.Array:
		case ElementType2.GenericInst:
		case ElementType2.ValueArray:
		case ElementType2.SZArray:
		case ElementType2.Pinned:
			return args is not null ? GetScopeType(args[0]) : null;

		case ElementType2.ValueType:
		case ElementType2.Class:
			return args is not null && args[0].Kind == PortableComplexTypeKind.Token ? args[0].Token : default(PortableToken?);

		case ElementType2.CModReqd:
		case ElementType2.CModOpt:
			return args is not null ? GetScopeType(args[1]) : null;

		default:
			return null;
		}
	}

	internal enum ElementType : byte {
		End = 0x00,
		Void = 0x01,
		Boolean = 0x02,
		Char = 0x03,
		I1 = 0x04,
		U1 = 0x05,
		I2 = 0x06,
		U2 = 0x07,
		I4 = 0x08,
		U4 = 0x09,
		I8 = 0x0A,
		U8 = 0x0B,
		R4 = 0x0C,
		R8 = 0x0D,
		String = 0x0E,
		Ptr = 0x0F,
		ByRef = 0x10,
		ValueType = 0x11,
		Class = 0x12,
		Var = 0x13,
		Array = 0x14,
		GenericInst = 0x15,
		TypedByRef = 0x16,
		ValueArray = 0x17,
		I = 0x18,
		U = 0x19,
		R = 0x1A,
		FnPtr = 0x1B,
		Object = 0x1C,
		SZArray = 0x1D,
		MVar = 0x1E,
		CModReqd = 0x1F,
		CModOpt = 0x20,
		Module = 0x3F,
		Sentinel = 0x41,
		Pinned = 0x45
	}

	enum CallingConvention : byte {
		Default = 0x0,
		C = 0x1,
		StdCall = 0x2,
		ThisCall = 0x3,
		FastCall = 0x4,
		VarArg = 0x5,
		Field = 0x6,
		LocalSig = 0x7,
		Property = 0x8,
		Unmanaged = 0x9,
		GenericInstCC = 0xA,
		NativeVarArg = 0xB
	}

	enum SpecialType : byte {
		Int32,
		MethodSpec,
		InlineType,
		InlineField,
		InlineMethod
	}
}

public static class PrimitivesHelper {
	public static long? ToSlot(object? value) {
		switch (value) {
		case null:
		default:
			return null;
		case bool b:
			return b ? 1 : 0;
		case char c:
			return c;
		case sbyte i1:
			return i1;
		case byte u1:
			return u1;
		case short i2:
			return i2;
		case ushort u2:
			return u2;
		case int i4:
			return i4;
		case uint u4:
			return u4;
		case long i8:
			return i8;
		case ulong u8:
			return (long)u8;
		case float r4:
			long t = BitConverter.DoubleToInt64Bits(r4);
			Debug.Assert(!float.IsNaN(r4) || (ulong)t == 0xFFF8000000000000, "Casting a NaN float to double will lost information.");
			return t;
		case double r8:
			return BitConverter.DoubleToInt64Bits(r8);
		}
	}

	public static object? FromSlot(long? value, int elementType) {
		if (value is not long slot)
			return null;
		var elementType2 = (ElementType2)elementType;
		switch (elementType2) {
		case ElementType2.Boolean:
			return slot != 0;
		case ElementType2.Char:
			return (char)slot;
		case ElementType2.I1:
			return (sbyte)slot;
		case ElementType2.U1:
			return (byte)slot;
		case ElementType2.I2:
			return (short)slot;
		case ElementType2.U2:
			return (ushort)slot;
		case ElementType2.I4:
			return (int)slot;
		case ElementType2.U4:
			return (uint)slot;
		case ElementType2.I8:
			return slot;
		case ElementType2.U8:
			return (ulong)slot;
		case ElementType2.R4:
			double t = BitConverter.Int64BitsToDouble(slot);
			Debug.Assert(!double.IsNaN(t) || t == 0xFFF8000000000000, "Casting a NaN double to float will lost information.");
			return (float)t;
		case ElementType2.R8:
			return BitConverter.Int64BitsToDouble(slot);
		default:
			Debug.Assert(Enum.IsDefined(typeof(ElementType2), elementType2), "Invalid element type.");
			return null;
		}
	}
}
