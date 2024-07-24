using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using dnlib.DotNet;
using MetadataSerialization;
using MetadataSerialization.Dnlib;

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
		{
		  "Options": 14,
		  "Types": {
		    "References": {
		      "0": {
		        "Name": "Sample_ImportOneMethod",
		        "Namespace": ""
		      },
		      "1": {
		        "Name": "List\u00601",
		        "Namespace": "System.Collections.Generic",
		        "Assembly": "System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
		      },
		      "2": {
		        "Name": "Enumerator",
		        "Namespace": "System.Collections.Generic",
		        "Assembly": "System.Collections, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
		        "EnclosingNames": [
		          "List\u00601"
		        ]
		      },
		      "3": {
		        "Name": "Console",
		        "Namespace": "System",
		        "Assembly": "System.Console, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
		      },
		      "4": {
		        "Name": "IDisposable",
		        "Namespace": "System",
		        "Assembly": "System.Runtime, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
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
		        "Name": ".ctor",
		        "Type": "GenericInst(Class(1),Int32(1),String)",
		        "Signature": "Default(Int32(32),Int32(0),Void)"
		      },
		      "2": {
		        "Name": "Add",
		        "Type": "GenericInst(Class(1),Int32(1),String)",
		        "Signature": "Default(Int32(32),Int32(1),Void,Var(Int32(0)))"
		      },
		      "3": {
		        "Name": "GetEnumerator",
		        "Type": "GenericInst(Class(1),Int32(1),String)",
		        "Signature": "Default(Int32(32),Int32(0),GenericInst(ValueType(2),Int32(1),Var(Int32(0))))"
		      },
		      "4": {
		        "Name": "get_Current",
		        "Type": "GenericInst(ValueType(2),Int32(1),String)",
		        "Signature": "Default(Int32(32),Int32(0),Var(Int32(0)))"
		      },
		      "5": {
		        "Name": "WriteLine",
		        "Type": "3",
		        "Signature": "Default(Int32(0),Int32(1),Void,String)"
		      },
		      "6": {
		        "Name": "MoveNext",
		        "Type": "GenericInst(ValueType(2),Int32(1),String)",
		        "Signature": "Default(Int32(32),Int32(0),Boolean)"
		      },
		      "7": {
		        "Name": "Dispose",
		        "Type": "4",
		        "Signature": "Default(Int32(32),Int32(0),Void)"
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
		              "OpCode": "newobj",
		              "TypeValue": "InlineMethod(1)"
		            },
		            {
		              "OpCode": "dup"
		            },
		            {
		              "OpCode": "ldstr",
		              "StringValue": "Hello"
		            },
		            {
		              "OpCode": "callvirt",
		              "TypeValue": "InlineMethod(2)"
		            },
		            {
		              "OpCode": "nop"
		            },
		            {
		              "OpCode": "dup"
		            },
		            {
		              "OpCode": "ldstr",
		              "StringValue": "World"
		            },
		            {
		              "OpCode": "callvirt",
		              "TypeValue": "InlineMethod(2)"
		            },
		            {
		              "OpCode": "nop"
		            },
		            {
		              "OpCode": "stloc.0"
		            },
		            {
		              "OpCode": "nop"
		            },
		            {
		              "OpCode": "ldloc.0"
		            },
		            {
		              "OpCode": "callvirt",
		              "TypeValue": "InlineMethod(3)"
		            },
		            {
		              "OpCode": "stloc.1"
		            },
		            {
		              "OpCode": "br.s",
		              "PrimitiveValue": 22
		            },
		            {
		              "OpCode": "ldloca.s",
		              "PrimitiveValue": 1
		            },
		            {
		              "OpCode": "call",
		              "TypeValue": "InlineMethod(4)"
		            },
		            {
		              "OpCode": "stloc.2"
		            },
		            {
		              "OpCode": "ldloc.2"
		            },
		            {
		              "OpCode": "call",
		              "TypeValue": "InlineMethod(5)"
		            },
		            {
		              "OpCode": "nop"
		            },
		            {
		              "OpCode": "ldloca.s",
		              "PrimitiveValue": 1
		            },
		            {
		              "OpCode": "call",
		              "TypeValue": "InlineMethod(6)"
		            },
		            {
		              "OpCode": "brtrue.s",
		              "PrimitiveValue": 16
		            },
		            {
		              "OpCode": "leave.s",
		              "PrimitiveValue": 31
		            },
		            {
		              "OpCode": "ldloca.s",
		              "PrimitiveValue": 1
		            },
		            {
		              "OpCode": "constrained.",
		              "TypeValue": "InlineType(GenericInst(ValueType(2),Int32(1),String))"
		            },
		            {
		              "OpCode": "callvirt",
		              "TypeValue": "InlineMethod(7)"
		            },
		            {
		              "OpCode": "nop"
		            },
		            {
		              "OpCode": "endfinally"
		            },
		            {
		              "OpCode": "ret"
		            }
		          ],
		          "ExceptionHandlers": [
		            {
		              "TryStart": 15,
		              "TryEnd": 26,
		              "FilterStart": -1,
		              "HandlerStart": 26,
		              "HandlerEnd": 31,
		              "HandlerType": 2
		            }
		          ],
		          "Variables": [
		            "GenericInst(Class(1),Int32(1),String)",
		            "GenericInst(ValueType(2),Int32(1),String)",
		            "String"
		          ]
		        },
		        "Name": "DemoMethod",
		        "Type": "0",
		        "Signature": "Default(Int32(0),Int32(0),Void)"
		      }
		    },
		    "Orders": []
		  }
		}
		""";
}
