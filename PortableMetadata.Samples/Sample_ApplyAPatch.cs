using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using MetadataSerialization;
using MetadataSerialization.Dnlib;

static class MyClientPatch {
	public record struct MyUserName(string Value);

	public static string MyGetUserName() {
		var data = ReadFile("myuser.json");
		MyUserName obj;
		try {
			obj = JsonSerializer.Deserialize<MyUserName>(data);
		}
		catch {
			return "??";
		}
		return obj.Value;
	}

	static string ReadFile(string path) {
		while (true) {
			try {
				if (!File.Exists(path))
					goto next;
				return File.ReadAllText(path);
			}
			catch {
				goto next;
			}
		next:
			Thread.Sleep(100);
		}
	}
}

static class Sample_ApplyAPatch {
	public static void Run() {
		// 1. Get the patch info of MyClient class
		var patch = GetPatch();
		var patchMetadata = JsonSerializer.Deserialize<PortableMetadataFacade>(patch, CreateJsonSerializerOptions())!.ToMetadata();

		// 2. Apply the patch
		using var module = ModuleDefMD.Load(File.ReadAllBytes("PortableMetadata.Sample.ApplyAPatch.dll"));
		var writer = new PortableMetadataWriter(module, patchMetadata);
		var myCientPatch = writer.AddType(patchMetadata.Types.Values.First(t => t.Name == "MyClientPatch"), PortableMetadataLevel.DefinitionWithChildren);
		var getUserName = module.FindNormalThrow("MyClient").FindMethod("GetUserName");
		getUserName.FreeMethodBody();
		var body = getUserName.Body = new CilBody();
		body.Instructions.Add(OpCodes.Call.ToInstruction(myCientPatch.FindMethod("MyGetUserName")));
		body.Instructions.Add(OpCodes.Ret.ToInstruction());
		module.Name = module.Assembly.Name = Guid.NewGuid().ToString();
		var path = Path.GetFullPath("PortableMetadata.Sample.ApplyAPatch.Patched.dll");
		module.Write(path);

		// 3. Test the patched assembly
		File.Delete("myuser.json");
		var assembly = Assembly.LoadFrom(path);
		dynamic server = Activator.CreateInstance(assembly.GetType("MyServer")!)!;
		int port = server.Start();
		dynamic client = Activator.CreateInstance(assembly.GetType("MyClient")!)!;
		client.Start(port);
		var names = new List<string> { "Alice", "Bob", "Charlie", "David", "Eve" };
		for (int i = 0; i < 5; i++) {
			WriteFile("myuser.json", JsonSerializer.Serialize<MyClientPatch.MyUserName>(new(names[i])));
			Thread.Sleep(1000);
		}
		Thread.Sleep(500);
	}

	/*
Output:
Data from client: Alice
Data from client: Bob
Data from client: Charlie
Data from client: David
Data from client: Eve
	 */

	static string GetPatch() {
		// 1. Load the module using dnlib
		using var module = ModuleDefMD.Load(typeof(MyClientPatch).Module);

		// 2. Create the portable metadata
		var reader = new PortableMetadataReader(module, PortableMetadata.DefaultOptions | PortableMetadataOptions.UseNamedToken);
		reader.AddType(module.FindNormalThrow("MyClientPatch"), PortableMetadataLevel.DefinitionWithChildren);
		var metadata = reader.Metadata;

		// 4. Export the patch
		var json = JsonSerializer.Serialize(new PortableMetadataFacade(metadata), CreateJsonSerializerOptions());
		var path = Path.GetFullPath("patch.json");
		File.WriteAllText(path, json);
		try {
			Process.Start("notepad", path)?.Dispose();
		}
		catch {
		}
		return json;
	}

	static JsonSerializerOptions CreateJsonSerializerOptions() {
		var options = new JsonSerializerOptions {
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties),
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = true
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		return options;
	}

	static void WriteFile(string path, string content) {
		while (true) {
			try {
				File.WriteAllText(path, content);
				return;
			}
			catch {
			}
		}
	}
}
