using System;
using System.Collections.Generic;
using System.Diagnostics;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace MetadataSerialization.Dnlib;

/// <summary>
/// The metadata reader that reads the metadata from the <see cref="ModuleDef"/> to the <see cref="PortableMetadata"/>.
/// </summary>
public sealed class PortableMetadataReader : ICustomAttributeWriterHelper {
	readonly ModuleDef module;
	readonly PortableMetadata metadata;
	readonly PortableMetadataUpdater updater;

	/// <summary>
	/// Gets the <see cref="ModuleDef"/> associated with the <see cref="PortableMetadataReader"/>.
	/// </summary>
	public ModuleDef Module => module;

	/// <summary>
	/// Gets the <see cref="PortableMetadata"/> associated with the <see cref="PortableMetadataReader"/>.
	/// </summary>
	public PortableMetadata Metadata => metadata;

	bool UseAssemblyFullName => (metadata.Options & PortableMetadataOptions.UseAssemblyFullName) != 0;

	bool IncludeMethodBodies => (metadata.Options & PortableMetadataOptions.IncludeMethodBodies) != 0;

	bool IncludeCustomAttributes => (metadata.Options & PortableMetadataOptions.IncludeCustomAttributes) != 0;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="module"></param>
	/// <param name="options"></param>
	public PortableMetadataReader(ModuleDef module, PortableMetadataOptions options = PortableMetadata.DefaultOptions) {
		this.module = module;
		metadata = new PortableMetadata(options);
		updater = new PortableMetadataUpdater(metadata);
	}

