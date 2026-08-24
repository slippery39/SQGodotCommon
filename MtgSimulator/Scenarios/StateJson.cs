using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ImmutableGameObjects;

namespace MtgSimulator.Scenarios;

/// <summary>
/// Round-trips a whole <see cref="GameState"/> through JSON.
///
/// This is a real serializer, not a report. <c>GameStateSnapshot</c> renders a position for a human
/// to read and cannot be loaded back; a scenario you cannot load is a screenshot. The AI inspector
/// needs a state it can hand to <c>SelectAction</c>, which means every zone, component and stack
/// entry has to survive the trip.
///
/// **Why reflection rather than <c>[JsonDerivedType]</c> attributes.** The state holds four
/// polymorphic hierarchies — 49 GameActions, 45 GameComponents, 33 GameEvents, 6 GameObjects at
/// last count — and the set grows every time a card needs a mechanic the engine lacks. Annotating
/// each one means every new mechanic silently breaks scenario loading until someone remembers the
/// attribute, and the failure surfaces as a deserialization error in unrelated tooling weeks later.
/// Discovering subclasses by reflection costs one startup scan and cannot be forgotten.
/// </summary>
public static class StateJson
{
	private static readonly Lazy<JsonSerializerOptions> LazyOptions = new(BuildOptions);

	public static JsonSerializerOptions Options => LazyOptions.Value;

	public static string Serialize(GameState state) => JsonSerializer.Serialize(state, Options);

	public static GameState Deserialize(string json) =>
		JsonSerializer.Deserialize<GameState>(json, Options)
		?? throw new JsonException("Scenario JSON deserialized to null.");

	private static JsonSerializerOptions BuildOptions()
	{
		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			// Records expose init-only properties; without this the whole state writes as {}.
			IncludeFields = false,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver
			{
				Modifiers = { DropDerivedChildren },
			},
		};

		options.Converters.Add(new PolymorphicConverterFactory());
		options.Converters.Add(new ImmutableStackConverter<GameAction>());
		options.Converters.Add(new MetadataValueConverter());

