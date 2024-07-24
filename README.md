# PortableMetadata

.NET metadata serialization library. Defines a human-readable and serializable metadata data structure. The PortableMetadata class is the center of the entire library.

## Features

1. Support saving the entire assembly.
1. Support saving only a single or specific types/fields/methods.
1. PortableMetadata is serialization friendly. It can be serialized directly to json by Json.NET or System.Text.Json and does not require any custom converters. Using the custom converts can achieve better display results, and the relevant code is in the samples.
1. PortableMetadata only requires a binary size of around 50kb and does not require any external dependencies. Compatible with .NET Framework 2.0 to .NET 8.0+.

## Scenarios

1. Dump part of .NET metadata to view.
1. Transfer processed. NET metadata between processes.
1. Exchange data directly between different. NET metadata libraries.
1. Create a plaintext patch and apply it to an existing assembly.
1. And more...

## Limits

1. Do NOT support the varargs method.
1. Do NOT support the multiple module assemblies.

## Samples

### Basics

Currently, the built-in reader/writer that uses dnlib are provided. Assume the corresponding namespaces are imported.

```cs
using dnlib.DotNet;
using dnlib.DotNet.MD;
using MetadataSerialization;
using MetadataSerialization.Dnlib;
```

Read the PortableMetadata from the ModuleDef in dnlib:

```cs
// 1. Load the module using dnlib
using ModuleDefMD module = ModuleDefMD.Load(typeof(YourType).Module);

// 2. Create the portable metadata
var reader = new PortableMetadataReader(module);
reader.AddType(module.FindNormalThrow("YourType"), PortableMetadataLevel.DefinitionWithChildren);

// 3. Get the PortableMetadata instance
PortableMetadata metadata = reader.Metadata;
```

Write the data from the PortableMetadata to the ModuleDef:

```cs
// 1. Specify the data source and module to write to
ModuleDef module = ...;
PortableMetadata metadata = ...;
PortableType type = metadata.Types.Values.First(t => ...);

// 2. Write the metadata from PortableMetadata to ModuleDef
var writer = new PortableMetadataWriter(module, metadata);
writer.AddType(type, PortableMetadataLevel.Definition.DefinitionWithChildren);
```

### ExportOneMethod

This example demonstrates how to export a method.

Code is available in 'PortableMetadata.Samples\Sample_ExportOneMethod.cs'.

```cs
static class Sample_ExportOneMethod {
	public static void DemoMethod() {
		Console.WriteLine("Hello World!");
	}

	public static void Run() {
		// 1. Load the module using dnlib
		using var module = ModuleDefMD.Load(typeof(Sample_ExportOneMethod).Module);

		// 2. Create the portable metadata
		var reader = new PortableMetadataReader(module);
		reader.AddMethod(module.FindNormalThrow("Sample_ExportOneMethod").FindMethod("DemoMethod"), PortableMetadataLevel.Definition);
		var metadata = reader.Metadata;

		// 3. Export the metadata (without any converters)
		var json = JsonSerializer.Serialize(new PortableMetadataFacade(metadata), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
		// Console.WriteLine(json);

		// 4. Export the metadata (with converters)
		var options = new JsonSerializerOptions {
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties),
			WriteIndented = true
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		json = JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		Console.WriteLine(json);
	}
}
```

Output:

```json
{
  "Options": 14,
  "Types": {
    "References": {
      "0": {
        "Name": "Sample_ExportOneMethod",
        "Namespace": ""
      },
      "1": {
        "Name": "Console",
        "Namespace": "System",
        "Assembly": "System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
      }
    },
    "Definitions": {},
    "Orders": []
  },
  "Fields": {
    "References": {},
    "Definitions": {},
    "Orders": []
  },
  "Methods": {
    "References": {
      "1": {
        "Name": "WriteLine",
        "Type": "1",
        "Signature": "Default(Int32(0),Int32(1),Void,String)"
      }
    },
    "Definitions": {
      "0": {
        "Attributes": 150,
        "ImplAttributes": 0,
        "Parameters": [],
        "Body": {
          "Instructions": [
            {
              "OpCode": "nop"
            },
            {
              "OpCode": "ldstr",
              "StringValue": "Hello World!"
            },
            {
              "OpCode": "call",
              "TypeValue": "InlineMethod(1)"
            },
            {
              "OpCode": "nop"
            },
            {
              "OpCode": "ret"
            }
          ],
          "ExceptionHandlers": [],
          "Variables": []
        },
        "Name": "DemoMethod",
        "Type": "0",
        "Signature": "Default(Int32(0),Int32(0),Void)"
      }
    },
    "Orders": []
  }
}
```

### ImportOneMethod

This example demonstrates how to import a method.

Code is available in 'PortableMetadata.Samples\Sample_ImportOneMethod.cs'.

```cs
static class Sample_ImportOneMethod {
	public static void DemoMethod() {
		//var list = new List<string> {
		//	"Hello",
		//	"World"
		//};
		//foreach (var item in list)
		//	Console.WriteLine(item);
		throw new NotImplementedException();
	}

	public static void Run() {
		// 1. Load the module using dnlib
		using var module = ModuleDefMD.Load(typeof(Sample_ImportOneMethod).Module);

		// 2. Deserialize the metadata
		var options = new JsonSerializerOptions {
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties),
			WriteIndented = true
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		var metadata = JsonSerializer.Deserialize<PortableMetadataFacade>(DemoMethodJson, options)!.ToMetadata();
		var demoMethod = metadata.Methods[0];
		Debug.Assert(demoMethod is PortableMethodDef);

		// 3. Import the method
		var writer = new PortableMetadataWriter(module, metadata);
		writer.AddMethod(demoMethod, PortableMetadataLevel.Definition);
		var path = Path.GetFullPath("Sample_ImportOneMethod.dll");
		module.Assembly.Name = module.Name = Guid.NewGuid().ToString();
		module.Write(path);
		Assembly.LoadFrom(path).GetType("Sample_ImportOneMethod")!.GetMethod("DemoMethod")!.Invoke(null, null);
	}

	const string DemoMethodJson =
		"""
        Omitted here, please go to the corresponding file to view the complete content
		""";
}
```

### Others

See PortableMetadata.Samples\PortableMetadata.Samples.csproj.
