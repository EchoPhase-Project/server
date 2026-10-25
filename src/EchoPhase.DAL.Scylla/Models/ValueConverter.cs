// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Text.Json;

namespace EchoPhase.DAL.Scylla.Models
{
    /// <summary>
    /// How a property is stored when its type is not a CQL one, like EF's
    /// HasConversion: an enum as text, an object as JSON. Applied on reads,
    /// writes, query parameters and IF conditions. Null stays null.
    /// </summary>
    public abstract class ValueConverter
    {
        /// <summary>The property's type.</summary>
        public abstract Type ModelType { get; }

        /// <summary>The type the column holds (a CQL one: string, int, ...).</summary>
        public abstract Type ProviderType { get; }

        public abstract object? ToProvider(object? value);

        public abstract object? FromProvider(object? value);
    }

    public sealed class ValueConverter<TModel, TProvider> : ValueConverter
    {
        private readonly Func<TModel, TProvider> _toProvider;
        private readonly Func<TProvider, TModel> _fromProvider;

        public ValueConverter(Func<TModel, TProvider> toProvider, Func<TProvider, TModel> fromProvider)
        {
            _toProvider = toProvider;
            _fromProvider = fromProvider;
        }

        public override Type ModelType => typeof(TModel);

        public override Type ProviderType => typeof(TProvider);

        public override object? ToProvider(object? value) => value is null ? null : _toProvider((TModel)value);

        public override object? FromProvider(object? value) => value is null ? null : _fromProvider((TProvider)value);
    }

    /// <summary>The usual conversions.</summary>
    public static class ValueConverters
    {
        /// <summary>An enum by name: readable, and new members do not shift stored values.</summary>
        public static ValueConverter<TEnum, string> EnumToString<TEnum>() where TEnum : struct, Enum
            => new(v => v.ToString(), s => Enum.Parse<TEnum>(s, ignoreCase: true));

        /// <summary>An enum by number (int): compact; never renumber its members.</summary>
        public static ValueConverter<TEnum, int> EnumToNumber<TEnum>() where TEnum : struct, Enum
            => new(v => Convert.ToInt32(v, CultureInfo.InvariantCulture), n => (TEnum)Enum.ToObject(typeof(TEnum), n));

        /// <summary>A value as JSON text: for structures CQL has no type for.</summary>
        public static ValueConverter<T, string> Json<T>(JsonSerializerOptions? options = null)
            => new(v => JsonSerializer.Serialize(v, options), s => JsonSerializer.Deserialize<T>(s, options)
                 ?? throw new InvalidOperationException($"The stored JSON is null, not a {typeof(T).Name}."));
    }
}