	/// <summary>
	/// Add a type to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="type"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public PortableToken AddType(TypeDef type, PortableMetadataLevel level) {
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.DefinitionWithChildren)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (type.Module != module)
			throw new ArgumentException("Type is not in the same module.", nameof(type));

		// 1. Write the type reference
		var enclosingNames = type.DeclaringType is not null ? new List<string>() : null;
		var enclosingType = type;
		while (enclosingType.DeclaringType is not null) {
			enclosingType = enclosingType.DeclaringType;
			enclosingNames!.Add(enclosingType.Name);
		}
		var typeRef = new PortableType(type.Name, enclosingType.Namespace, null, enclosingNames);
		var token = updater.Update(typeRef, PortableMetadataLevel.Reference, out var oldLevel);
		if (level <= oldLevel)
			return token;

		// 2. Write the type definition
		if (oldLevel < PortableMetadataLevel.Definition) {
			var baseType = type.BaseType is ITypeDefOrRef bt ? AddType(bt) : default(PortableComplexType?);
			var interfaces = AddInterfaces(type.Interfaces);
			var classLayout = AddClassLayout(type.ClassLayout);
			var genericParameters = AddGenericParameters(type.GenericParameters);
			var customAttributes = AddCustomAttributes(type.CustomAttributes);
			var typeDef = new PortableTypeDef(type.Name, enclosingType.Namespace, null, enclosingNames,
				(int)type.Attributes, baseType, interfaces, classLayout, genericParameters, customAttributes);
			var t = updater.Update(typeDef, PortableMetadataLevel.Definition, out _);
			Debug.Assert(t == token);
		}
		if (level <= PortableMetadataLevel.Definition)
			return token;

		// 3. Write the children
		if (oldLevel < PortableMetadataLevel.DefinitionWithChildren) {
			var typeDef = (PortableTypeDef)metadata.Types[token];
			typeDef.NestedTypes = AddTypes(type.NestedTypes, level);
			typeDef.Fields = AddFields(type.Fields, PortableMetadataLevel.Definition);
			typeDef.Methods = AddMethods(type.Methods, PortableMetadataLevel.Definition);
			typeDef.Properties = AddProperties(type.Properties);
			typeDef.Events = AddEvents(type.Events);
			var t = updater.Update(typeDef, PortableMetadataLevel.DefinitionWithChildren, out _);
			Debug.Assert(t == token);
		}
		if (level <= PortableMetadataLevel.DefinitionWithChildren)
			return token;

		throw new InvalidOperationException();
	}

	/// <summary>
	/// Add a type to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentNullException"></exception>
	public PortableComplexType AddType(TypeSpec type) {
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		return AddTypeSig(type.TypeSig);
	}

	/// <summary>
	/// Add a field to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="field"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public PortableToken AddField(FieldDef field, PortableMetadataLevel level) {
		if (field is null)
			throw new ArgumentNullException(nameof(field));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.Definition)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (field.Module != module)
			throw new ArgumentException("Field is not in the same module.", nameof(field));

		// 1. Write the field reference
		var type = AddType(field.DeclaringType);
		var signature = AddCallingConventionSig(field.Signature);
		var fieldRef = new PortableField(field.Name, type, signature);
		var token = updater.Update(fieldRef, PortableMetadataLevel.Reference, out var oldLevel);
		if (level <= oldLevel)
			return token;

		// 2. Write the field definition
		if (oldLevel < PortableMetadataLevel.Definition) {
			var constant = AddConstant(field.Constant);
			var customAttributes = AddCustomAttributes(field.CustomAttributes);
			var fieldDef = new PortableFieldDef(field.Name, type, signature, (ushort)field.Attributes, field.InitialValue,
				constant, customAttributes);
			var t = updater.Update(fieldDef, PortableMetadataLevel.Definition, out _);
			Debug.Assert(t == token);
		}
		if (level <= PortableMetadataLevel.Definition)
			return token;

		throw new InvalidOperationException();
	}

	/// <summary>
	/// Add a method to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="method"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public PortableToken AddMethod(MethodDef method, PortableMetadataLevel level) {
		if (method is null)
			throw new ArgumentNullException(nameof(method));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.Definition)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (method.Module != module)
			throw new ArgumentException("Method is not in the same module.", nameof(method));

		// 1. Write the method reference
		var type = AddType(method.DeclaringType);
		var signature = AddCallingConventionSig(method.Signature);
		var methodRef = new PortableMethod(method.Name, type, signature);
		var token = updater.Update(methodRef, PortableMetadataLevel.Reference, out var oldLevel);
		if (level <= oldLevel)
			return token;

		// 2. Write the method definition
		if (oldLevel < PortableMetadataLevel.Definition) {
			var parameters = AddParameters(method.ParamDefs);
			var body = IncludeMethodBodies ? AddMethodBody(method.Body) : null;
			var overrides = AddMethodOverrides(method.Overrides);
			var implMap = AddImplMap(method.ImplMap);
			var genericParameters = AddGenericParameters(method.GenericParameters);
			var customAttributes = AddCustomAttributes(method.CustomAttributes);
			var methodDef = new PortableMethodDef(method.Name, type, signature, (ushort)method.Attributes, (ushort)method.ImplAttributes, parameters, body,
				overrides, implMap, genericParameters, customAttributes);
			var t = updater.Update(methodDef, PortableMetadataLevel.Definition, out _);
			Debug.Assert(t == token);
		}
		if (level <= PortableMetadataLevel.Definition)
			return token;

		throw new InvalidOperationException();
	}

	#region Wrappers
	PortableToken AddType(TypeRef type) {
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (type.ResolutionScope is ModuleRef)
			throw new NotSupportedException("Doesn't support the reference to the types of another module.");

		var assembly = UseAssemblyFullName ? type.DefinitionAssembly.FullName : (string)type.DefinitionAssembly.Name;
		var enclosingNames = type.DeclaringType is not null ? new List<string>() : null;
		var enclosingType = type;
		while (enclosingType.DeclaringType is not null) {
			enclosingType = enclosingType.DeclaringType;
			enclosingNames!.Add(enclosingType.Name);
		}
		var typeRef = new PortableType(type.Name, enclosingType.Namespace, assembly, enclosingNames);
		return updater.Update(typeRef, PortableMetadataLevel.Reference, out _);
	}

	PortableComplexType AddType(ITypeDefOrRef type, bool allowTypeSpec = true) {
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		if (type is TypeDef td)
			return PortableComplexType.CreateToken(AddType(td, PortableMetadataLevel.Reference));
		else if (type is TypeRef tr)
			return PortableComplexType.CreateToken(AddType(tr));
		else if (allowTypeSpec && type is TypeSpec ts)
			return AddType(ts);
		else
			throw new NotSupportedException();
	}

	PortableToken AddField(MemberRef field) {
		if (field is null)
			throw new ArgumentNullException(nameof(field));
		if (!field.IsFieldRef)
			throw new ArgumentException("MemberRef is not a field reference", nameof(field));
		if (field.Class is ModuleRef)
			throw new NotSupportedException("Doesn't support the reference to the members of another module's <Module>.");

		var type = AddType(field.DeclaringType);
		var signature = AddCallingConventionSig(field.Signature);
		var fieldRef = new PortableField(field.Name, type, signature);
		return updater.Update(fieldRef, PortableMetadataLevel.Reference, out _);
	}

	PortableToken AddField(IField field) {
		if (field is null)
			throw new ArgumentNullException(nameof(field));

		if (field is FieldDef fd)
			return AddField(fd, PortableMetadataLevel.Reference);
		else if (field is MemberRef fr)
			return AddField(fr);
		else
			throw new NotSupportedException();
	}

	PortableToken AddMethod(MemberRef method) {
		if (method is null)
			throw new ArgumentNullException(nameof(method));
		if (!method.IsMethodRef)
			throw new ArgumentException("MemberRef is not a method reference", nameof(method));
		if (method.Class is MethodDef)
			throw new NotSupportedException("Doesn't support the varargs method reference.");
		if (method.Class is ModuleRef)
			throw new NotSupportedException("Doesn't support the reference to the members of another module's <Module>.");

		var type = AddType(method.DeclaringType);
		var signature = AddCallingConventionSig(method.Signature);
		var methodRef = new PortableMethod(method.Name, type, signature);
		return updater.Update(methodRef, PortableMetadataLevel.Reference, out _);
	}

	PortableToken AddMethod(IMethodDefOrRef method) {
		if (method is null)
			throw new ArgumentNullException(nameof(method));

		if (method is MethodDef md)
			return AddMethod(md, PortableMetadataLevel.Reference);
		else if (method is MemberRef mr)
			return AddMethod(mr);
		else
			throw new NotSupportedException();
	}

	/// <summary>
	/// Add the types to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="types"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<PortableToken> AddTypes(IEnumerable<TypeDef> types, PortableMetadataLevel level) {
		if (types is null)
			throw new ArgumentNullException(nameof(types));

		if (Enumerable2.NewList(types, out List<PortableToken> list))
			return list;
		foreach (var t in types) {
			var type = AddType(t, level);
			list.Add(type);
		}
		return list;
	}

	/// <summary>
	/// Add the fields to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<PortableToken> AddFields(IEnumerable<FieldDef> fields, PortableMetadataLevel level) {
		if (fields is null)
			throw new ArgumentNullException(nameof(fields));

		if (Enumerable2.NewList(fields, out List<PortableToken> list))
			return list;
		foreach (var f in fields) {
			var field = AddField(f, level);
			list.Add(field);
		}
		return list;
	}

	/// <summary>
	/// Add the methods to <see cref="Metadata"/>.
	/// </summary>
	/// <param name="methods"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<PortableToken> AddMethods(IEnumerable<MethodDef> methods, PortableMetadataLevel level) {
		if (methods is null)
			throw new ArgumentNullException(nameof(methods));

		if (Enumerable2.NewList(methods, out List<PortableToken> list))
			return list;
		foreach (var m in methods) {
			var method = AddMethod(m, level);
			list.Add(method);
		}
		return list;
	}
	#endregion

	#region Private
	List<PortableCustomAttribute>? AddCustomAttributes(CustomAttributeCollection customAttributes) {
		if (!IncludeCustomAttributes || customAttributes.Count == 0)
			return null;
		var list = new List<PortableCustomAttribute>(customAttributes.Count);
		foreach (var ca in customAttributes) {
			var ctor = AddMethod((IMethodDefOrRef)ca.Constructor);
			var data = CustomAttributeWriter.Write(this, ca);
			list.Add(new PortableCustomAttribute(ctor, data));
		}
		return list;
	}

	List<PortableGenericParameter>? AddGenericParameters(IList<GenericParam> genericParameters) {
		if (genericParameters.Count == 0)
			return null;
		var list = new List<PortableGenericParameter>(genericParameters.Count);
		foreach (var gp in genericParameters) {
			var gpcs = gp.GenericParamConstraints;
			List<PortableComplexType>? constraints = null;
			if (gpcs.Count != 0) {
				constraints = new List<PortableComplexType>(gpcs.Count);
				foreach (var gpc in gpcs) {
					var type = AddType(gpc.Constraint);
					constraints!.Add(type);
				}
			}
			list.Add(new PortableGenericParameter(gp.Name, (ushort)gp.Flags, gp.Number, constraints));
		}
		return list;
	}

	static PortableConstant? AddConstant(Constant? constant) {
		if (constant is null)
			return null;
		return new PortableConstant((int)constant.Type, constant.Value);
	}

	List<PortableComplexType>? AddInterfaces(IList<InterfaceImpl> interfaces) {
		if (interfaces.Count == 0)
			return null;
		var list = new List<PortableComplexType>(interfaces.Count);
		foreach (var i in interfaces) {
			var type = AddType(i.Interface);
			list.Add(type);
		}
		return list;
	}

	static PortableClassLayout? AddClassLayout(ClassLayout? classLayout) {
		if (classLayout is null)
			return null;
		return new PortableClassLayout(classLayout.PackingSize, (int)classLayout.ClassSize);
	}

	List<PortableParameter> AddParameters(IList<ParamDef> parameters) {
		var list = new List<PortableParameter>(parameters.Count);
		foreach (var p in parameters) {
			var constant = AddConstant(p.Constant);
			var customAttributes = AddCustomAttributes(p.CustomAttributes);
			list.Add(new PortableParameter(p.Name, p.Sequence, (ushort)p.Attributes, constant, customAttributes));
		}
		return list;
	}

	List<PortableToken>? AddMethodOverrides(IList<MethodOverride> overrides) {
		if (overrides.Count == 0)
			return null;
		var list = new List<PortableToken>(overrides.Count);
		foreach (var o in overrides) {
			var method = AddMethod(o.MethodDeclaration);
			list.Add(method);
		}
		return list;
	}

	static PortableImplMap? AddImplMap(ImplMap? implMap) {
		if (implMap is null)
			return null;
		return new PortableImplMap(implMap.Name, implMap.Module.Name, (ushort)implMap.Attributes);
	}

	List<PortableProperty> AddProperties(IList<PropertyDef> properties) {
		var list = new List<PortableProperty>(properties.Count);
		foreach (var p in properties) {
			var signature = AddCallingConventionSig(p.PropertySig);
			var getMethod = p.GetMethod is MethodDef get ? AddMethod(get) : default(PortableToken?);
			var setMethod = p.SetMethod is MethodDef set ? AddMethod(set) : default(PortableToken?);
			var customAttributes = AddCustomAttributes(p.CustomAttributes);
			list.Add(new PortableProperty(p.Name, signature, (int)p.Attributes, getMethod, setMethod, customAttributes));
		}
		return list;
	}

	List<PortableEvent> AddEvents(IList<EventDef> events) {
		var list = new List<PortableEvent>(events.Count);
		foreach (var e in events) {
			var type = AddType(e.EventType);
			var addMethod = e.AddMethod is MethodDef add ? AddMethod(add) : default(PortableToken?);
			var removeMethod = e.RemoveMethod is MethodDef remove ? AddMethod(remove) : default(PortableToken?);
			var invokeMethod = e.InvokeMethod is MethodDef invoke ? AddMethod(invoke) : default(PortableToken?);
			var customAttributes = AddCustomAttributes(e.CustomAttributes);
			list.Add(new PortableEvent(e.Name, type, (int)e.Attributes, addMethod, removeMethod, invokeMethod, customAttributes));
		}
		return list;
	}

	bool IFullNameFactoryHelper.MustUseAssemblyName(IType type) {
		return FullNameFactory.MustUseAssemblyName(module, type, true);
	}

	void IWriterError.Error(string message) {
		// TODO: implement this method.
	}
	#endregion

	#region Method Body
	PortableMethodBody? AddMethodBody(CilBody? body) {
		if (body is null)
			return null;

		var indexes = new Dictionary<Instruction, int>(body.Instructions.Count);
		int index = 0;
		foreach (var instr in body.Instructions)
			indexes.Add(instr, index++);

		var instructions = new List<PortableInstruction>(body.Instructions.Count);
		foreach (var instr in body.Instructions) {
			object? operand;
			switch (instr.OpCode.OperandType) {
			case OperandType.InlineBrTarget:
			case OperandType.ShortInlineBrTarget:
				operand = indexes[(Instruction)instr.Operand];
				break;

			case OperandType.InlineField:
			case OperandType.InlineMethod:
			case OperandType.InlineSig:
			case OperandType.InlineTok:
			case OperandType.InlineType:
				operand = AddToken(instr.Operand);
				break;

			case OperandType.InlineI:
			case OperandType.InlineI8:
			case OperandType.InlineNone:
			case OperandType.InlinePhi:
			case OperandType.InlineR:
			case OperandType.InlineString:
			case OperandType.ShortInlineR:
				operand = instr.Operand;
				break;

			case OperandType.InlineSwitch: {
				var targets = (IList<Instruction>)instr.Operand;
				var newTargets = new int[targets.Count];
				operand = newTargets;
				for (int i = 0; i < targets.Count; i++)
					newTargets[i] = indexes[targets[i]];
				break;
			}

			case OperandType.InlineVar:
			case OperandType.ShortInlineVar:
				operand = ((IVariable)instr.Operand).Index;
				break;

			case OperandType.ShortInlineI:
				operand = instr.Operand is sbyte sb ? sb : (int)(byte)instr.Operand;
				break;

			default:
				throw new NotSupportedException();
			}

			instructions.Add(new PortableInstruction(instr.OpCode.Name, operand));
		}

		var exceptionHandlers = new List<PortableExceptionHandler>(body.ExceptionHandlers.Count);
		foreach (var eh in body.ExceptionHandlers) {
			int tryStart = indexes[eh.TryStart];
			int tryEnd = eh.TryEnd is not null ? indexes[eh.TryEnd] : -1;
			int filterStart = eh.FilterStart is not null ? indexes[eh.FilterStart] : -1;
			int handlerStart = indexes[eh.HandlerStart];
			int handlerEnd = eh.HandlerEnd is not null ? indexes[eh.HandlerEnd] : -1;
			var catchType = eh.CatchType is not null ? AddType(eh.CatchType) : default(PortableComplexType?);
			int handlerType = (int)eh.HandlerType;
			exceptionHandlers.Add(new PortableExceptionHandler(tryStart, tryEnd, filterStart, handlerStart, handlerEnd, catchType, handlerType));
		}

		var variables = new List<PortableComplexType>(body.Variables.Count);
		foreach (var v in body.Variables)
			variables.Add(AddTypeSig(v.Type));

		return new PortableMethodBody(instructions, exceptionHandlers, variables);
	}

	PortableComplexType AddToken(object o) {
		if (o is CallingConventionSig sig) {
			Debug.Assert(sig is MethodSig || sig is LocalSig);
			return AddCallingConventionSig(sig);
		}

		PortableComplexTypeKind kind;
		PortableToken? token = null;
		PortableComplexType? type = null;
		switch (o) {
		case TypeDef td:
			kind = PortableComplexTypeKind.InlineType;
			token = AddType(td, PortableMetadataLevel.Reference);
			break;
		case TypeRef tr:
			kind = PortableComplexTypeKind.InlineType;
			token = AddType(tr);
			break;
		case TypeSpec ts:
			kind = PortableComplexTypeKind.InlineType;
			type = AddType(ts);
			break;

		case FieldDef fd:
			kind = PortableComplexTypeKind.InlineField;
			token = AddField(fd, PortableMetadataLevel.Reference);
			break;
		case MemberRef { IsFieldRef: true } fr:
			kind = PortableComplexTypeKind.InlineField;
			token = AddField(fr);
			break;

		case MethodDef md:
			kind = PortableComplexTypeKind.InlineMethod;
			token = AddMethod(md, PortableMetadataLevel.Reference);
			break;
		case MemberRef { IsMethodRef: true } mr:
			kind = PortableComplexTypeKind.InlineMethod;
			token = AddMethod(mr);
			break;
		case MethodSpec ms:
			kind = PortableComplexTypeKind.InlineMethod;
			token = AddMethod(ms.Method);
			type = AddCallingConventionSig(ms.Instantiation);
			break;

		default:
			throw new NotSupportedException();
		}

		if (token is PortableToken token2) {
			if (type is PortableComplexType type2)
				type = PortableComplexType.CreateMethodSpec(PortableComplexType.CreateToken(token2), type2);
			else
				type = PortableComplexType.CreateToken(token2);
		}
		return PortableComplexType.CreateInlineTokenOperand(kind, type!.Value);
	}
	#endregion

	#region Signature
	PortableComplexType AddTypeSig(TypeSig typeSig) {
		byte elementType = (byte)typeSig.ElementType;
		switch ((ElementType)elementType) {
		case ElementType.End:
		case ElementType.Void:
		case ElementType.Boolean:
		case ElementType.Char:
		case ElementType.I1:
		case ElementType.U1:
		case ElementType.I2:
		case ElementType.U2:
		case ElementType.I4:
		case ElementType.U4:
		case ElementType.I8:
		case ElementType.U8:
		case ElementType.R4:
		case ElementType.R8:
		case ElementType.String:
		case ElementType.TypedByRef:
		case ElementType.I:
		case ElementType.U:
		case ElementType.R:
		case ElementType.Object:
		case ElementType.Sentinel:
			// et
			return PortableComplexType.CreateTypeSig(elementType, null);

		case ElementType.Ptr:
		case ElementType.ByRef:
		case ElementType.FnPtr:
		case ElementType.SZArray:
		case ElementType.Pinned:
			// et(next)
			return PortableComplexType.CreateTypeSig(elementType, [AddTypeSig(typeSig.Next)]);

		case ElementType.ValueType:
		case ElementType.Class:
			// et(next)
			return PortableComplexType.CreateTypeSig(elementType, [AddType(((ClassOrValueTypeSig)typeSig).TypeDefOrRef, false)]);

		case ElementType.Var:
		case ElementType.MVar:
			// et(index)
			return PortableComplexType.CreateTypeSig(elementType, [PortableComplexType.CreateInt32((int)((GenericSig)typeSig).Number)]);

		case ElementType.Array: {
			// et(next, rank, numSizes, .. sizes, numLowerBounds, .. lowerBounds)
			var arraySig = (ArraySig)typeSig;
			var sizes = arraySig.Sizes;
			var lowerBounds = arraySig.LowerBounds;
			var arguments = new List<PortableComplexType>(4 + sizes.Count + lowerBounds.Count) {
				AddTypeSig(arraySig.Next),
				PortableComplexType.CreateInt32((int)arraySig.Rank),
				PortableComplexType.CreateInt32(sizes.Count)
			};
			foreach (uint size in sizes)
				arguments.Add(PortableComplexType.CreateInt32((int)size));
			arguments.Add(PortableComplexType.CreateInt32(lowerBounds.Count));
			foreach (int lowerBound in lowerBounds)
				arguments.Add(PortableComplexType.CreateInt32(lowerBound));
			return PortableComplexType.CreateTypeSig(elementType, arguments);
		}

		case ElementType.GenericInst: {
			// et(next, num, .. args)
			var instSig = (GenericInstSig)typeSig;
			var arguments = new List<PortableComplexType>(2 + instSig.GenericArguments.Count) {
				AddTypeSig(instSig.GenericType),
				PortableComplexType.CreateInt32(instSig.GenericArguments.Count)
			};
			foreach (var arg in instSig.GenericArguments)
				arguments.Add(AddTypeSig(arg));
			return PortableComplexType.CreateTypeSig(elementType, arguments);
		}

		case ElementType.ValueArray:
			// et(next, size)
			return PortableComplexType.CreateTypeSig(elementType, [AddTypeSig(((ValueArraySig)typeSig).Next), PortableComplexType.CreateInt32((int)((ValueArraySig)typeSig).Size)]);

		case ElementType.CModReqd:
		case ElementType.CModOpt:
			// et(modifier, next)
			return PortableComplexType.CreateTypeSig(elementType, [AddType(((ModifierSig)typeSig).Modifier), AddTypeSig(((ModifierSig)typeSig).Next)]);

		case ElementType.Module:
			// et(index, next)
			return PortableComplexType.CreateTypeSig(elementType, [PortableComplexType.CreateInt32((int)((ModuleSig)typeSig).Index), AddTypeSig(((ModuleSig)typeSig).Next)]);

		default:
			throw new InvalidOperationException();
		}
	}

	PortableComplexType AddCallingConventionSig(CallingConventionSig callingConventionSig) {
		byte callingConvention = (byte)(callingConventionSig.GetCallingConvention() & CallingConvention.Mask);
		int flags = (byte)(callingConventionSig.GetCallingConvention() & ~CallingConvention.Mask);
		List<PortableComplexType> arguments = [PortableComplexType.CreateInt32(flags)];
		switch ((CallingConvention)callingConvention) {
		case CallingConvention.Default:
		case CallingConvention.C:
		case CallingConvention.StdCall:
		case CallingConvention.ThisCall:
		case CallingConvention.FastCall:
		case CallingConvention.VarArg:
		case CallingConvention.Property:
		case CallingConvention.Unmanaged:
		case CallingConvention.NativeVarArg: {
			// cc([numGPs], numParams, retType, .. params)
			var methodSig = (MethodBaseSig)callingConventionSig;
			if (methodSig.Generic)
				arguments.Add(PortableComplexType.CreateInt32((int)methodSig.GenParamCount));
			arguments.Add(PortableComplexType.CreateInt32(methodSig.Params.Count));
			arguments.Add(AddTypeSig(methodSig.RetType));
			foreach (var param in methodSig.Params)
				arguments.Add(AddTypeSig(param));
			if (methodSig.ParamsAfterSentinel is not null && methodSig.ParamsAfterSentinel.Count > 0) {
				arguments.Add(PortableComplexType.CreateTypeSig((byte)ElementType.Sentinel, null));
				foreach (var param in methodSig.ParamsAfterSentinel)
					arguments.Add(AddTypeSig(param));
			}
			break;
		}

		case CallingConvention.Field:
			// cc(fieldType)
			arguments.Add(AddTypeSig(((FieldSig)callingConventionSig).Type));
			break;

		case CallingConvention.LocalSig: {
			// cc(numLocals, .. locals)
			var locals = ((LocalSig)callingConventionSig).Locals;
			arguments.Add(PortableComplexType.CreateInt32(locals.Count));
			foreach (var local in locals)
				arguments.Add(AddTypeSig(local));
			break;
		}

		case CallingConvention.GenericInst: {
			// cc(numArgs, .. args)
			var instMethodSig = (GenericInstMethodSig)callingConventionSig;
			arguments.Add(PortableComplexType.CreateInt32(instMethodSig.GenericArguments.Count));
			foreach (var arg in instMethodSig.GenericArguments)
				arguments.Add(AddTypeSig(arg));
			break;
		}

		default:
			throw new InvalidOperationException();
		}
		return PortableComplexType.CreateCallingConventionSig(callingConvention, arguments);
	}
	#endregion
}

static class Enumerable2 {
	public static bool NewList<T1, T2>(IEnumerable<T1> a, out List<T2> b) {
		if (a is IList<T1> l) {
			if (l.Count == 0) {
				b = [];
				return true;
			}
			b = new List<T2>(l.Count);
		}
		else
			b = [];
		return false;
	}
}
