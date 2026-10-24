// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EchoPhase.Scripting.Engine
{
    /// <summary>
    /// JSON read as it is needed: an object or array level is turned into
    /// values the first time something reads it, so a program that takes one
    /// field of a large response does not pay for the rest. To the language
    /// they are plain objects and lists, read-only (Set copies them first, as
    /// it does constants); serialized, they write their JSON as it came.
    /// </summary>
    internal abstract class JsonView(JsonElement element)
    {
        public JsonElement Element { get; } = element;

        /// <summary>A JSON value for the language: a view for an object or array, a plain value otherwise.</summary>
        public static object? Of(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => new JsonObjectView(element),
            JsonValueKind.Array => new JsonArrayView(element),
            _ => Values.FromJson(element)
        };

        protected static NotSupportedException ReadOnly() => new("JSON from a response is read-only; Set copies it first.");
    }

    [JsonConverter(typeof(JsonViewConverter))]
    internal sealed class JsonObjectView(JsonElement element)
        : JsonView(element), IDictionary<string, object?>, IReadOnlyDictionary<string, object?>, ICollection
    {
        // This level only; nested objects and arrays stay views until read. A repeated key keeps its last value.
        private Dictionary<string, object?> Members
        {
            get
            {
                if (field is { } ready)
                    return ready;
                var members = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in Element.EnumerateObject())
                    members[property.Name] = Of(property.Value);
                return field = members;
            }
        }

        public object? this[string key]
        {
            get => Members[key];
            set => throw ReadOnly();
        }

        public ICollection<string> Keys => Members.Keys;

        public ICollection<object?> Values => Members.Values;

        IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys => Members.Keys;

        IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values => Members.Values;

        public int Count => Members.Count;

        public bool IsReadOnly => true;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public bool ContainsKey(string key) => Members.ContainsKey(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value) => Members.TryGetValue(key, out value);

        public bool Contains(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)Members).Contains(item);

        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
            => ((ICollection<KeyValuePair<string, object?>>)Members).CopyTo(array, arrayIndex);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)Members).CopyTo(array, index);

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => Members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(string key, object? value) => throw ReadOnly();

        public void Add(KeyValuePair<string, object?> item) => throw ReadOnly();

        public bool Remove(string key) => throw ReadOnly();

        public bool Remove(KeyValuePair<string, object?> item) => throw ReadOnly();

        public void Clear() => throw ReadOnly();
    }

    [JsonConverter(typeof(JsonViewConverter))]
    internal sealed class JsonArrayView(JsonElement element)
        : JsonView(element), IList, IList<object?>, IReadOnlyList<object?>
    {
        private object?[]? _values;
        private bool[]? _ready;

        // The array's elements, found once; each turned into a value when first read.
        private JsonElement[] Elements => field ??= [.. Element.EnumerateArray()];

        public int Count => Element.GetArrayLength();

        public object? this[int index]
        {
            get
            {
                var elements = Elements;
                if ((uint)index >= (uint)elements.Length)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _values ??= new object?[elements.Length];
                _ready ??= new bool[elements.Length];
                if (!_ready[index])
                {
                    _values[index] = Of(elements[index]);
                    _ready[index] = true;
                }
                return _values[index];
            }
            set => throw ReadOnly();
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => true;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public int IndexOf(object? item)
        {
            for (int i = 0; i < Count; i++)
                if (Equals(this[i], item))
                    return i;
            return -1;
        }

        public bool Contains(object? item) => IndexOf(item) >= 0;

        public void CopyTo(object?[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
                array[arrayIndex + i] = this[i];
        }

        void ICollection.CopyTo(Array array, int index)
        {
            for (int i = 0; i < Count; i++)
                array.SetValue(this[i], index + i);
        }

        public IEnumerator<object?> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(object? item) => throw ReadOnly();

        int IList.Add(object? value) => throw ReadOnly();

        public void Insert(int index, object? item) => throw ReadOnly();

        public bool Remove(object? item) => throw ReadOnly();

        void IList.Remove(object? value) => throw ReadOnly();

        public void RemoveAt(int index) => throw ReadOnly();

        public void Clear() => throw ReadOnly();
    }

    /// <summary>Writes a view's JSON as it came: nothing is turned into values to be serialized back.</summary>
    internal sealed class JsonViewConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeof(JsonView).IsAssignableFrom(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            => typeToConvert == typeof(JsonObjectView) ? new Writer<JsonObjectView>()
                : typeToConvert == typeof(JsonArrayView) ? new Writer<JsonArrayView>()
                : throw new NotSupportedException($"{typeToConvert.Name} is not a JSON view.");

        private sealed class Writer<T> : JsonConverter<T> where T : JsonView
        {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => throw new NotSupportedException("JSON views are written, not read.");

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
                => value.Element.WriteTo(writer);
        }
    }
}
