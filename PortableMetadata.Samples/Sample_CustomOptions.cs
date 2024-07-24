using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using dnlib.DotNet;
using MetadataSerialization;
using MetadataSerialization.Dnlib;

static class Sample_CustomOptions {
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	sealed class MyAttribute : Attribute { }

	[My]
	public static void DemoMethod([CallerMemberName] string arg = "") {
		Console.WriteLine($"Hello World: {arg}");
	}

	public static void Run() {
		// 1. Load the module using dnlib
		using var module = ModuleDefMD.Load(typeof(Sample_CustomOptions).Module);

		// 2. Create the portable metadata with custom options (use named token and exclude custom attributes)
		var reader = new PortableMetadataReader(module, PortableMetadataOptions.UseNamedToken | PortableMetadataOptions.IncludeMethodBodies);
		reader.AddMethod(module.FindNormalThrow("Sample_CustomOptions").FindMethod("DemoMethod"), PortableMetadataLevel.Definition);
		var metadata = reader.Metadata;

		// 3. Export the metadata
		var options = new JsonSerializerOptions {
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver().WithAddedModifier(STJPortableMetadataObjectPropertyRemover.RemoveObjectProperties),
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			WriteIndented = true
		};
		options.Converters.Add(new STJPortableTokenConverter());
		options.Converters.Add(new STJPortableComplexTypeConverter());
		var json = JsonSerializer.Serialize(new PortableMetadataFacade(metadata), options);
		Console.WriteLine(json);
	}

	/*
Output:
{
  "Options": 5,
  "Types": {
    "References": {
      "Sample_CustomOptions": {
        "Name": "Sample_CustomOptions",
        "Namespace": ""
      },
      "String": {
        "Name": "String",
        "Namespace": "System",
        "Assembly": "System.Runtime"
      },
      "Console": {
        "Name": "Console",
        "Namespace": "System",
        "Assembly": "System.Console"
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
      "String::Concat": {
        "Name": "Concat",
        "Type": "'String'",
        "Signature": "Default(Int32(0),Int32(2),String,String,String)"
      },
      "Console::WriteLine": {
        "Name": "WriteLine",
        "Type": "'Console'",
        "Signature": "Default(Int32(0),Int32(1),Void,String)"
      }
    },
    "Definitions": {
      "Sample_CustomOptions::DemoMethod": {
        "Attributes": 150,
        "ImplAttributes": 0,
        "Parameters": [
          {
            "Name": "arg",
            "Sequence": 1,
            "Attributes": 4112,
            "Constant": {
              "Type": 14,
              "StringValue": ""
            }
          }
        ],
        "Body": {
          "Instructions": [
            {
              "OpCode": "nop"
            },
            {
              "OpCode": "ldstr",
              "StringValue": "Hello World: "
            },
            {
              "OpCode": "ldarg.0"
            },
            {
              "OpCode": "call",
              "TypeValue": "InlineMethod('String::Concat')"
            },
            {
              "OpCode": "call",
              "TypeValue": "InlineMethod('Console::WriteLine')"
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
        "Type": "'Sample_CustomOptions'",
        "Signature": "Default(Int32(0),Int32(1),Void,String)"
      }
    },
    "Orders": [
      0,
      1
    ]
  }
}
	 */
}
