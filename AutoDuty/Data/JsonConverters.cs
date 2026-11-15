using AutoDuty.Helpers;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AutoDuty.Data;

// Path-only settings. Configuration clones and the multibox wire format keep their existing settings.
internal static class PathJson
{
    internal const int CurrentSchema = 2;
    internal static JsonSerializerSettings Read { get; } = Create();
    internal static JsonSerializerSettings Write { get; } = Create();

    private static JsonSerializerSettings Create() => new()
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.None,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Culture = CultureInfo.InvariantCulture,
        MaxDepth = 64,
        ContractResolver = new PathContractResolver(),
        Converters = [new Newtonsoft.Json.Converters.StringEnumConverter(), new PathActionConditionConverter(), new Vector3Converter()],
    };

    internal static PathFile ReadFile(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        int schema = root.GetValue("SchemaVersion", StringComparison.OrdinalIgnoreCase)?.Value<int>() ?? 1;
        if (schema < 1 || schema > CurrentSchema) throw new JsonSerializationException($"Unsupported path schema {schema}");
        if (root.GetValue("Actions", StringComparison.OrdinalIgnoreCase) is not JArray) throw new JsonSerializationException("Path must contain an Actions array");
        var file = root.ToObject<PathFile>(JsonSerializer.Create(Read)) ?? throw new JsonSerializationException("Empty path");
        PathValidator.ValidateStructure(file);
        return file;
    }

    internal static string WriteFile(PathFile file, Formatting formatting = Formatting.Indented)
    {
        file.SchemaVersion = CurrentSchema;
        return JsonConvert.SerializeObject(file, formatting, Write);
    }
}

internal sealed class PathContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization serialization)
    {
        var property = base.CreateProperty(member, serialization);
        if (member.DeclaringType == typeof(PathAction))
            property.ShouldSerialize = obj =>
            {
                var value = property.ValueProvider!.GetValue(obj);
                return value switch
                {
                    null => false,
                    string str => str.Length != 0,
                    ICollection collection => collection.Count != 0,
                    ActionTag tag => tag != ActionTag.None,
                    PathActionFlags flags => flags != PathActionFlags.None,
                    _ => true,
                };
            };
        if (member.DeclaringType == typeof(PathFileMetaData)) property.NullValueHandling = NullValueHandling.Ignore;
        return property;
    }
}

internal sealed class Vector3Converter : JsonConverter<Vector3>
{
    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        float Number(JToken? value)
        {
            if (value?.Type is not (JTokenType.Float or JTokenType.Integer)) throw new JsonSerializationException("Coordinates must be numbers");
            float result = value.Value<float>();
            return float.IsFinite(result) ? result : throw new JsonSerializationException("Non-finite coordinate");
        }
        return token switch
        {
            JArray { Count: 3 } a => new(Number(a[0]), Number(a[1]), Number(a[2])),
            JObject o => new(Number(o["X"]), Number(o["Y"]), Number(o["Z"])),
            _ => throw new JsonSerializationException("Position must be [x,y,z] or {X,Y,Z}"),
        };
    }

    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.X); writer.WriteValue(value.Y); writer.WriteValue(value.Z);
        writer.WriteEndArray();
    }
}

internal sealed class PathActionConditionConverter : JsonConverter<PathActionCondition>
{
    public override PathActionCondition ReadJson(JsonReader reader, Type objectType, PathActionCondition? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        string? key = obj.Value<string>("Condition");
        string? legacy = obj.Value<string>("$type")?.Split(',')[0].Split('+').Last().Split('.').Last();
        Type? type = null;
        foreach (var pair in PathActionCondition.PARSE_KEYS)
            if (key != null ? pair.Key.ToString() == key : pair.Value.Name == legacy) { type = pair.Value; break; }
        if (type == null) throw new JsonSerializationException($"Unknown condition: {key ?? legacy}");
        obj.Remove("Condition"); obj.Remove("$type");
        var condition = (PathActionCondition)Activator.CreateInstance(type)!;
        using var source = obj.CreateReader();
        serializer.Populate(source, condition);
        return condition;
    }

    public override void WriteJson(JsonWriter writer, PathActionCondition? value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }
        writer.WriteStartObject();
        writer.WritePropertyName("Condition"); writer.WriteValue(value.ParseKey.ToString());
        var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(value.GetType());
        foreach (var property in contract.Properties.Where(p => !p.Ignored && p.Readable))
        {
            writer.WritePropertyName(property.PropertyName!);
            serializer.Serialize(writer, property.ValueProvider!.GetValue(value));
        }
        writer.WriteEndObject();
    }
}
