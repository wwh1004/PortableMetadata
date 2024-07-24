using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using dnlib.DotNet;
using MetadataSerialization;
using MetadataSerialization.Dnlib;
using Newtonsoft.Json;

[MemoryDiagnoser]
[GcServer(true), GcForce]
public partial class Program {
	[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
	[JsonSerializable(typeof(PortableMetadataFacade))]
	[JsonSerializable(typeof(bool)), JsonSerializable(typeof(char)), JsonSerializable(typeof(sbyte)), JsonSerializable(typeof(byte)), JsonSerializable(typeof(short)),
		JsonSerializable(typeof(ushort)), JsonSerializable(typeof(uint)), JsonSerializable(typeof(ulong)), JsonSerializable(typeof(float)), JsonSerializable(typeof(double))]
	partial class JsonSourceGenerationContext : JsonSerializerContext { }

	readonly ModuleDef module;
	readonly PortableMetadataOptions options;
	readonly PortableMetadata metadata;

	public Program() {
		module = ModuleDefMD.Load(File.ReadAllBytes("dnlib.dll"));
		options = /*PortableMetadataOptions.UseNamedToken | */PortableMetadataOptions.UseAssemblyFullName | PortableMetadataOptions.IncludeMethodBodies | PortableMetadataOptions.IncludeCustomAttributes;
		var reader = new PortableMetadataReader(module, options);
		metadata = reader.Metadata;
		foreach (var type in module.Types)
			reader.AddType(type, PortableMetadataLevel.DefinitionWithChildren);
	}

	static void Main() {
		BenchmarkRunner.Run<Program>();
	}

	[Benchmark]
	public void PortableMetadataReader() {
		var reader = new PortableMetadataReader(module, options);
		foreach (var type in module.Types)
			reader.AddType(type, PortableMetadataLevel.DefinitionWithChildren);
	}

	[Benchmark]
	public void PortableMetadataWriter() {
		var module = new ModuleDefUser();
		var writer = new PortableMetadataWriter(module, metadata);
		foreach (var type in metadata.Types.Values) {
			if (type is PortableTypeDef && type.Assembly is null)
				writer.AddType(type, PortableMetadataLevel.DefinitionWithChildren);
		}
	}

	[Benchmark]
	public void NewtonsoftJson() {
		var settings = new JsonSerializerSettings {
			NullValueHandling = NullValueHandling.Ignore
		};
		var json = JsonConvert.SerializeObject(new PortableMetadataFacade(metadata), settings);
		LogJson(json);
		var obj = JsonConvert.DeserializeObject<PortableMetadataFacade>(json, settings)!.ToMetadata();
		Compare(obj, metadata);
	}

	[Benchmark]
	public void NewtonsoftJsonWithConverts() {
		var settings = new JsonSerializerSettings {
			Converters = [new NJPortableTokenConverter(), new NJPortableComplexTypeConverter()],
			NullValueHandling = NullValueHandling.Ignore,
			ContractResolver = new NJPortableMetadataObjectPropertyRemover()
		};
		var json = JsonConvert.SerializeObject(new PortableMetadataFacade(metadata), settings);
		LogJson(json);
		var obj = JsonConvert.DeserializeObject<PortableMetadataFacade>(json, settings)!.ToMetadata();
		Compare(obj, metadata);
	}

	[Benchmark]
	public void SystemTextJson() {
		var options = new System.Text.Json.JsonSerializerOptions {
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals // Without converters, serializing NaN will throw an exception.
		};
		var json = System.Text.Json.JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		LogJson(json);
		var obj = System.Text.Json.JsonSerializer.Deserialize<PortableMetadataFacade>(json, options)!.ToMetadata();
		Compare(obj, metadata);
	}

	[Benchmark]
	public void SystemTextJsonWithConverts() {
		var options = new System.Text.Json.JsonSerializerOptions {
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties)
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		var json = System.Text.Json.JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		LogJson(json);
		var obj = System.Text.Json.JsonSerializer.Deserialize<PortableMetadataFacade>(json, options)!.ToMetadata();
		Compare(obj, metadata);
	}

	[Benchmark]
	public void SystemTextJsonSourceGenerator() {
		var options = new System.Text.Json.JsonSerializerOptions {
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, // Without converters, serializing NaN will throw an exception.
			TypeInfoResolver = new JsonSourceGenerationContext()
		};
		var json = System.Text.Json.JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		LogJson(json);
		var obj = System.Text.Json.JsonSerializer.Deserialize<PortableMetadataFacade>(json, options)!.ToMetadata();
		Compare(obj, metadata);
	}

	[Benchmark]
	public void SystemTextJsonSourceGeneratorWithConverts() {
		var options = new System.Text.Json.JsonSerializerOptions {
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new JsonSourceGenerationContext().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties)
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		var json = System.Text.Json.JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		LogJson(json);
		var obj = System.Text.Json.JsonSerializer.Deserialize<PortableMetadataFacade>(json, options)!.ToMetadata();
		Compare(obj, metadata);
	}

	static void LogJson(string json, [CallerMemberName] string name = "") {
#if DEBUG
		Console.WriteLine(name + ": " + json.Length);
		File.WriteAllText(name + ".json", json);
#endif
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
