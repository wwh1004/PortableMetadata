using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using dnlib.DotNet;
using MetadataSerialization;
using MetadataSerialization.Dnlib;

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
		Console.WriteLine(json);

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

	/*
Output:
{"Options":14,"Types":{"References":{"0":{"Name":"Sample_ExportOneMethod","Namespace":""},"1":{"Name":"Console","Namespace":"System","Assembly":"System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"}},"Definitions":{},"Orders":[]},"Fields":{"References":{},"Definitions":{},"Orders":[]},"Methods":{"References":{"1":{"Name":"WriteLine","Type":{"Kind":0,"Token":{"Index":1},"Type":0},"Signature":{"Kind":2,"Token":{"Index":0},"Type":0,"Arguments":[{"Kind":3,"Token":{"Index":0},"Type":0},{"Kind":3,"Token":{"Index":1},"Type":0},{"Kind":1,"Token":{"Index":0},"Type":1},{"Kind":1,"Token":{"Index":0},"Type":14}]}}},"Definitions":{"0":{"Attributes":150,"ImplAttributes":0,"Parameters":[],"Body":{"Instructions":[{"OpCode":"nop"},{"OpCode":"ldstr","Operand":"Hello World!","StringValue":"Hello World!"},{"OpCode":"call","Operand":{"Kind":7,"Token":{"Index":0},"Type":0,"Arguments":[{"Kind":0,"Token":{"Index":1},"Type":0}]},"TypeValue":{"Kind":7,"Token":{"Index":0},"Type":0,"Arguments":[{"Kind":0,"Token":{"Index":1},"Type":0}]}},{"OpCode":"nop"},{"OpCode":"ret"}],"ExceptionHandlers":[],"Variables":[]},"Name":"DemoMethod","Type":{"Kind":0,"Token":{"Index":0},"Type":0},"Signature":{"Kind":2,"Token":{"Index":0},"Type":0,"Arguments":[{"Kind":3,"Token":{"Index":0},"Type":0},{"Kind":3,"Token":{"Index":0},"Type":0},{"Kind":1,"Token":{"Index":0},"Type":1}]}}},"Orders":[]}}
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
	 */
}
