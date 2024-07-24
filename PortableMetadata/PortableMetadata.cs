using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MetadataSerialization;

/// <summary>
/// The options to create the portable metadata.
/// </summary>
[Flags]
public enum PortableMetadataOptions {
	/// <summary>
	/// None
	/// </summary>
	None = 0,

	/// <summary>
	/// Use type/member name as token instead of index. This option will make the output more readable but significantly increase the size.
	/// </summary>
	UseNamedToken = 1,

	/// <summary>
	/// Use assembly full name not the display name.
	/// </summary>
	UseAssemblyFullName = 2,

	/// <summary>
	/// Include method bodies.
	/// </summary>
	IncludeMethodBodies = 4,

	/// <summary>
	/// Include custom attributes.
	/// </summary>
	IncludeCustomAttributes = 8
}

/// <summary>
/// Portable metadata
/// </summary>
/// <param name="options">The options of the portable metadata.</param>
public sealed class PortableMetadata(PortableMetadataOptions options) {
	/// <summary>
	/// The default options of the portable metadata.
	/// </summary>
	public const PortableMetadataOptions DefaultOptions = PortableMetadataOptions.UseAssemblyFullName | PortableMetadataOptions.IncludeMethodBodies | PortableMetadataOptions.IncludeCustomAttributes;

	/// <summary>
	/// Gets the options of the portable metadata.
	/// </summary>
	public PortableMetadataOptions Options { get; } = options;

	/// <summary>
	/// Gets the dictionary of portable types.
	/// </summary>
	public IDictionary<PortableToken, PortableType> Types { get; } = CreateDictionary<PortableType>((options & PortableMetadataOptions.UseNamedToken) != 0);

	/// <summary>
	/// Gets the dictionary of portable fields.
	/// </summary>
	public IDictionary<PortableToken, PortableField> Fields { get; } = CreateDictionary<PortableField>((options & PortableMetadataOptions.UseNamedToken) != 0);

	/// <summary>
	/// Gets the dictionary of portable methods.
	/// </summary>
	public IDictionary<PortableToken, PortableMethod> Methods { get; } = CreateDictionary<PortableMethod>((options & PortableMetadataOptions.UseNamedToken) != 0);

	static IDictionary<PortableToken, TValue> CreateDictionary<TValue>(bool useNamedToken) where TValue : class {
		return useNamedToken ? new Dictionary<PortableToken, TValue>() : new ListToDictionaryAdapter<TValue>();
	}

	[DebuggerDisplay("Count = {Count}")]
	[DebuggerTypeProxy(typeof(ListToDictionaryAdapter_CollectionDebugView<>))]
	sealed class ListToDictionaryAdapter<TValue> : IDictionary<PortableToken, TValue>, IDictionary where TValue : class {
		readonly List<TValue> values = [];
		readonly List<PortableToken> keys = [];

		public TValue this[PortableToken key] {
			get => values[GetIndex(key)];
			set => Add(key, value, true);
		}

		public ICollection<PortableToken> Keys => keys.AsReadOnly();

		public ICollection<TValue> Values => values.AsReadOnly();

		public int Count => values.Count;

		public bool IsReadOnly => false;

		public void Add(PortableToken key, TValue value) {
			Add(key, value, false);
		}

		void Add(PortableToken key, TValue value, bool inserting) {
			if (value is null)
				throw new ArgumentNullException(nameof(value));

			int index = GetIndex(key);
			if (index > values.Count)
				throw new ArgumentException("Key is out of range", nameof(key));

			if (index == values.Count) {
				values.Add(value);
				keys.Add(index);
				return;
			}

			if (!inserting)
				throw new ArgumentException("Key already exists", nameof(key));
			values[index] = value;
		}

		void ICollection<KeyValuePair<PortableToken, TValue>>.Add(KeyValuePair<PortableToken, TValue> item) {
			Add(item.Key, item.Value);
		}

		public void Clear() {
			values.Clear();
			keys.Clear();
		}

		bool ICollection<KeyValuePair<PortableToken, TValue>>.Contains(KeyValuePair<PortableToken, TValue> item) {
			return ContainsKey(item.Key) && EqualityComparer<TValue>.Default.Equals(values[GetIndex(item.Key)], item.Value);
		}

		public bool ContainsKey(PortableToken key) {
			return GetIndex(key) < values.Count;
		}

		void ICollection<KeyValuePair<PortableToken, TValue>>.CopyTo(KeyValuePair<PortableToken, TValue>[] array, int arrayIndex) {
			if (array is null)
				throw new ArgumentNullException(nameof(array));
			if (arrayIndex < 0 || arrayIndex >= array.Length)
				throw new ArgumentOutOfRangeException(nameof(arrayIndex));
			if (array.Length - arrayIndex < values.Count)
				throw new ArgumentException("Insufficient space in destination array", nameof(array));

			for (int i = 0; i < values.Count; i++)
				array[arrayIndex + i] = new KeyValuePair<PortableToken, TValue>(keys[i], values[i]);
		}

