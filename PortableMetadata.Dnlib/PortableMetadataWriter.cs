using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace MetadataSerialization.Dnlib;

/// <summary>
/// The metadata writer that writes the metadata from the <see cref="PortableMetadata"/> to the <see cref="ModuleDef"/>.
/// </summary>
/// <param name="module"></param>
/// <param name="metadata"></param>
public sealed class PortableMetadataWriter(ModuleDef module, PortableMetadata metadata) {
	sealed class EntityWithLevel<T>(T value) {
		public T Value = value;
		public PortableMetadataLevel Level = PortableMetadataLevel.Reference;
	}

	readonly Dictionary<PortableType, EntityWithLevel<ITypeDefOrRef>> types = new(PortableMetadataEqualityComparer.ReferenceComparer);
	readonly Dictionary<PortableField, EntityWithLevel<FieldDef>> fields = new(PortableMetadataEqualityComparer.ReferenceComparer);
	readonly Dictionary<PortableMethod, EntityWithLevel<MethodDef>> methods = new(PortableMetadataEqualityComparer.ReferenceComparer);
	readonly Dictionary<string, AssemblyRef> assemblies = [];

	/// <summary>
	/// Gets the <see cref="ModuleDef"/> associated with the <see cref="PortableMetadataWriter"/>.
	/// </summary>
	public ModuleDef Module => module;

	/// <summary>
	/// Gets the <see cref="PortableMetadata"/> associated with the <see cref="PortableMetadataWriter"/>.
	/// </summary>
	public PortableMetadata Metadata => metadata;

	/// <summary>
	/// Gets or sets the delegate used for resolving assemblies.
	/// </summary>
	public Func<string, AssemblyRef?>? AssemblyResolving { get; set; }

	/// <summary>
	/// Gets or sets the delegate used for resolving types.
	/// </summary>
	public Func<AssemblyRef?, PortableType, ITypeDefOrRef?>? TypeResolving { get; set; }