		return options;
	}

	/// <summary>
	/// Applies polymorphic dispatch to EVERY abstract game type, discovered rather than listed.
	///
	/// The first version of this named four bases — GameObject, GameComponent, GameAction,
	/// GameEvent — and the round-trip test immediately found a fifth: <c>TargetSpecification</c>,
	/// nested two levels down inside a card's effects. That is the same failure the reflection
	/// approach exists to avoid, reintroduced one level up. Any abstract type in the game
	/// assemblies gets a discriminator, so a new hierarchy works without anyone remembering.
	/// </summary>
	private sealed class PolymorphicConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert) =>
			// Abstract only. A concrete type never matches, which is what stops the converter
			// recursing when it writes out the concrete form of a value it just claimed.
			typeToConvert.IsAbstract
			&& !typeToConvert.IsGenericType
			&& GameAssemblies().Contains(typeToConvert.Assembly);

		public override JsonConverter CreateConverter(
			Type typeToConvert,
			JsonSerializerOptions options
		) =>
			(JsonConverter)
				Activator.CreateInstance(
					typeof(PolymorphicConverter<>).MakeGenericType(typeToConvert)
				)!;
	}

	/// <summary>
	/// Drops <c>GameObject.Children</c> from serialization.
	///
	/// It is documented as "for initialization or view purposes only" — <c>ParentToChildren</c> is
	/// the source of truth. Writing it would duplicate the entire object tree inside every node of
	/// that same tree, and <c>LoadFrom</c> populates it recursively, so a state where it had been
	/// hydrated would serialize the graph exponentially.
	/// </summary>
	private static void DropDerivedChildren(JsonTypeInfo info)
	{
		if (!typeof(GameObject).IsAssignableFrom(info.Type))
			return;

		for (var i = info.Properties.Count - 1; i >= 0; i--)
			if (info.Properties[i].Name == nameof(GameObject.Children))
				info.Properties.RemoveAt(i);
	}

	// ===== POLYMORPHIC DISPATCH =====

	private static readonly ConcurrentDictionary<
		Type,
		IReadOnlyDictionary<string, Type>
	> Registries = new();

	/// <summary>
	/// Every concrete subclass of <paramref name="baseType"/> in the loaded game assemblies, keyed
	/// by short type name.
	///
	/// Short names keep the file readable and survive namespace moves. A collision would make
	/// loading silently produce the wrong type, so it throws at registry-build time instead — a
	/// startup failure naming both types is far cheaper to diagnose than a scenario that loads
	/// into a subtly different board.
	/// </summary>
	private static IReadOnlyDictionary<string, Type> RegistryFor(Type baseType) =>
		Registries.GetOrAdd(
			baseType,
			bt =>
			{
				var map = new Dictionary<string, Type>(StringComparer.Ordinal);
				foreach (var assembly in GameAssemblies())
				foreach (var type in assembly.GetTypes())
				{
					if (type.IsAbstract || !bt.IsAssignableFrom(type))
						continue;
					if (map.TryGetValue(type.Name, out var existing) && existing != type)
						throw new InvalidOperationException(
							$"Two {bt.Name} subclasses share the short name '{type.Name}': "
								+ $"{existing.FullName} and {type.FullName}. Scenario JSON keys on "
								+ "short names, so one would silently load as the other. Rename one."
						);
					map[type.Name] = type;
				}
				return map;
			}
		);

	private static IEnumerable<Assembly> GameAssemblies()
	{
		yield return typeof(GameState).Assembly; // ImmutableGameObjects
		yield return typeof(MtgCore.MtgGameFactory).Assembly; // MtgCore
	}

	private sealed class PolymorphicConverter<TBase> : JsonConverter<TBase>
		where TBase : class
	{
		public override void Write(
			Utf8JsonWriter writer,
			TBase value,
			JsonSerializerOptions options
		)
		{
			var concrete = value.GetType();
			var node = JsonSerializer.SerializeToNode(value, concrete, options)!.AsObject();
			// Prepended rather than appended so a human scanning the file sees the type first.
			var withType = new JsonObject { ["$type"] = concrete.Name };
			foreach (var property in node.ToList())
			{
				node.Remove(property.Key);
				withType[property.Key] = property.Value;
			}
			withType.WriteTo(writer, options);
		}

		public override TBase? Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		)
		{
			var node = JsonNode.Parse(ref reader)?.AsObject();
			if (node == null)
				return null;

			var typeName =
				node["$type"]?.GetValue<string>()
				?? throw new JsonException(
					$"A {typeof(TBase).Name} entry has no $type discriminator."
				);

			if (!RegistryFor(typeof(TBase)).TryGetValue(typeName, out var concrete))
				throw new JsonException(
					$"Unknown {typeof(TBase).Name} type '{typeName}'. The scenario was probably "
						+ "saved by a build that had a card mechanic this one does not."
				);

			node.Remove("$type");
			return (TBase?)JsonSerializer.Deserialize(node, concrete, options);
		}
	}

	// ===== COLLECTIONS =====

	/// <summary>
	/// <see cref="ImmutableStack{T}"/> has no natural JSON form and enumerates top-first, so
	/// rebuilding by pushing in read order would invert the action stack — the one place in the
	/// engine where order decides what resolves next.
	/// </summary>
	private sealed class ImmutableStackConverter<T> : JsonConverter<ImmutableStack<T>>
	{
		public override void Write(
			Utf8JsonWriter writer,
			ImmutableStack<T> value,
			JsonSerializerOptions options
		) => JsonSerializer.Serialize(writer, value.ToArray(), options);

		public override ImmutableStack<T> Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		)
		{
			var items =
				JsonSerializer.Deserialize<T[]>(ref reader, options)
				?? throw new JsonException("Action stack read as null.");

			// Written top-first, so push in reverse to put the same element back on top.
			var stack = ImmutableStack<T>.Empty;
			for (var i = items.Length - 1; i >= 0; i--)
				stack = stack.Push(items[i]);
			return stack;
		}
	}

	/// <summary>
	/// The <c>ImmutableDictionary&lt;string, object&gt;</c> metadata and pipeline-context stores.
	///
	/// <c>object</c> is the worst case for JSON: a bare <c>5</c> reads back as a boxed
	/// <c>JsonElement</c>, and <c>GetMeta&lt;int&gt;</c> then throws on the cast at some unrelated
	/// point far from the load. Values carry their own type tag so they come back as what went in.
	/// </summary>
	private sealed class MetadataValueConverter : JsonConverter<object>
	{
		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

		public override void Write(
			Utf8JsonWriter writer,
			object value,
			JsonSerializerOptions options
		)
		{
			writer.WriteStartObject();
			switch (value)
			{
				case string s:
					writer.WriteString("s", s);
					break;
				case bool b:
					writer.WriteBoolean("b", b);
					break;
				case int i:
					writer.WriteNumber("i", i);
					break;
				case long l:
					writer.WriteNumber("l", l);
					break;
				case float f:
					writer.WriteNumber("f", f);
					break;
				case double d:
					writer.WriteNumber("d", d);
					break;
				default:
					// Deliberately loud. Silently writing an unknown value as its ToString would
					// load back as a string and fail at an unrelated GetMeta cast much later.
					throw new JsonException(
						$"Metadata value of type {value.GetType().FullName} cannot be serialized. "
							+ "Add a tag for it here, or store it as a component instead."
					);
			}
			writer.WriteEndObject();
		}

		public override object Read(
			ref Utf8JsonReader reader,
			Type typeToConvert,
			JsonSerializerOptions options
		)
		{
			var node =
				JsonNode.Parse(ref reader)?.AsObject()
				?? throw new JsonException("Metadata value read as null.");

			foreach (var (tag, value) in node)
			{
				if (value == null)
					continue;
				return tag switch
				{
					"s" => value.GetValue<string>(),
					"b" => value.GetValue<bool>(),
					"i" => value.GetValue<int>(),
					"l" => value.GetValue<long>(),
					"f" => value.GetValue<float>(),
					"d" => value.GetValue<double>(),
					_ => throw new JsonException($"Unknown metadata type tag '{tag}'."),
				};
			}

			throw new JsonException("Metadata value object was empty.");
		}
	}
}