		public IEnumerator<KeyValuePair<PortableToken, TValue>> GetEnumerator() {
			int oldCount = values.Count;
			for (int i = 0; i < oldCount; i++) {
				if (values.Count != oldCount)
					throw new InvalidOperationException("Collection was modified");
				yield return new KeyValuePair<PortableToken, TValue>(keys[i], values[i]);
			}
		}

		public bool Remove(PortableToken key) {
			throw new NotSupportedException();
		}

		bool ICollection<KeyValuePair<PortableToken, TValue>>.Remove(KeyValuePair<PortableToken, TValue> item) {
			throw new NotImplementedException();
		}

#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
		public bool TryGetValue(PortableToken key, [MaybeNullWhen(false)] out TValue value) {
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
			int index = GetIndex(key);
			if (index >= values.Count) {
				value = default;
				return false;
			}
			value = values[index];
			return true;
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return GetEnumerator();
		}

		static int GetIndex(PortableToken token) {
			if (token.Name is not null)
				throw new ArgumentException("Token is named.", nameof(token));
			int index = token.Index;
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(token));
			return index;
		}

		#region IDictionary
		ICollection IDictionary.Keys => (ICollection)Keys;
		ICollection IDictionary.Values => (ICollection)Values;
		bool IDictionary.IsFixedSize => false;
		object ICollection.SyncRoot => values;
		bool ICollection.IsSynchronized => false;
		object? IDictionary.this[object key] { get => this[(PortableToken)key]; set => this[(PortableToken)key] = (TValue)value!; }
		bool IDictionary.Contains(object key) { return ContainsKey((PortableToken)key); }
		void IDictionary.Add(object key, object? value) { Add((PortableToken)key, (TValue)value!); }
		IDictionaryEnumerator IDictionary.GetEnumerator() { return new DictionaryEnumerator(GetEnumerator()); }
		void IDictionary.Remove(object key) { Remove((PortableToken)key); }
		void ICollection.CopyTo(Array array, int index) { ((ICollection<KeyValuePair<PortableToken, TValue>>)this).CopyTo((KeyValuePair<PortableToken, TValue>[])array, index); }
		sealed class DictionaryEnumerator(IEnumerator<KeyValuePair<PortableToken, TValue>> enumerator) : IDictionaryEnumerator {
			public object Current => Entry;
			public object Key => enumerator.Current.Key;
			public object Value => enumerator.Current.Value;
			public DictionaryEntry Entry => new(enumerator.Current.Key, enumerator.Current.Value);
			public bool MoveNext() { return enumerator.MoveNext(); }
			public void Reset() { enumerator.Reset(); }
		}
		#endregion
	}

	sealed class ListToDictionaryAdapter_CollectionDebugView<TValue>(ListToDictionaryAdapter<TValue> dictionary) where TValue : class {
		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public TValue[] Items => [.. dictionary.Values];
	}
}

/// <summary>
/// Represents a facade for portable metadata.
/// </summary>
/// <param name="metadata"></param>
public sealed class PortableMetadataFacade(PortableMetadata? metadata) {
	/// <summary>
	/// Represents a facade for accessing portable metadata.
	/// </summary>
	/// <typeparam name="T">The type of the portable entity.</typeparam>
	/// <typeparam name="TDef">The type of the portable entity definition.</typeparam>
	public struct EntityFacade<T, TDef> where T : class where TDef : T {
		/// <summary>
		/// Gets or sets the dictionary of portable entity references.
		/// </summary>
		public IDictionary<string, T> References { get; set; }

		/// <summary>
		/// Gets or sets the dictionary of portable entity definitions.
		/// </summary>
		public IDictionary<string, TDef> Definitions { get; set; }

		/// <summary>
		/// Gets or sets the list of orders for portable entities.
		/// </summary>
		public IList<int> Orders { get; set; }
	}

	/// <summary>
	/// Gets or sets the options of the portable metadata.
	/// </summary>
	public PortableMetadataOptions Options { get; set; } = metadata?.Options ?? 0;

	/// <summary>
	/// Gets or sets the entity facade for portable types.
	/// </summary>
	public EntityFacade<PortableType, PortableTypeDef> Types { get; set; } = Copy<PortableType, PortableTypeDef>(metadata?.Types, metadata?.Options ?? 0);