	/// <summary>
	/// Add a type to <see cref="Module"/>.
	/// </summary>
	/// <param name="type"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public TypeDef AddType(PortableType type, PortableMetadataLevel level) {
		if (type is null)
			throw new ArgumentNullException(nameof(type));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.DefinitionWithChildren)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (!ShouldBeResolvedAsTypeDef(type))
			throw new InvalidOperationException($"Dont't call this method if parameter '{nameof(type)}' is not defined in the {module}.");
		if (type is not PortableTypeDef && level >= PortableMetadataLevel.Definition)
			throw new InvalidOperationException($"The parameter '{nameof(type)}' is not a {nameof(PortableTypeDef)} but the parameter '{nameof(level)}' is {level}");

		// 1. Resolve the type
		TypeDef typeDef;
		if (!types.TryGetValue(type, out var result)) {
			typeDef = (TypeDef)ResolveType(type);
			result = types[type];
			Debug.Assert(result.Value == typeDef && result.Level == PortableMetadataLevel.Reference);
		}
		else
			typeDef = (TypeDef)result.Value;
		if (level <= result.Level)
			return typeDef;

		// 2. Update the type definition
		var type2 = (PortableTypeDef)type;
		var gpContext = new GenericParamContext(typeDef);
		if (result.Level < PortableMetadataLevel.Definition) {
			typeDef.Attributes = (TypeAttributes)type2.Attributes;
			typeDef.BaseType = type2.BaseType is PortableComplexType bt ? AddType(bt, gpContext) : null;
			AddCustomAttributes(type2.CustomAttributes, typeDef.CustomAttributes, gpContext);
			AddGenericParameters(type2.GenericParameters, typeDef.GenericParameters, gpContext);
			AddInterfaces(type2.Interfaces, typeDef.Interfaces, gpContext);
			typeDef.ClassLayout = AddClassLayout(type2.ClassLayout);
			result.Level = PortableMetadataLevel.Definition;
		}
		if (level <= PortableMetadataLevel.Definition)
			return typeDef;

		// 3. Update the children
		if (result.Level < PortableMetadataLevel.DefinitionWithChildren) {
			if (type2.NestedTypes is not null) {
				foreach (var nestedType in type2.NestedTypes)
					AddType(metadata.Types[nestedType], level);
			}
			if (type2.Fields is not null) {
				foreach (var field in type2.Fields)
					AddField(metadata.Fields[field], PortableMetadataLevel.Definition);
			}
			if (type2.Methods is not null) {
				foreach (var method in type2.Methods)
					AddMethod(metadata.Methods[method], PortableMetadataLevel.Definition);
			}
			AddProperties(type2.Properties, typeDef.Properties, gpContext);
			AddEvents(type2.Events, typeDef.Events, gpContext);
			result.Level = PortableMetadataLevel.DefinitionWithChildren;
		}
		if (level <= PortableMetadataLevel.DefinitionWithChildren)
			return typeDef;

		throw new InvalidOperationException();
	}

	/// <summary>
	/// Add a field to <see cref="Module"/>.
	/// </summary>
	/// <param name="field"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public FieldDef AddField(PortableField field, PortableMetadataLevel level) {
		if (field is null)
			throw new ArgumentNullException(nameof(field));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.Definition)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (!ShouldBeResolvedAsTypeDef(metadata.Types[field.Type.Token]))
			throw new InvalidOperationException($"Dont't call this method if parameter '{nameof(field)}' is not defined in the {module}.");
		if (field is not PortableFieldDef && level >= PortableMetadataLevel.Definition)
			throw new InvalidOperationException($"The parameter '{nameof(field)}' is not a {nameof(PortableFieldDef)} but the parameter '{nameof(level)}' is {level}");

		// 1. Resolve the field
		FieldDef fieldDef;
		if (!fields.TryGetValue(field, out var result)) {
			fieldDef = (FieldDef)ResolveField(field, default);
			result = fields[field];
			Debug.Assert(result.Value == fieldDef && result.Level == PortableMetadataLevel.Reference);
		}
		else
			fieldDef = result.Value;
		if (level <= result.Level)
			return fieldDef;

		// 2. Update the field definition
		var field2 = (PortableFieldDef)field;
		var gpContext = new GenericParamContext(fieldDef.DeclaringType);
		if (result.Level < PortableMetadataLevel.Definition) {
			fieldDef.Attributes = (FieldAttributes)field2.Attributes;
			fieldDef.InitialValue = field2.InitialValue;
			AddCustomAttributes(field2.CustomAttributes, fieldDef.CustomAttributes, gpContext);
			fieldDef.Constant = AddConstant(field2.Constant);
			result.Level = PortableMetadataLevel.Definition;
		}
		if (level <= PortableMetadataLevel.Definition)
			return fieldDef;

		throw new InvalidOperationException();
	}

	/// <summary>
	/// Add a method to <see cref="Module"/>.
	/// </summary>
	/// <param name="method"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public MethodDef AddMethod(PortableMethod method, PortableMetadataLevel level) {
		if (method is null)
			throw new ArgumentNullException(nameof(method));
		if (level < PortableMetadataLevel.Reference || level > PortableMetadataLevel.Definition)
			throw new ArgumentOutOfRangeException(nameof(level));
		if (!ShouldBeResolvedAsTypeDef(metadata.Types[method.Type.Token]))
			throw new InvalidOperationException($"Dont't call this method if parameter '{nameof(method)}' is not defined in the {module}.");
		if (method is not PortableMethodDef && level >= PortableMetadataLevel.Definition)
			throw new InvalidOperationException($"The parameter '{nameof(method)}' is not a {nameof(PortableMethodDef)} but the parameter '{nameof(level)}' is {level}");

		// 1. Resolve the method
		MethodDef methodDef;
		if (!methods.TryGetValue(method, out var result)) {
			methodDef = (MethodDef)ResolveMethod(method, default);
			result = methods[method];
			Debug.Assert(result.Value == methodDef && result.Level == PortableMetadataLevel.Reference);
		}
		else
			methodDef = result.Value;
		if (result.Level >= level)
			return methodDef;

		// 2. Update the method definition
		var method2 = (PortableMethodDef)method;
		var gpContext = new GenericParamContext(methodDef.DeclaringType, methodDef);
		if (result.Level < PortableMetadataLevel.Definition) {
			methodDef.Attributes = (MethodAttributes)method2.Attributes;
			methodDef.ImplAttributes = (MethodImplAttributes)method2.ImplAttributes;
			AddParameters(method2.Parameters, methodDef.ParamDefs, gpContext);
			methodDef.Parameters.UpdateParameterTypes();
			if (method2.Body is PortableMethodBody body) {
				methodDef.FreeMethodBody();
				methodDef.Body = AddMethodBody(body, methodDef.Parameters, gpContext);
			}
			AddCustomAttributes(method2.CustomAttributes, methodDef.CustomAttributes, gpContext);
			AddGenericParameters(method2.GenericParameters, methodDef.GenericParameters, gpContext);
			AddMethodOverrides(method2.Overrides, methodDef.Overrides, methodDef, gpContext);
			methodDef.ImplMap = AddImplMap(method2.ImplMap);
			result.Level = PortableMetadataLevel.Definition;
		}
		if (level <= PortableMetadataLevel.Definition)
			return methodDef;

		throw new InvalidOperationException();
	}

	#region Wrappers
	ITypeDefOrRef AddType(PortableComplexType type, GenericParamContext gpContext, bool allowTypeSpec = true) {
		if (type.Kind == PortableComplexTypeKind.Token)
			return ResolveType(metadata.Types[type.Token]);
		else if (allowTypeSpec)
			return new TypeSpecUser(AddTypeSig(type, gpContext));
		else
			throw new InvalidOperationException();
	}

	IField AddField(PortableToken field, GenericParamContext gpContext) {
		return ResolveField(metadata.Fields[field], gpContext);
	}

	IMethodDefOrRef AddMethod(PortableToken method, GenericParamContext gpContext) {
		return ResolveMethod(metadata.Methods[method], gpContext);
	}

	/// <summary>
	/// Add the types to <see cref="Module"/>.
	/// </summary>
	/// <param name="types"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<TypeDef> AddTypes(IEnumerable<PortableType> types, PortableMetadataLevel level) {
		if (types is null)
			throw new ArgumentNullException(nameof(types));

		if (Enumerable2.NewList(types, out List<TypeDef> list))
			return list;
		foreach (var m in types) {
			var type = AddType(m, level);
			list.Add(type);
		}
		return list;
	}

	/// <summary>
	/// Add the fields to <see cref="Module"/>.
	/// </summary>
	/// <param name="fields"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<FieldDef> AddFields(IEnumerable<PortableField> fields, PortableMetadataLevel level) {
		if (fields is null)
			throw new ArgumentNullException(nameof(fields));

		if (Enumerable2.NewList(fields, out List<FieldDef> list))
			return list;
		foreach (var m in fields) {
			var field = AddField(m, level);
			list.Add(field);
		}
		return list;
	}

	/// <summary>
	/// Add the methods to <see cref="Module"/>.
	/// </summary>
	/// <param name="methods"></param>
	/// <param name="level"></param>
	/// <returns></returns>
	public List<MethodDef> AddMethods(IEnumerable<PortableMethod> methods, PortableMetadataLevel level) {
		if (methods is null)
			throw new ArgumentNullException(nameof(methods));

		if (Enumerable2.NewList(methods, out List<MethodDef> list))
			return list;
		foreach (var m in methods) {
			var method = AddMethod(m, level);
			list.Add(method);
		}
		return list;
	}
	#endregion

	#region Resolver
	readonly Dictionary<PortableType, PortableType> originalTypes = CreateOriginalTypes(metadata.Types.Values);

	static Dictionary<PortableType, PortableType> CreateOriginalTypes(ICollection<PortableType> types) {
		var result = new Dictionary<PortableType, PortableType>(types.Count, PortableMetadataEqualityComparer.ReferenceComparer);
		foreach (var type in types)
			result.Add(type, type);
		return result;
	}

	AssemblyRef ResolveAssembly(string name) {
		if (assemblies.TryGetValue(name, out var assembly))
			return assembly;

		// 1. Use the assembly resolving callback
		assembly = AssemblyResolving?.Invoke(name);
		if (assembly is not null) {
			assemblies.Add(name, assembly);
			return assembly;
		}

		// 2. Find the existing assembly reference or create a new one
		bool useFullName = (metadata.Options & PortableMetadataOptions.UseAssemblyFullName) != 0;
		foreach (var asmRef in module.GetAssemblyRefs()) {
			if ((useFullName ? asmRef.FullName : asmRef.Name) == name) {
				assembly = asmRef;
				break;
			}
		}
		assembly ??= useFullName ? new AssemblyNameInfo(name).ToAssemblyRef() : new AssemblyRefUser(name);

		assemblies.Add(name, assembly);
		return assembly;
	}

	ITypeDefOrRef ResolveType(PortableType type) {
		if (types.TryGetValue(type, out var typeWithLevel))
			return typeWithLevel.Value;

		if (type is PortableTypeDef && type.Name == "<Module>" && type.Namespace.Length == 0 && type.Assembly is null
			&& (type.EnclosingNames is null || type.EnclosingNames.Count == 0)) {
			types.Add(type, new EntityWithLevel<ITypeDefOrRef>(module.GlobalType));
			return module.GlobalType;
		}

		// 1. Resolve the assembly
		var assembly = type.Assembly is not null ? ResolveAssembly(type.Assembly) : null;

		// 2. Use the type resolving callback
		bool asTypeDef = ShouldBeResolvedAsTypeDef(type);
		var result = TypeResolving?.Invoke(assembly, type);
		if (result is not null) {
			if (asTypeDef && result is not TypeDef)
				throw new InvalidOperationException($"Type {type} should be resolved as {nameof(TypeDef)}.");
			types.Add(type, new EntityWithLevel<ITypeDefOrRef>(result));
			return result;
		}

		// 3. Resolve the enclosing type
		ITypeDefOrRef? enclosingType;
		if (type.EnclosingNames is not null && type.EnclosingNames.Count != 0) {
			var names = new List<string>(type.EnclosingNames);
			var enclosingTypeName = names[0];
			names.RemoveAt(0);
			var t = new PortableType(enclosingTypeName, type.Namespace, type.Assembly, names);
			t = originalTypes[t];
			Debug.Assert(ShouldBeResolvedAsTypeDef(t) == asTypeDef);
			enclosingType = ResolveType(t);
		}
		else
			enclosingType = null;
		bool isNested = enclosingType is not null;

		// 4. Resolve the type self
		if (asTypeDef) {
			var types = isNested ? ((TypeDef)enclosingType!).NestedTypes : module.Types;
			foreach (var td in types) {
				if (td.Name == type.Name && (isNested || td.Namespace == type.Namespace)) {
					result = td;
					break;
				}
			}
			if (result is null) {
				var t = new TypeDefUser(isNested ? string.Empty : type.Namespace, type.Name);
				types.Add(t);
				module.UpdateRowId(t);
				result = t;
			}
		}
		else {
			var scope = isNested ? (TypeRef)enclosingType! : (IResolutionScope)assembly!;
			foreach (var tr in module.GetTypeRefs()) {
				if (tr.Name == type.Name && (isNested || tr.Namespace == type.Namespace) && tr.ResolutionScope == scope) {
					result = tr;
					break;
				}
			}
			if (result is null) {
				var t = new TypeRefUser(module, isNested ? string.Empty : type.Namespace, type.Name, scope);
				module.UpdateRowId(t);
				result = t;
			}
		}

		types.Add(type, new EntityWithLevel<ITypeDefOrRef>(result));
		return result;
	}

	IField ResolveField(PortableField field, GenericParamContext gpContext) {
		if (fields.TryGetValue(field, out var fieldWithLevel))
			return fieldWithLevel.Value;

		// 1. Resolve the declaring type
		var declaringType = AddType(field.Type, gpContext);

		// 2. Resolve the field
		if (declaringType is TypeDef declaringType2) {
			var fieldSig = (FieldSig)AddCallingConventionSig(field.Signature, new GenericParamContext(declaringType2));
			var fieldDef = declaringType2.FindField(field.Name, fieldSig, SigComparerOptions.PrivateScopeFieldIsComparable);
			if (fieldDef is null) {
				fieldDef = new FieldDefUser(field.Name, fieldSig, FieldAttributes.Assembly);
				declaringType2.Fields.Add(fieldDef);
				module.UpdateRowId(fieldDef);
			}
			fields.Add(field, new EntityWithLevel<FieldDef>(fieldDef));
			return fieldDef;
		}
		else {
			var fieldSig = (FieldSig)AddCallingConventionSig(field.Signature, gpContext);
			var fieldRef = new MemberRefUser(module, field.Name, fieldSig, declaringType);
			module.UpdateRowId(fieldRef);
			return fieldRef;
		}
	}

	IMethodDefOrRef ResolveMethod(PortableMethod method, GenericParamContext gpContext) {
		if (methods.TryGetValue(method, out var methodWithLevel))
			return methodWithLevel.Value;

		// 1. Resolve the declaring type
		var declaringType = AddType(method.Type, gpContext);

		// 2. Resolve the method
		var methodSig = (MethodSig)AddCallingConventionSig(method.Signature, gpContext);
		if (declaringType is TypeDef declaringType2) {
			var methodDef = declaringType2.FindMethod(method.Name, methodSig, SigComparerOptions.PrivateScopeMethodIsComparable);
			if (methodDef is null) {
				methodDef = new MethodDefUser(method.Name, null, MethodAttributes.Assembly);
				methodSig = (MethodSig)AddCallingConventionSig(method.Signature, new GenericParamContext(declaringType2, methodDef));
				methodDef.MethodSig = methodSig;
				declaringType2.Methods.Add(methodDef);
				module.UpdateRowId(methodDef);
			}
			methods.Add(method, new EntityWithLevel<MethodDef>(methodDef));
			return methodDef;
		}
		else {
			var methodRef = new MemberRefUser(module, method.Name, methodSig, declaringType);
			module.UpdateRowId(methodRef);
			return methodRef;
		}
	}

	static bool ShouldBeResolvedAsTypeDef(PortableType type) {
		return type.Assembly is null;
	}
	#endregion

	#region Private
	void AddCustomAttributes(IList<PortableCustomAttribute>? source, CustomAttributeCollection destination, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var ca in source) {
			var ctor = AddMethod(ca.Constructor, gpContext);
			var ca2 = CustomAttributeReader.Read(module, ca.RawData, ctor, gpContext);
			destination.Add(ca2);
		}
	}

	void AddGenericParameters(IList<PortableGenericParameter>? source, IList<GenericParam> destination, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var gp in source) {
			var gp2 = new GenericParamUser((ushort)gp.Number, (GenericParamAttributes)gp.Attributes, gp.Name);
			if (gp.Constraints is not null) {
				foreach (var gpc in gp.Constraints)
					gp2.GenericParamConstraints.Add(new GenericParamConstraintUser(AddType(gpc, gpContext)));
			}
			destination.Add(gp2);
		}
	}

	static ConstantUser? AddConstant(PortableConstant? constant) {
		if (constant is not PortableConstant c)
			return null;
		return new ConstantUser(c.Value, (ElementType)c.Type);
	}

	void AddInterfaces(IList<PortableComplexType>? source, IList<InterfaceImpl> destination, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var i in source)
			destination.Add(new InterfaceImplUser(AddType(i, gpContext)));
	}

	static ClassLayoutUser? AddClassLayout(PortableClassLayout? classLayout) {
		if (classLayout is not PortableClassLayout cl)
			return null;
		return new ClassLayoutUser((ushort)cl.PackingSize, (uint)cl.ClassSize);
	}

	void AddParameters(IList<PortableParameter> source, IList<ParamDef> destination, GenericParamContext gpContext) {
		foreach (var p in source) {
			var p2 = new ParamDefUser(p.Name, (ushort)p.Sequence, (ParamAttributes)p.Attributes);
			AddCustomAttributes(p.CustomAttributes, p2.CustomAttributes, gpContext);
			p2.Constant = AddConstant(p.Constant);
			destination.Add(p2);
		}
	}

	void AddMethodOverrides(IList<PortableToken>? source, IList<MethodOverride> destination, IMethodDefOrRef methodBody, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var o in source)
			destination.Add(new MethodOverride(methodBody, AddMethod(o, gpContext)));
	}

	ImplMapUser? AddImplMap(PortableImplMap? implMap) {
		if (implMap is not PortableImplMap im)
			return null;
		var scope = new ModuleRefUser(module, im.Module);
		module.UpdateRowId(scope);
		return new ImplMapUser(scope, im.Name, (PInvokeAttributes)im.Attributes);
	}

	void AddProperties(IList<PortableProperty>? source, IList<PropertyDef> destination, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var p in source) {
			var p2 = new PropertyDefUser(p.Name, (PropertySig)AddCallingConventionSig(p.Signature, gpContext), (PropertyAttributes)p.Attributes) {
				GetMethod = p.GetMethod is PortableToken get ? (MethodDef)AddMethod(get, gpContext) : null,
				SetMethod = p.SetMethod is PortableToken set ? (MethodDef)AddMethod(set, gpContext) : null
			};
			AddCustomAttributes(p.CustomAttributes, p2.CustomAttributes, gpContext);
			destination.Add(p2);
		}
	}

	void AddEvents(IList<PortableEvent>? source, IList<EventDef> destination, GenericParamContext gpContext) {
		if (source is null)
			return;
		foreach (var e in source) {
			var e2 = new EventDefUser(e.Name, AddType(e.Type, gpContext), (EventAttributes)e.Attributes) {
				AddMethod = e.AddMethod is PortableToken add ? (MethodDef)AddMethod(add, gpContext) : null,
				RemoveMethod = e.RemoveMethod is PortableToken remove ? (MethodDef)AddMethod(remove, gpContext) : null,
				InvokeMethod = e.InvokeMethod is PortableToken invoke ? (MethodDef)AddMethod(invoke, gpContext) : null
			};
			AddCustomAttributes(e.CustomAttributes, e2.CustomAttributes, gpContext);
			destination.Add(e2);
		}
	}
	#endregion

	#region Method Body
	static readonly Dictionary<string, OpCode> opCodes = CreateOpCodeTable();

	static Dictionary<string, OpCode> CreateOpCodeTable() {
		var table = new Dictionary<string, OpCode>(byte.MaxValue);
		foreach (var opCode in OpCodes.OneByteOpCodes) if (opCode != OpCodes.UNKNOWN1)
				table.Add(opCode.Name, opCode);
		foreach (var opCode in OpCodes.TwoByteOpCodes) if (opCode != OpCodes.UNKNOWN2)
				table.Add(opCode.Name, opCode);
		return table;
	}

	CilBody AddMethodBody(PortableMethodBody body, IList<Parameter> parameters, GenericParamContext gpContext) {
		var variables = new List<Local>(body.Variables.Count);
		foreach (var v in body.Variables)
			variables.Add(new Local(AddTypeSig(v, gpContext)));

		var instructions = new List<Instruction>(body.Instructions.Count);
		foreach (var instr in body.Instructions)
			instructions.Add(new Instruction(opCodes[instr.OpCode], instr.Operand));

		foreach (var instr in instructions) {
			var operand = instr.Operand;
			switch (instr.OpCode.OperandType) {
			case OperandType.InlineBrTarget:
			case OperandType.ShortInlineBrTarget:
				instr.Operand = instructions[(int)operand];
				break;

			case OperandType.InlineField:
			case OperandType.InlineMethod:
			case OperandType.InlineSig:
			case OperandType.InlineTok:
			case OperandType.InlineType:
				instr.Operand = AddToken((PortableComplexType)operand, gpContext);
				break;

			case OperandType.InlineI:
			case OperandType.InlineI8:
			case OperandType.InlineNone:
			case OperandType.InlinePhi:
			case OperandType.InlineR:
			case OperandType.InlineString:
				break;

			case OperandType.InlineSwitch: {
				var targets = (int[])operand;
				var newTargets = new List<Instruction>(targets.Length);
				foreach (int target in targets)
					newTargets.Add(instructions[target]);
				instr.Operand = newTargets;
				break;
			}

			case OperandType.InlineVar:
			case OperandType.ShortInlineVar:
				if (instr.OpCode.Code is Code.Ldarg or Code.Ldarg_S or Code.Ldarga or Code.Ldarga_S or Code.Starg or Code.Starg_S)
					instr.Operand = parameters[(int)operand];
				else
					instr.Operand = variables[(int)operand];
				break;

			case OperandType.ShortInlineI:
				instr.Operand = instr.OpCode.Code == Code.Ldc_I4_S ? (sbyte)(int)operand : (byte)(int)operand;
				break;

			case OperandType.ShortInlineR:
				instr.Operand = (float)operand;
				break;

			default:
				throw new NotSupportedException();
			}
		}

		var exceptionHandlers = new List<ExceptionHandler>(body.ExceptionHandlers.Count);
		foreach (var eh in body.ExceptionHandlers) exceptionHandlers.Add(new ExceptionHandler {
			TryStart = instructions[eh.TryStart],
			TryEnd = eh.TryEnd != -1 ? instructions[eh.TryEnd] : null,
			FilterStart = eh.FilterStart != -1 ? instructions[eh.FilterStart] : null,
			HandlerStart = instructions[eh.HandlerStart],
			HandlerEnd = eh.HandlerEnd != -1 ? instructions[eh.HandlerEnd] : null,
			CatchType = eh.CatchType is PortableComplexType catchType ? AddType(catchType, gpContext) : null,
			HandlerType = (ExceptionHandlerType)eh.HandlerType
		});

		return new CilBody(true, instructions, exceptionHandlers, variables);
	}

	object AddToken(PortableComplexType type, GenericParamContext gpContext) {
		if (type.Kind == PortableComplexTypeKind.CallingConventionSig)
			return AddCallingConventionSig(type, gpContext);

		var kind = type.Kind;
		Debug.Assert(type.Arguments is not null);
		type = type.Arguments![0];
		switch (kind) {
		case PortableComplexTypeKind.InlineType:
			Debug.Assert(type.Kind == PortableComplexTypeKind.Token || type.Kind == PortableComplexTypeKind.TypeSig);
			return AddType(type, gpContext);
		case PortableComplexTypeKind.InlineField:
			Debug.Assert(type.Kind == PortableComplexTypeKind.Token);
			return AddField(type.Token, gpContext);
		case PortableComplexTypeKind.InlineMethod:
			Debug.Assert(type.Kind == PortableComplexTypeKind.Token || type.Kind == PortableComplexTypeKind.MethodSpec);
			if (type.Kind == PortableComplexTypeKind.Token)
				return AddMethod(type.Token, gpContext);
			var method = type.Arguments![0].Token;
			var instantiation = type.Arguments[1];
			return new MethodSpecUser(AddMethod(method, gpContext), (GenericInstMethodSig)AddCallingConventionSig(instantiation, gpContext));
		default:
			throw new NotSupportedException();
		}
	}
	#endregion

	#region Signature
	TypeSig AddTypeSig(PortableComplexType type, GenericParamContext gpContext) {
		if (type.Kind != PortableComplexTypeKind.TypeSig)
			throw new ArgumentException("Type is not a type signature.", nameof(type));

		var elementType = (ElementType)type.Type;
		switch (elementType) {
		case ElementType.Void: return module.CorLibTypes.Void;
		case ElementType.Boolean: return module.CorLibTypes.Boolean;
		case ElementType.Char: return module.CorLibTypes.Char;
		case ElementType.I1: return module.CorLibTypes.SByte;
		case ElementType.U1: return module.CorLibTypes.Byte;
		case ElementType.I2: return module.CorLibTypes.Int16;
		case ElementType.U2: return module.CorLibTypes.UInt16;
		case ElementType.I4: return module.CorLibTypes.Int32;
		case ElementType.U4: return module.CorLibTypes.UInt32;
		case ElementType.I8: return module.CorLibTypes.Int64;
		case ElementType.U8: return module.CorLibTypes.UInt64;
		case ElementType.R4: return module.CorLibTypes.Single;
		case ElementType.R8: return module.CorLibTypes.Double;
		case ElementType.String: return module.CorLibTypes.String;
		case ElementType.TypedByRef: return module.CorLibTypes.TypedReference;
		case ElementType.I: return module.CorLibTypes.IntPtr;
		case ElementType.U: return module.CorLibTypes.UIntPtr;
		case ElementType.Object: return module.CorLibTypes.Object;
		case ElementType.Sentinel: return new SentinelSig();
		}

		var arguments = type.Arguments ?? throw new InvalidDataException("Type arguments are missing.");
		switch (elementType) {
		case ElementType.Ptr: // et(next)
			return new PtrSig(AddTypeSig(arguments[0], gpContext));
		case ElementType.ByRef:
			return new ByRefSig(AddTypeSig(arguments[0], gpContext));
		case ElementType.FnPtr:
			return new FnPtrSig(AddCallingConventionSig(arguments[0], gpContext));
		case ElementType.SZArray:
			return new SZArraySig(AddTypeSig(arguments[0], gpContext));
		case ElementType.Pinned:
			return new PinnedSig(AddTypeSig(arguments[0], gpContext));

		case ElementType.ValueType: // et(next)
			return new ValueTypeSig(AddType(arguments[0], gpContext, false));
		case ElementType.Class:
			return new ClassSig(AddType(arguments[0], gpContext, false));

		case ElementType.Var: // et(index)
			return new GenericVar(arguments[0].GetInt32(), gpContext.Type);
		case ElementType.MVar:
			return new GenericMVar(arguments[0].GetInt32(), gpContext.Method);

		case ElementType.Array: {
			// et(next, rank, numSizes, .. sizes, numLowerBounds, .. lowerBound)
			var nextType = AddTypeSig(arguments[0], gpContext);
			uint rank = (uint)arguments[1].GetInt32();
			int numSizes = arguments[2].GetInt32();
			var sizes = new List<uint>(numSizes);
			for (int i = 0; i < numSizes; i++)
				sizes.Add((uint)arguments[3 + i].GetInt32());
			int numLowerBounds = arguments[3 + numSizes].GetInt32();
			var lowerBounds = new List<int>(numLowerBounds);
			for (int i = 0; i < numLowerBounds; i++)
				lowerBounds.Add(arguments[4 + numSizes + i].GetInt32());
			return new ArraySig(nextType, rank, sizes, lowerBounds);
		}

		case ElementType.GenericInst: {
			// et(next, num, .. args)
			var nextType = AddTypeSig(arguments[0], gpContext);
			int num = arguments[1].GetInt32();
			var genericInstSig = new GenericInstSig(nextType as ClassOrValueTypeSig, num);
			for (int i = 0; i < num; i++)
				genericInstSig.GenericArguments.Add(AddTypeSig(arguments[2 + i], gpContext));
			return genericInstSig;
		}

		case ElementType.ValueArray: // et(next, size)
			return new ValueArraySig(AddTypeSig(arguments[0], gpContext), (uint)arguments[1].GetInt32());

		case ElementType.CModReqd: // et(modifier, next)
			return new CModReqdSig(AddType(arguments[0], gpContext), AddTypeSig(arguments[1], gpContext));
		case ElementType.CModOpt:
			return new CModOptSig(AddType(arguments[0], gpContext), AddTypeSig(arguments[1], gpContext));

		case ElementType.Module: // et(index, next)
			return new ModuleSig((uint)arguments[0].GetInt32(), AddTypeSig(arguments[1], gpContext));

		case ElementType.End:
		case ElementType.R:
		case ElementType.Internal:
		default:
			throw new InvalidDataException("Not supported element type.");
		}
	}

	CallingConventionSig AddCallingConventionSig(PortableComplexType type, GenericParamContext gpContext) {
		if (type.Kind != PortableComplexTypeKind.CallingConventionSig)
			throw new ArgumentException("Type is not a calling convention signature.", nameof(type));

		var callingConvention = (CallingConvention)type.Type;
		var arguments = type.Arguments ?? throw new InvalidDataException("Type arguments are missing.");
		var flags = (CallingConvention)arguments[0].GetInt32();
		switch (callingConvention) {
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
			var methodSig = callingConvention == CallingConvention.Property ? new PropertySig() : (MethodBaseSig)new MethodSig();
			methodSig.CallingConvention = callingConvention | flags;
			int index = 1;
			if (methodSig.Generic)
				methodSig.GenParamCount = (uint)arguments[index++].GetInt32();
			int numParams = arguments[index++].GetInt32();
			methodSig.RetType = AddTypeSig(arguments[index++], gpContext);
			var parameters = methodSig.Params;
			for (int i = 0; i < numParams; i++) {
				var paramType = AddTypeSig(arguments[index++], gpContext);
				if (paramType is SentinelSig) {
					methodSig.ParamsAfterSentinel ??= parameters = new List<TypeSig>(numParams - i);
					i--;
				}
				else
					parameters.Add(paramType);
			}
			return methodSig;
		}

		case CallingConvention.Field:
			// cc(fieldType)
			Debug.Assert(flags == 0);
			return new FieldSig(AddTypeSig(arguments[1], gpContext));

		case CallingConvention.LocalSig: {
			// cc(numLocals, .. locals)
			Debug.Assert(flags == 0);
			int numLocals = arguments[1].GetInt32();
			var locals = new List<TypeSig>(numLocals);
			for (int i = 0; i < numLocals; i++)
				locals.Add(AddTypeSig(arguments[2 + i], gpContext));
			return new LocalSig(locals);
		}

		case CallingConvention.GenericInst: {
			// cc(numArgs, .. args)
			Debug.Assert(flags == 0);
			int numArgs = arguments[1].GetInt32();
			var args = new List<TypeSig>(numArgs);
			for (int i = 0; i < numArgs; i++)
				args.Add(AddTypeSig(arguments[2 + i], gpContext));
			return new GenericInstMethodSig(args);
		}

		default:
			throw new InvalidDataException("Not supported calling convention.");
		}
	}
	#endregion
}
