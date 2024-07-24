using System;
using System.Collections.Generic;

namespace MetadataSerialization;

/// <summary>
/// The equality comparer for <see cref="PortableType"/>, <see cref="PortableField"/>, <see cref="PortableMethod"/>, and <see cref="PortableComplexType"/>.
/// </summary>
public sealed class PortableMetadataEqualityComparer : IEqualityComparer<PortableType>, IEqualityComparer<PortableField>, IEqualityComparer<PortableMethod>, IEqualityComparer<PortableComplexType> {
	readonly bool onlyReference;
	readonly bool nullEqualsEmpty;

	/// <summary>
	/// Gets the default equality comparer that compares only the reference.
	/// </summary>
	public static readonly PortableMetadataEqualityComparer ReferenceComparer = new(true, true);

	/// <summary>
	/// Gets the default equality comparer that compares all properties.
	/// </summary>
	public static readonly PortableMetadataEqualityComparer FullComparer = new(false, false);

	PortableMetadataEqualityComparer(bool onlyReference, bool nullEqualsEmpty) {
		this.onlyReference = onlyReference;
		this.nullEqualsEmpty = nullEqualsEmpty;
	}

	/// <inheritdoc/>
	public bool Equals(PortableType? x, PortableType? y) {
		if (ReferenceEquals(x, y))
			return true;
		if (x is null || y is null)
			return false;
		if (!onlyReference && x.GetType() != y.GetType())
			return false;
		if (x.Name != y.Name || x.Namespace != y.Namespace || x.Assembly != y.Assembly || !Equals_NameList(x.EnclosingNames, y.EnclosingNames))
			return false;
		if (onlyReference || (x is not PortableTypeDef && y is not PortableTypeDef))
			return true;

		if (x is not PortableTypeDef xd || y is not PortableTypeDef yd)
			return false;
		if (xd.Attributes != yd.Attributes)
			return false;
		if (!Equals_ComplexType(xd.BaseType, yd.BaseType))
			return false;

		if (!Equals_CustomAttributeList(xd.CustomAttributes, yd.CustomAttributes))
			return false;
		if (!Equals_GenericParamList(xd.GenericParameters, yd.GenericParameters))
			return false;
		if (!Equals_ComplexTypeList(xd.Interfaces, yd.Interfaces))
			return false;
		if (!Equals_ClassLayout(xd.ClassLayout, yd.ClassLayout))
			return false;

		if (!Equals_TokenList(xd.NestedTypes, yd.NestedTypes))
			return false;
		if (!Equals_TokenList(xd.Fields, yd.Fields))
			return false;
		if (!Equals_TokenList(xd.Methods, yd.Methods))
			return false;
		if (!Equals_PropertyList(xd.Properties, yd.Properties))
			return false;
		if (!Equals_EventList(xd.Events, yd.Events))
			return false;
		return true;
	}

	/// <inheritdoc/>
	public int GetHashCode(PortableType obj) {
		if (obj is null)
			return 0;
		return (((((obj.Name.GetHashCode() * -1521134295) + obj.Namespace.GetHashCode()) * -1521134295) + (obj.Assembly?.GetHashCode() ?? 0)) * -1521134295) + GetHashCode_NameList(obj.EnclosingNames);
	}

	/// <inheritdoc/>
	public bool Equals(PortableField? x, PortableField? y) {
		if (ReferenceEquals(x, y))
			return true;
		if (x is null || y is null)
			return false;
		if (!onlyReference && x.GetType() != y.GetType())
			return false;
		if (x.Name != y.Name || !Equals(x.Type, y.Type) || !Equals(x.Signature, y.Signature))
			return false;
		if (onlyReference || (x is not PortableFieldDef && y is not PortableFieldDef))
			return true;

		if (x is not PortableFieldDef xd || y is not PortableFieldDef yd)
			return false;
		if (xd.Attributes != yd.Attributes)
			return false;
		if (!Equals_ByteArray(xd.InitialValue, yd.InitialValue))
			return false;

		if (!Equals_CustomAttributeList(xd.CustomAttributes, yd.CustomAttributes))
			return false;
		if (!Equals_Constant(xd.Constant, yd.Constant))
			return false;
		return true;
	}

	/// <inheritdoc/>
	public int GetHashCode(PortableField obj) {
		if (obj is null)
			return 0;
		return (((obj.Name.GetHashCode() * -1521134295) + GetHashCode(obj.Type)) * -1521134295) + GetHashCode(obj.Signature);
	}

