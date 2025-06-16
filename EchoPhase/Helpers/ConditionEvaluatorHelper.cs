using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace EchoPhase.Helpers
{
    /// <summary>
    /// Provides utility methods for evaluating conditional expressions using dynamic variables.
    /// </summary>
    public static class ConditionEvaluatorHelper
    {
        /// <summary>
        /// Regular expression to parse simple conditional expressions of the form:
        /// <c>&lt;left&gt; &lt;operator&gt; &lt;right&gt;</c>, where operator is one of
        /// <c>==</c>, <c>!=</c>, <c>&gt;=</c>, <c>&lt;=</c>, <c>&gt;</c>, or <c>&lt;</c>.
        /// </summary>
        private static readonly Regex ConditionRegex = new Regex(@"(\S+)\s*(==|!=|>=|<=|>|<)\s*(\S+)", RegexOptions.Compiled);

        /// <summary>
        /// Evaluates a simple conditional expression using the provided variables.
        /// </summary>
        /// <param name="condition">
        /// A string representing the condition to evaluate. It must follow the format:
        /// <c>&lt;left&gt; &lt;operator&gt; &lt;right&gt;</c>, e.g. <c>age &gt; 18</c> or <c>"status" == "active"</c>.
        /// </param>
        /// <param name="variables">
        /// A dictionary containing variable names and their corresponding values, used for resolving tokens in the condition.
        /// </param>
        /// <returns>
        /// <c>true</c> if the condition evaluates to true; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the condition is empty or does not match the expected format.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if a referenced variable is not found in the dictionary.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the operator is not supported or operands cannot be compared.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// Thrown if a null value is encountered during token resolution.
        /// </exception>
        public static bool Evaluate(string condition, IDictionary<string, object> variables)
        {
            if (string.IsNullOrWhiteSpace(condition))
                throw new ArgumentException("Condition cannot be empty.", nameof(condition));

            var match = ConditionRegex.Match(condition);
            if (!match.Success)
                throw new ArgumentException($"Invalid condition format: {condition}");

            string leftToken = match.Groups[1].Value;
            string op = match.Groups[2].Value;
            string rightToken = match.Groups[3].Value;

            object leftValue = ResolveToken(leftToken, variables);
            object rightValue = ResolveToken(rightToken, variables);

            return CompareOperands(leftValue, rightValue, op);
        }

        /// <summary>
        /// Resolves a string token to its corresponding value using a dictionary of variables.
        /// Supports constants, quoted strings, nested property/field access, and JSON structures.
        /// </summary>
        /// <param name="token">
        /// The token to resolve. It can be:
        /// <list type="bullet">
        /// <item><description>A quoted string (e.g., <c>"hello"</c> or <c>'world'</c>)</description></item>
        /// <item><description>A constant (e.g., <c>123</c>, <c>true</c>, <c>3.14</c>)</description></item>
        /// <item><description>A variable or nested path (e.g., <c>user.name</c>)</description></item>
        /// </list>
        /// </param>
        /// <param name="variables">A dictionary containing root-level variable names and their values.</param>
        /// <returns>
        /// The resolved value. This may be a primitive type, object property, field value,
        /// or a converted value from <see cref="JsonElement"/> or <see cref="JToken"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if the token is empty or invalid.</exception>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if the root variable or a nested property/field cannot be found.
        /// </exception>
        /// <exception cref="NullReferenceException">
        /// Thrown if a null value is encountered during nested property/field access.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to access a property on a non-object <see cref="JsonElement"/> or <see cref="JToken"/>.
        /// </exception>
        private static object ResolveToken(string token, IDictionary<string, object> variables)
        {
            if ((token.StartsWith("\"") && token.EndsWith("\"")) ||
                (token.StartsWith("'") && token.EndsWith("'")))
                return token.Trim('"', '\'');

            if (TryParseConstant(token, out var constant))
                return constant;

            var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new ArgumentException("Invalid token: empty string.");

            var rootName = parts[0];

            if (!variables.TryGetValue(rootName, out var current))
                throw new KeyNotFoundException($"Cannot resolve variable '{rootName}'.");

            for (int i = 1; i < parts.Length; i++)
            {
                var propName = parts[i];

                if (current == null)
                    throw new NullReferenceException($"Null encountered while accessing '{propName}'.");

                if (current is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException($"Cannot get property '{propName}' from non-object JSON element.");

                    if (!jsonElement.TryGetProperty(propName, out var prop))
                        throw new KeyNotFoundException($"Property '{propName}' not found in JSON element.");

                    current = prop;
                    continue;
                }

                if (current is JToken jtoken)
                {
                    if (jtoken.Type != JTokenType.Object)
                        throw new InvalidOperationException($"Cannot get property '{propName}' from non-object JSON token.");

                    JToken? propToken = jtoken[propName];

                    if (propToken is null)
                        throw new KeyNotFoundException($"Property '{propName}' not found in JSON token.");

                    current = propToken;
                    continue;
                }

                var type = current.GetType();
                var property = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property != null)
                {
                    current = property.GetValue(current);
                    continue;
                }

                var field = type.GetField(propName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (field != null)
                {
                    current = field.GetValue(current);
                    continue;
                }

                throw new KeyNotFoundException($"Property or field '{propName}' not found on type '{type.Name}'.");
            }

            if (current is JsonElement je)
                return JsonElementToClrValue(je);
            if (current is JToken jt)
                return JTokenToClrValue(jt);
            if (current is null)
                throw new NullReferenceException($"Result is null.");

            return current;
        }

        /// <summary>
        /// Converts a <see cref="JsonElement"/> to a corresponding CLR value.
        /// </summary>
        /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
        /// <returns>
        /// A CLR value based on the element's <see cref="JsonValueKind"/>:
        /// <list type="bullet">
        /// <item><description><see cref="string"/> for <c>JsonValueKind.String</c></description></item>
        /// <item><description><see cref="int"/> if the number fits in <see cref="int"/>; otherwise <see cref="double"/> for <c>JsonValueKind.Number</c></description></item>
        /// <item><description><see cref="bool"/> for <c>JsonValueKind.True</c> and <c>JsonValueKind.False</c></description></item>
        /// <item><description><c>null</c> for <c>JsonValueKind.Null</c></description></item>
        /// <item><description>Raw JSON string via <see cref="JsonElement.GetRawText"/> for unsupported kinds</description></item>
        /// </list>
        /// </returns>
        private static object JsonElementToClrValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => element.GetRawText()
            };
        }

        /// <summary>
        /// Converts a <see cref="JToken"/> to a corresponding CLR value.
        /// </summary>
        /// <param name="token">The <see cref="JToken"/> to convert.</param>
        /// <returns>
        /// A CLR value corresponding to the token's type:
        /// <list type="bullet">
        /// <item><description><see cref="string"/> for <c>JTokenType.String</c></description></item>
        /// <item><description><see cref="int"/> for <c>JTokenType.Integer</c></description></item>
        /// <item><description><see cref="double"/> for <c>JTokenType.Float</c></description></item>
        /// <item><description><see cref="bool"/> for <c>JTokenType.Boolean</c></description></item>
        /// <item><description><c>null</c> for <c>JTokenType.Null</c></description></item>
        /// <item><description><see cref="string"/> (via <c>ToString()</c>) for other token types</description></item>
        /// </list>
        /// </returns>
        private static object JTokenToClrValue(JToken token)
        {
            return token.Type switch
            {
                JTokenType.String => token.Value<string>()!,
                JTokenType.Integer => token.Value<int>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.Null => null!,
                _ => token.ToString()
            };
        }

        /// <summary>
        /// Attempts to parse the given string as a constant of type <c>int</c>, <c>double</c>, or <c>bool</c>.
        /// </summary>
        /// <param name="token">The input string that may represent a constant value.</param>
        /// <param name="value">
        /// When this method returns, contains the parsed value as an <c>int</c>, <c>double</c>, or <c>bool</c>
        /// if the parsing was successful; otherwise, <c>null</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the string was successfully parsed as one of the supported constant types;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool TryParseConstant(string token, out object value)
        {
            if (int.TryParse(token, out int intVal))
            {
                value = intVal;
                return true;
            }
            if (double.TryParse(token, out double doubleVal))
            {
                value = doubleVal;
                return true;
            }
            if (bool.TryParse(token, out bool boolVal))
            {
                value = boolVal;
                return true;
            }

            value = null!;
            return false;
        }

        /// <summary>
        /// Compares two operands using the specified comparison operator.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="op">
        /// The comparison operator. Supported values are:
        /// <c>"=="</c>, <c>"!="</c>, <c>"&gt;"</c>, <c>"&lt;"</c>, <c>"&gt;="</c>, <c>"&lt;="</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the comparison evaluates to true; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either operand is null.</exception>
        /// <exception cref="InvalidCastException">
        /// Thrown if operands cannot be converted to a common comparable type.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if operands are of incompatible types or if the operator is unknown.
        /// </exception>
        private static bool CompareOperands(object left, object right, string op)
        {
            if (left == null || right == null)
                throw new ArgumentNullException("Operands must not be null.");

            Type leftType = left.GetType();
            Type rightType = right.GetType();

            bool leftIsPrimitiveOrString = IsPrimitiveOrString(leftType);
            bool rightIsPrimitiveOrString = IsPrimitiveOrString(rightType);

            Type targetType;

            if (leftType == rightType)
            {
                targetType = leftType;
            }
            else if (leftIsPrimitiveOrString && !rightIsPrimitiveOrString)
            {
                try
                {
                    right = Convert.ChangeType(right, leftType);
                    targetType = leftType;
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException($"Cannot convert right operand of type {rightType} to {leftType}.", ex);
                }
            }
            else if (!leftIsPrimitiveOrString && rightIsPrimitiveOrString)
            {
                try
                {
                    left = Convert.ChangeType(left, rightType);
                    targetType = rightType;
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException($"Cannot convert left operand of type {leftType} to {rightType}.", ex);
                }
            }
            else
            {
                throw new InvalidOperationException($"Operands have incompatible types: {leftType} and {rightType}.");
            }

            int comparisonResult;

            if (targetType == typeof(string))
            {
                comparisonResult = string.Compare((string)left, (string)right, StringComparison.Ordinal);
            }
            else if (typeof(IComparable).IsAssignableFrom(targetType))
            {
                comparisonResult = ((IComparable)left).CompareTo(right);
            }
            else
            {
                throw new InvalidOperationException($"Type {targetType} is not comparable.");
            }

            return op switch
            {
                "==" => comparisonResult == 0,
                "!=" => comparisonResult != 0,
                ">" => comparisonResult > 0,
                "<" => comparisonResult < 0,
                ">=" => comparisonResult >= 0,
                "<=" => comparisonResult <= 0,
                _ => throw new InvalidOperationException($"Unknown operator {op}")
            };
        }

        /// <summary>
        /// Determines whether the specified <see cref="Type"/> is a primitive type,
        /// a string, or a decimal.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>
        /// <c>true</c> if the type is a primitive, string, or decimal; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsPrimitiveOrString(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }
    }
}
