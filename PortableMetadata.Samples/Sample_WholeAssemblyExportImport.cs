using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using dnlib.DotNet;
using dnlib.DotNet.MD;
using MetadataSerialization;
using MetadataSerialization.Dnlib;

static class Sample_WholeAssemblyExportImport {
	public static void DemoMethod() {
		var assembly = Assembly.GetExecutingAssembly();
		Console.WriteLine(assembly.FullName);
		Console.WriteLine(assembly.Location);
	}

	public static void Run() {
		// Test dnlib.dll
		Run(typeof(ModuleDef).Module, "dnlib2");

		// Test current assembly
		var path = Run(typeof(Sample_WholeAssemblyExportImport).Module, "Sample_WholeAssemblyExportImport");
		Assembly.LoadFrom(path).GetType("Sample_WholeAssemblyExportImport")!.GetMethod("DemoMethod")!.Invoke(null, null);
	}

	/*
Output:
431e8643-0ed9-49b9-97af-c020c0dacd62, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
X:\bin\Debug\net8.0\Sample_WholeAssemblyExportImport.dll
	 */

	static string Run(Module reflModule, string name) {
		// 1. Load the module using dnlib
		using var module = ModuleDefMD.Load(reflModule);

		// 2. Import all the typedefs
		var reader = new PortableMetadataReader(module);
		foreach (var type in module.Types)
			reader.AddType(type, PortableMetadataLevel.DefinitionWithChildren);
		var metadata = reader.Metadata;

		// 3. Serialize the metadata
		var options = new JsonSerializerOptions {
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties)
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		var json = JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		File.WriteAllText($"{name}.json", json);

		// 4. Deserialize the metadata
		var metadata2 = JsonSerializer.Deserialize<PortableMetadataFacade>(json, options)!.ToMetadata();
		Compare(metadata, metadata2);

		// 5. Create the new module
		var module2 = new ModuleDefUser(Guid.NewGuid().ToString()) { RuntimeVersion = MDHeaderRuntimeVersion.MS_CLR_40 };
		new AssemblyDefUser(module2.Name).Modules.Add(module2);
		var writer = new PortableMetadataWriter(module2, metadata2);
		foreach (var type in metadata2.Types.Values.OfType<PortableTypeDef>())
			writer.AddType(type, PortableMetadataLevel.DefinitionWithChildren);
		var path = Path.GetFullPath($"{name}.dll");
		module2.Write(path);
		return path;
	}

	static void Compare(PortableMetadata x, PortableMetadata y) {
		Debug.Assert(x.Options == y.Options);
		Debug.Assert(x.Types.Count == y.Types.Count);
		var xt = x.Types.ToArray();
		var yt = y.Types.ToArray();
		for (int i = 0; i < xt.Length; i++) {
			Debug.Assert(xt[i].Key == yt[i].Key);
			Debug.Assert(PortableMetadataEqualityComparer.FullComparer.Equals(xt[i].Value, yt[i].Value));
		}
		Debug.Assert(x.Fields.Count == y.Fields.Count);
		var xf = x.Fields.ToArray();
		var yf = y.Fields.ToArray();
		for (int i = 0; i < xf.Length; i++) {
			Debug.Assert(xf[i].Key == yf[i].Key);
			Debug.Assert(PortableMetadataEqualityComparer.FullComparer.Equals(xf[i].Value, yf[i].Value));
		}
		Debug.Assert(x.Methods.Count == y.Methods.Count);
		var xm = x.Methods.ToArray();
		var ym = y.Methods.ToArray();
		for (int i = 0; i < xm.Length; i++) {
			Debug.Assert(xm[i].Key == ym[i].Key);
			Debug.Assert(PortableMetadataEqualityComparer.FullComparer.Equals(xm[i].Value, ym[i].Value));
		}
	}
}