	/// <inheritdoc/>
	public bool Equals(PortableMethod? x, PortableMethod? y) {
		if (ReferenceEquals(x, y))
			return true;
		if (x is null || y is null)
			return false;
		if (!onlyReference && x.GetType() != y.GetType())
			return false;
		if (x.Name != y.Name || !Equals(x.Type, y.Type) || !Equals(x.Signature, y.Signature))
			return false;
		if (onlyReference || (x is not PortableMethodDef && y is not PortableMethodDef))
			return true;

		if (x is not PortableMethodDef xd || y is not PortableMethodDef yd)
			return false;
		if (xd.Attributes != yd.Attributes || xd.ImplAttributes != yd.ImplAttributes)
			return false;
		if (!Equals_ParamList(xd.Parameters, yd.Parameters))
			return false;
		if (!Equals_MethodBody(xd.Body, yd.Body))
			return false;

		if (!Equals_CustomAttributeList(xd.CustomAttributes, yd.CustomAttributes))
			return false;
		if (!Equals_GenericParamList(xd.GenericParameters, yd.GenericParameters))
			return false;
		if (!Equals_TokenList(xd.Overrides, yd.Overrides))
			return false;
		if (!Equals_ImplMap(xd.ImplMap, yd.ImplMap))
			return false;
		return true;
	}

	/// <inheritdoc/>
	public int GetHashCode(PortableMethod obj) {
		if (obj is null)
			return 0;
		return (((obj.Name.GetHashCode() * -1521134295) + GetHashCode(obj.Type)) * -1521134295) + GetHashCode(obj.Signature);
	}

	/// <inheritdoc/>
	public bool Equals(PortableComplexType x, PortableComplexType y) {
		return x.Kind == y.Kind && x.Token == y.Token && x.Type == y.Type && Equals_ComplexTypeList(x.Arguments, y.Arguments);
	}

	/// <inheritdoc/>
	public int GetHashCode(PortableComplexType obj) {
		int hash = obj.Kind.GetHashCode();
		hash = (hash * -1521134295) + obj.Token.GetHashCode();
		hash = (hash * -1521134295) + obj.Type.GetHashCode();
		var args = obj.Arguments;
		if (args is null)
			return hash;
		foreach (var arg in args)
			hash = (hash * -1521134295) + GetHashCode(arg);
		return hash;
	}

