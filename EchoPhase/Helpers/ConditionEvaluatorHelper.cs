using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace EchoPhase.Helpers
{
    public static class ConditionEvaluatorHelper
    {
        private static readonly Regex ConditionRegex = new Regex(@"(\S+)\s*(==|!=|>=|<=|>|<)\s*(\S+)", RegexOptions.Compiled);

        /// <summary>
        /// Evaluates the condition string against the given variables.
        /// Condition format example: "x > 7", "hello == y", "a <= b"
        /// Supports variable-to-variable, variable-to-constant, and constant-to-variable comparisons.
        /// </summary>
        /// <param name="condition">Condition string</param>
        /// <param name="variables">Dictionary of variable names to values</param>
        /// <returns>True if condition passes, false otherwise</returns>
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
        /// Resolves token to either a variable value or a parsed constant.
        /// </summary>
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

                // JsonElement
                if (current is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException($"Cannot get property '{propName}' from non-object JSON element.");

                    if (!jsonElement.TryGetProperty(propName, out var prop))
                        throw new KeyNotFoundException($"Property '{propName}' not found in JSON element.");

                    current = prop;
                    continue;
                }

                // JToken
                if (current is JToken jtoken)
                {
                    if (jtoken.Type != JTokenType.Object)
                        throw new InvalidOperationException($"Cannot get property '{propName}' from non-object JSON token.");

                    jtoken = jtoken[propName];

                    if (jtoken == null)
                        throw new KeyNotFoundException($"Property '{propName}' not found in JSON token.");

                    current = jtoken;
                    continue;
                }

                // Обычный объект — обращаемся через Reflection
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

            // После обхода всех частей возвращаем результат
            if (current is JsonElement je)
                return JsonElementToClrValue(je);
            if (current is JToken jt)
                return JTokenToClrValue(jt);

            return current;
        }

        private static object JsonElementToClrValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                _ => element.GetRawText() // Для объектов и массивов возвращаем JSON строку
            };
        }

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
        /// Tries to parse token as a primitive constant (int, bool, double).
        /// </summary>
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
        /// Compares two operands with given operator.
        /// Operands should be of same type or convertible.
        /// </summary>
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

        private static bool IsPrimitiveOrString(Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }
    }
}