	/// <summary>
	/// Gets or sets the entity facade for portable fields.
	/// </summary>
	public EntityFacade<PortableField, PortableFieldDef> Fields { get; set; } = Copy<PortableField, PortableFieldDef>(metadata?.Fields, metadata?.Options ?? 0);

	/// <summary>
	/// Gets or sets the entity facade for portable methods.
	/// </summary>
	public EntityFacade<PortableMethod, PortableMethodDef> Methods { get; set; } = Copy<PortableMethod, PortableMethodDef>(metadata?.Methods, metadata?.Options ?? 0);

	/// <summary>
	/// The constructor for deserialization.
	/// </summary>
	public PortableMetadataFacade() : this(null) { }

	static EntityFacade<T, TDef> Copy<T, TDef>(IDictionary<PortableToken, T>? source, PortableMetadataOptions useNamedToken) where T : class where TDef : T {
		var result = new EntityFacade<T, TDef> {
			References = new Dictionary<string, T>(),
			Definitions = new Dictionary<string, TDef>(),
			Orders = []
		};
		if (source is null)
			return result;
		bool useNamedToken2 = (useNamedToken & PortableMetadataOptions.UseNamedToken) != 0;
		bool lastIsDef = false;
		int i = 0;
		foreach (var kvp in source) {
			var key = kvp.Key.ToString();
			var value = kvp.Value;
			bool addOrder;
			if (value is TDef t) {
				result.Definitions.Add(key, t);
				addOrder = !lastIsDef;
				lastIsDef = true;
			}
			else {
				result.References.Add(key, value);
				addOrder = lastIsDef;
				lastIsDef = false;
			}
			if (addOrder && useNamedToken2)
				result.Orders.Add(i);
			i++;
		}
		return result;
	}

	/// <summary>
	/// Converts the <see cref="PortableMetadataFacade"/> to <see cref="PortableMetadata"/>.
	/// </summary>
	/// <returns>The converted <see cref="PortableMetadata"/>.</returns>
	public PortableMetadata ToMetadata() {
		var metadata = new PortableMetadata(Options);
		bool useNamedToken = (Options & PortableMetadataOptions.UseNamedToken) != 0;
		Copy(Types, metadata.Types, useNamedToken);
		Copy(Fields, metadata.Fields, useNamedToken);
		Copy(Methods, metadata.Methods, useNamedToken);
		return metadata;
	}

	static void Copy<T, TDef>(EntityFacade<T, TDef> source, IDictionary<PortableToken, T> destination, bool useNamedToken) where T : class where TDef : T {
		int count = source.References.Count + source.Definitions.Count;
		if (useNamedToken) {
			using var orders = source.Orders.GetEnumerator();
			using var refs = source.References.GetEnumerator();
			using var defs = source.Definitions.GetEnumerator();
			orders.MoveNext();
			bool lastIsDef = false;
			for (int i = 0; i < count; i++) {
				if (i == orders.Current) {
					lastIsDef = !lastIsDef;
					orders.MoveNext();
				}
				string key;
				T value;
				if (lastIsDef) {
					defs.MoveNext();
					key = defs.Current.Key;
					value = defs.Current.Value;
				}
				else {
					refs.MoveNext();
					key = refs.Current.Key;
					value = refs.Current.Value;
				}
				destination.Add(key, value);
			}
		}
		else {
			for (int i = 0; i < count; i++) {
				var key = i.ToString();
				var value = source.References.TryGetValue(key, out var t) ? t : source.Definitions[key];
				destination.Add(i, value);
			}
		}
	}
}

/// <summary>
/// Represents the serialization level of the portable entity.
/// </summary>
public enum PortableMetadataLevel {
	/// <summary>
	/// Represents the reference level of the portable entity.
	/// </summary>
	Reference,

	/// <summary>
	/// Represents the definition level of the portable entity.
	/// </summary>
	Definition,

	/// <summary>
	/// Represents the definition with children level of the portable entity.
	/// </summary>
	DefinitionWithChildren
}

/// <summary>
/// A helper type to create or update the portable metadata.
/// </summary>
/// <param name="metadata">The portable metadata.</param>
public sealed class PortableMetadataUpdater(PortableMetadata metadata) {
	sealed class TokenWithLevel {
		public PortableToken Token;
		public PortableMetadataLevel Level;
	}

	readonly Dictionary<PortableType, TokenWithLevel> typeTokens = CreateTokenMap(metadata.Types);
	readonly Dictionary<PortableField, TokenWithLevel> fieldTokens = CreateTokenMap(metadata.Fields);
	readonly Dictionary<PortableMethod, TokenWithLevel> methodTokens = CreateTokenMap(metadata.Methods);

	bool UseNamedToken => (metadata.Options & PortableMetadataOptions.UseNamedToken) != 0;