	bool Equals_ComplexTypeList(IList<PortableComplexType>? x, IList<PortableComplexType>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (!Equals(x[i], y[i]))
				return false;
		}
		return true;
	}

	bool Equals_NameList(IList<string>? x, IList<string>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i] != y[i])
				return false;
		}
		return true;
	}

	static int GetHashCode_NameList(IList<string>? obj) {
		if (obj is null)
			return 0;
		int hash = 0;
		foreach (var name in obj)
			hash = (hash * -1521134295) + name.GetHashCode();
		return hash;
	}

	bool HandleNullableListCount<T>(IList<T>? x, IList<T>? y) {
		if (nullEqualsEmpty) {
			if (x is null)
				return y!.Count == 0;
			if (y is null)
				return x!.Count == 0;
		}
		return false;
	}

	#region Definition Comparer
	bool Equals_ComplexType(PortableComplexType? x, PortableComplexType? y) {
		if (x is PortableComplexType x1)
			return y is PortableComplexType y1 && Equals(x1, y1);
		else
			return y is null;
	}

	static bool Equals_ClassLayout(PortableClassLayout? x, PortableClassLayout? y) {
		if (x is PortableClassLayout x1) {
			if (y is not PortableClassLayout y1)
				return false;
			return x1.PackingSize == y1.PackingSize && x1.ClassSize == y1.ClassSize;
		}
		else
			return y is null;
	}

	static bool Equals_ImplMap(PortableImplMap? x, PortableImplMap? y) {
		if (x is PortableImplMap x1) {
			if (y is not PortableImplMap y1)
				return false;
			return x1.Name == y1.Name && x1.Module == y1.Module && x1.Attributes == y1.Attributes;
		}
		else
			return y is null;
	}

	bool Equals_MethodBody(PortableMethodBody? x, PortableMethodBody? y) {
		if (x is PortableMethodBody x1) {
			if (y is not PortableMethodBody y1)
				return false;
			if (!Equals_InstructionList(x1.Instructions, y1.Instructions))
				return false;
			if (!Equals_ExceptionHandlerList(x1.ExceptionHandlers, y1.ExceptionHandlers))
				return false;
			if (!Equals_ComplexTypeList(x1.Variables, y1.Variables))
				return false;
			return true;
		}
		else
			return y is null;
	}

	static bool Equals_Constant(PortableConstant? x, PortableConstant? y) {
		if (x is PortableConstant x1) {
			if (y is not PortableConstant y1)
				return false;
			if (x1.Type != y1.Type)
				return false;
			if (x1.Value is object xv)
				return y1.Value is object yv && xv.Equals(yv);
			else
				return y1.Value is null;
		}
		else
			return y is null;
	}

	bool Equals_TokenList(IList<PortableToken>? x, IList<PortableToken>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i] != y[i])
				return false;
		}
		return true;
	}

	bool Equals_CustomAttributeList(IList<PortableCustomAttribute>? x, IList<PortableCustomAttribute>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].Constructor != y[i].Constructor)
				return false;
			if (!Equals_ByteArray(x[i].RawData, y[i].RawData))
				return false;
		}
		return true;
	}

	bool Equals_GenericParamList(IList<PortableGenericParameter>? x, IList<PortableGenericParameter>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].Name != y[i].Name || x[i].Attributes != y[i].Attributes || x[i].Number != y[i].Number || !Equals_ComplexTypeList(x[i].Constraints, y[i].Constraints))
				return false;
		}
		return true;
	}

	bool Equals_PropertyList(IList<PortableProperty>? x, IList<PortableProperty>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].Name != y[i].Name || !Equals(x[i].Signature, y[i].Signature) || x[i].Attributes != y[i].Attributes
				|| x[i].GetMethod != y[i].GetMethod || x[i].SetMethod != y[i].SetMethod || !Equals_CustomAttributeList(x[i].CustomAttributes, y[i].CustomAttributes))
				return false;
		}
		return true;
	}

	bool Equals_EventList(IList<PortableEvent>? x, IList<PortableEvent>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].Name != y[i].Name || !Equals(x[i].Type, y[i].Type) || x[i].Attributes != y[i].Attributes || x[i].AddMethod != y[i].AddMethod
				|| x[i].RemoveMethod != y[i].RemoveMethod || x[i].InvokeMethod != y[i].InvokeMethod || !Equals_CustomAttributeList(x[i].CustomAttributes, y[i].CustomAttributes))
				return false;
		}
		return true;
	}

	bool Equals_ParamList(IList<PortableParameter>? x, IList<PortableParameter>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].Name != y[i].Name || x[i].Sequence != y[i].Sequence || x[i].Attributes != y[i].Attributes
				|| !Equals_CustomAttributeList(x[i].CustomAttributes, y[i].CustomAttributes) || !Equals_Constant(x[i].Constant, y[i].Constant))
				return false;
		}
		return true;
	}

	bool Equals_ExceptionHandlerList(IList<PortableExceptionHandler>? x, IList<PortableExceptionHandler>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].TryStart != y[i].TryStart || x[i].TryEnd != y[i].TryEnd || x[i].FilterStart != y[i].FilterStart
				|| x[i].HandlerStart != y[i].HandlerStart || x[i].HandlerType != y[i].HandlerType || !Equals_ComplexType(x[i].CatchType, y[i].CatchType))
				return false;
		}
		return true;
	}

	bool Equals_InstructionList(IList<PortableInstruction>? x, IList<PortableInstruction>? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Count != y.Count)
			return false;
		for (int i = 0; i < x.Count; i++) {
			if (x[i].OpCode != y[i].OpCode)
				return false;
			switch (x[i].Operand) {
			case null:
				if (y[i].Operand is not null)
					return false;
				break;
			case int xi:
				if (y[i].Operand is not int yi || xi != yi)
					return false;
				break;
			case long xl:
				if (y[i].Operand is not long yl || xl != yl)
					return false;
				break;
			case float xf:
				if (y[i].Operand is not float yf || BitConverter.DoubleToInt64Bits(xf) != BitConverter.DoubleToInt64Bits(yf))
					return false;
				break;
			case double xd:
				if (y[i].Operand is not double yd || BitConverter.DoubleToInt64Bits(xd) != BitConverter.DoubleToInt64Bits(yd))
					return false;
				break;
			case string xs:
				if (y[i].Operand is not string ys || xs != ys)
					return false;
				break;
			case int[] xia:
				if (y[i].Operand is not int[] yia || !Equals_Int32Array(xia, yia))
					return false;
				break;
			case PortableComplexType xt:
				if (y[i].Operand is not PortableComplexType yt || !Equals(xt, yt))
					return false;
				break;
			default:
				throw new NotSupportedException();
			}
		}
		return true;
	}

	bool Equals_Int32Array(int[]? x, int[]? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Length != y.Length)
			return false;
		for (int i = 0; i < x.Length; i++) {
			if (x[i] != y[i])
				return false;
		}
		return true;
	}

	bool Equals_ByteArray(byte[]? x, byte[]? y) {
		if (x == y)
			return true;
		if (x is null || y is null)
			return HandleNullableListCount(x, y);
		if (x.Length != y.Length)
			return false;
		for (int i = 0; i < x.Length; i++) {
			if (x[i] != y[i])
				return false;
		}
		return true;
	}
	#endregion
}
