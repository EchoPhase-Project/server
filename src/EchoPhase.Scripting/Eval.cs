// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Text.Json;
using EchoPhase.Scripting.Engine;
using EchoPhase.Types.Result;

namespace EchoPhase.Scripting
{
    /// <summary>
    /// The scripting language: expressions (<c>a + 1 &gt; b ? 'x' : 'y'</c>),
    /// templates (<c>Hello @user.name, you have @(len(items)) items</c>) and
    /// paths (<c>a.b[0]</c>). Sources are parsed once and cached, so evaluating
    /// in a loop costs only the evaluation. Stateless and thread-safe. The only
    /// public entry to the engine: Execute/Process/Value report failures as a
    /// result, the Typed*/Evaluate variants throw them.
    /// </summary>
    public static class Eval
    {
        /// <summary>
        /// Variable holding the IANA time zone the date functions use by
        /// default. Not a valid name in the language, so programs can neither
        /// read nor shadow it; the runner supplies it.
        /// </summary>
        public const string TimeZoneVariable = "$timeZone";

        /// <summary>An expression's value as T; failures (syntax, unknown variable, bad types) as the result's error.</summary>
        public static IServiceResult<T> Execute<T>(string expression, IDictionary<string, object> variables)
            => Try(() => TypedExecute<T>(expression, variables));

        public static T TypedExecute<T>(string expression, IDictionary<string, object> variables)
            => Budget.Run(() => Values.ConvertTo<T>(Compiled.Expression(expression).Evaluate(variables)));

        /// <summary>A template rendered to text (then converted to T).</summary>
        public static IServiceResult<T> Process<T>(string template, IDictionary<string, object> variables)
            => Try(() => TypedProcess<T>(template, variables));

        public static T TypedProcess<T>(string template, IDictionary<string, object> variables)
            => Budget.Run(() => Values.ConvertTo<T>(Compiled.Template(template).Render(variables)));

        /// <summary>
        /// A template's value: a template that is one hole (<c>@items</c>,
        /// <c>@(a + 1)</c>) gives that value with its type; anything else, the text.
        /// </summary>
        public static IServiceResult<object?> Value(string template, IDictionary<string, object> variables)
            => Try(() => Evaluate(template, variables));

        /// <summary>Throwing <see cref="Value"/>: for callers that handle failures as exceptions.</summary>
        public static object? Evaluate(string template, IDictionary<string, object> variables)
            => Budget.Run(() => Compiled.Template(template).Evaluate(variables));

        /// <summary>
        /// A value as the language sees it: JSON elements become dictionaries,
        /// lists, numbers (long or double), strings, bools or null; anything
        /// else is returned as is.
        /// </summary>
        public static object? Plain(object? value) => Values.Unwrap(value);

        /// <summary>
        /// Variables with one more name on top (it shadows an outer one of the
        /// same name); the rest are read through.
        /// </summary>
        public static IDictionary<string, object> With(IDictionary<string, object> variables, string name, object? value)
            => new Scope1(name, value, variables);

        /// <summary>
        /// A value to share between evaluations, made safe for it: its lists
        /// and objects are copied into frozen ones, which Set copies before
        /// writing into. For hosts that hand the same value to many evaluations.
        /// </summary>
        public static object? Freeze(object? value) => Frozen.Freeze(value);

        /// <summary>
        /// A JSON value read lazily: each object or array level becomes values
        /// when first read, so taking a few fields of a large document costs
        /// little. Read-only (Set copies before writing); serializes as the
        /// original JSON. The element must outlive the value: pass a Clone()
        /// of a document that will be disposed.
        /// </summary>
        public static object? FromJsonLazily(JsonElement element) => JsonView.Of(element);

        /// <summary>A value as templates print it: text as is, whole numbers without ".0", collections as JSON.</summary>
        public static string Format(object? value) => Values.Format(value);

        /// <summary>A value as JSON (text quoted), as the json() function gives it.</summary>
        public static string ToJson(object? value) => Values.ToJson(value);

        /// <summary>A template rendered with a model's public properties as variables.</summary>
        public static IServiceResult<TR> ProcessTemplate<TM, TR>(string template, TM model)
        {
            ArgumentNullException.ThrowIfNull(model);
            var variables = typeof(TM)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, p => p.GetValue(model) ?? string.Empty);
            return Process<TR>(template, variables);
        }

        /// <summary>The value at a path; throws for an unknown variable or bad syntax.</summary>
        public static object? Resolve(IDictionary<string, object> variables, string path)
            => Budget.Run(() => Compiled.Path(path).Evaluate(variables));

        /// <summary>Assigns at a path, creating the dictionaries and lists on the way.</summary>
        public static IDictionary<string, object> Set(IDictionary<string, object> variables, string path, object? value)
        {
            return Budget.Run(() =>
            {
                Compiled.Assign(Compiled.Target(path), variables, value);
                return variables;
            });
        }

        /// <summary>Null when the expression parses; otherwise why not. For checking programs before they run.</summary>
        public static string? CheckExpression(string expression) => Check(() => Compiled.Expression(expression));

        public static string? CheckTemplate(string template) => Check(() => Compiled.Template(template));

        public static string? CheckPath(string path) => Check(() => Compiled.Target(path));

        private static IServiceResult<T> Try<T>(Func<T> evaluate)
        {
            try
            {
                return ServiceResult<T>.Success(evaluate());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return ServiceResult<T>.Failure(err => err.Set(ex.GetType().Name, ex.Message));
            }
        }

        private static string? Check(Action parse)
        {
            try
            {
                parse();
                return null;
            }
            catch (ScriptSyntaxException ex)
            {
                return ex.Message;
            }
        }
    }
}