	static Dictionary<T, TokenWithLevel> CreateTokenMap<T>(IDictionary<PortableToken, T> source) where T : class {
		var map = new Dictionary<T, TokenWithLevel>((IEqualityComparer<T>)(object)PortableMetadataEqualityComparer.ReferenceComparer);
		foreach (var kvp in source) {
			var level = kvp.Value.GetType() == typeof(T) ? PortableMetadataLevel.Reference : PortableMetadataLevel.Definition;
			map.Add(kvp.Value, new TokenWithLevel {
				Token = kvp.Key,
				Level = level
			});
		}
		return map;
	}

	/// <summary>
	/// Updates the portable type in the portable metadata.
	/// </summary>
	/// <param name="type">The portable type to update.</param>
	/// <param name="level">The serialization level of the portable type.</param>
	/// <param name="currentLevel">The current serialization level of the portable type.</param>
	/// <returns>The updated portable type token.</returns>
	public PortableToken Update(PortableType type, PortableMetadataLevel level, out PortableMetadataLevel currentLevel) {
		if (!typeTokens.TryGetValue(type, out var tl)) {
			tl = new TokenWithLevel {
				Token = UseNamedToken ? GetUniqueName(type.Name, s => metadata.Types.ContainsKey(s)) : metadata.Types.Count,
				Level = currentLevel = level
			};
			typeTokens.Add(type, tl);
			metadata.Types.Add(tl.Token, type);
		}
		else if (level > tl.Level) {
			tl.Level = currentLevel = level;
			metadata.Types[tl.Token] = type;
		}
		else
			currentLevel = tl.Level;

		return tl.Token;
	}

	/// <summary>
	/// Updates the portable field in the portable metadata.
	/// </summary>
	/// <param name="field">The portable field to update.</param>
	/// <param name="level">The serialization level of the portable field.</param>
	/// <param name="currentLevel">The current serialization level of the portable field.</param>
	/// <returns>The updated portable field token.</returns>
	public PortableToken Update(PortableField field, PortableMetadataLevel level, out PortableMetadataLevel currentLevel) {
		if (!fieldTokens.TryGetValue(field, out var tl)) {
			tl = new TokenWithLevel {
				Token = UseNamedToken ? GetUniqueName(field.Type, field.Name, s => metadata.Fields.ContainsKey(s)) : metadata.Fields.Count,
				Level = currentLevel = level
			};
			fieldTokens.Add(field, tl);
			metadata.Fields.Add(tl.Token, field);
		}
		else if (level > tl.Level) {
			tl.Level = currentLevel = level;
			metadata.Fields[tl.Token] = field;
		}
		else
			currentLevel = tl.Level;

		return tl.Token;
	}

	/// <summary>
	/// Updates the portable method in the portable metadata.
	/// </summary>
	/// <param name="method">The portable method to update.</param>
	/// <param name="level">The serialization level of the portable method.</param>
	/// <param name="currentLevel">The current serialization level of the portable method.</param>
	/// <returns>The updated portable method token.</returns>
	public PortableToken Update(PortableMethod method, PortableMetadataLevel level, out PortableMetadataLevel currentLevel) {
		if (!methodTokens.TryGetValue(method, out var tl)) {
			tl = new TokenWithLevel {
				Token = UseNamedToken ? GetUniqueName(method.Type, method.Name, s => metadata.Methods.ContainsKey(s)) : metadata.Methods.Count,
				Level = currentLevel = level
			};
			methodTokens.Add(method, tl);
			metadata.Methods.Add(tl.Token, method);
		}
		else if (level > tl.Level) {
			tl.Level = currentLevel = level;
			metadata.Methods[tl.Token] = method;
		}
		else
			currentLevel = tl.Level;

		return tl.Token;
	}

	static string GetUniqueName(PortableComplexType type, string baseName, Func<string, bool> hasName) {
		var typeName = type.Kind == PortableComplexTypeKind.Token ? type.Token.Name : PortableComplexTypeFormatter.GetScopeType(type)!.Value + "<?>";
		return GetUniqueName(typeName + "::" + baseName, hasName);
	}

	static string GetUniqueName(string baseName, Func<string, bool> hasName) {
		var sb = new StringBuilder(baseName);
		for (int i = 0; i < sb.Length; i++) {
			bool flag = false;
			foreach (char c in PortableToken.InvalidNameChars) {
				if (sb[i] == c) {
					flag = true;
					break;
				}
			}
			if (flag)
				sb[i] = '_';
		}
		baseName = sb.ToString();

		if (!hasName(baseName))
			return baseName;

		for (int n = 2; n < int.MaxValue; n++) {
			var name = $"{baseName}_{n}";
			if (!hasName(name))
				return name;
		}

		throw new InvalidOperationException();
	}
}
