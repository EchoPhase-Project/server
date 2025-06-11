using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Z.Expressions;

namespace EchoPhase.Helpers
{
    public static class ExpressionHelper
    {
        /// <summary>
        /// Полная обработка строки с выражениями и переменными: {{}} → {}, {var} → значение, ((expr)) → (), (expr) → результат
        /// </summary>
        public static string ProcessStringWithExpressions(string input, IDictionary<string, object> variables)
        {
            if (input == null)
                return null!;

            // Экранирование двойных скобок {{...}} -> {...}
            input = ReplaceVariables(input, variables);

            // 1. Заменяем ((...)) на временные токены
            var doubleParensRegex = new Regex(@"\(\(([^()]+)\)\)");
            var placeholders = new Dictionary<string, string>();
            int placeholderIndex = 0;

            input = doubleParensRegex.Replace(input, m =>
            {
                string inner = m.Groups[1].Value;
                string placeholder = $"__D_EXPR_{placeholderIndex++}__";
                placeholders[placeholder] = $"({inner})"; // сохранить в виде (..)
                return placeholder;
            });

            // 2. Обрабатываем выражения в одинарных скобках (..)
            string pattern = @"(?<=^|\s)\((?>[^(){};]+|(?<open>\()|(?<-open>\)))*(?(open)(?!))\)(?=\s|$)";
            var funcCallRegex = new Regex(@"\b\w+\s*\(");
            int maxIterations = 10;
            int iteration = 0;

            while (iteration++ < maxIterations)
            {
                var matches = Regex.Matches(input, pattern);
                if (matches.Count == 0)
                    break;

                string previous = input;

                input = Regex.Replace(input, pattern, m =>
                {
                    string expr = m.Value.Substring(1, m.Value.Length - 2); // убрать внешние скобки

                    // Пропуск вызовов функций
                    if (funcCallRegex.IsMatch(expr))
                        return m.Value;

                    var evalResult = EvaluateExpressionWithVariables(expr, variables);
                    return evalResult?.ToString() ?? "";
                });

                if (input == previous)
                    break; // ничего не изменилось — предотвращаем застревание
            }

            // 3. Восстанавливаем ((...)) как (...) без вычисления
            foreach (var kvp in placeholders)
            {
                input = input.Replace(kvp.Key, kvp.Value);
            }

            return input;
        }

        public static object? EvaluateExpressionWithVariables(string expr, IDictionary<string, object> variables)
        {
            // 1. Заменяем переменные вида {var} на их значения, включая вложенные свойства и экранирование
            string exprWithValues = ReplaceVariables(expr, variables);

            // 2. Вычисляем выражение с помощью Eval
            try
            {
                return Eval.Execute(exprWithValues, variables);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating expression '{expr}': {ex.Message}", ex);
            }
        }

        public static bool EvaluateConditionWithVariables(string expression, IDictionary<string, object> variables)
        {
            var result = EvaluateExpressionWithVariables(expression, variables);

            if (result == null)
                return false;

            if (result is bool b)
                return b;

            if (bool.TryParse(result.ToString(), out bool parsedBool))
                return parsedBool;

            if (double.TryParse(result.ToString(), out double d))
                return d != 0;

            throw new InvalidCastException($"Cannot convert expression result '{result}' to bool.");
        }

        /// <summary>
        /// Заменяет переменные вида {var} в тексте, включая вложенные свойства, поддерживает {{ и }} как экранирование
        /// </summary>
        public static string ReplaceVariables(string text, IDictionary<string, object> variables)
        {
            string tempToken = Guid.NewGuid().ToString();
            text = text.Replace("{{", tempToken + "OPEN").Replace("}}", tempToken + "CLOSE");

            var regex = new Regex(@"\{([a-zA-Z_][a-zA-Z0-9_.]*)\}");

            string result = regex.Replace(text, match =>
            {
                string varName = match.Groups[1].Value.Trim();

                object? value = null;
                try
                {
                    value = ResolveVariablePath(variables, varName);
                }
                catch (KeyNotFoundException ex)
                {
                    throw new KeyNotFoundException($"Variable '{varName}' not found.", ex);
                }

                if (value == null)
                    return "";

                string str;

                if (value is JToken jtoken)
                    str = jtoken.ToString(Newtonsoft.Json.Formatting.None);
                else
                    str = value.ToString() ?? "";

                if (str.Length >= 2 &&
                    ((str.StartsWith("\"") && str.EndsWith("\"")) || (str.StartsWith("'") && str.EndsWith("'"))))
                    str = str.Trim('"', '\'');

                return str;
            });

            return result.Replace(tempToken + "OPEN", "{").Replace(tempToken + "CLOSE", "}");
        }

        /// <summary>
        /// Рекурсивно разрешает переменную с поддержкой вложенных свойств через точку.
        /// </summary>
        public static object? ResolveVariablePath(IDictionary<string, object> variables, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string[] parts = path.Split('.', 2);
            if (!variables.TryGetValue(parts[0], out var current))
                throw new KeyNotFoundException($"Variable '{parts[0]}' not found.");

            if (parts.Length == 1)
                return current;

            string remainingPath = parts[1];

            if (current is JObject jObject)
            {
                var token = jObject.SelectToken(remainingPath);
                if (token == null)
                    throw new KeyNotFoundException($"Property path '{remainingPath}' not found in variable '{parts[0]}'.");
                return token.Type == JTokenType.Null ? null : token;
            }
            else if (current is JToken jToken)
            {
                var token = jToken.SelectToken(remainingPath);
                if (token == null)
                    throw new KeyNotFoundException($"Property path '{remainingPath}' not found in variable '{parts[0]}'.");
                return token.Type == JTokenType.Null ? null : token;
            }
            else
            {
                var prop = current.GetType().GetProperty(remainingPath);
                if (prop == null)
                    throw new KeyNotFoundException($"Property '{remainingPath}' not found in variable '{parts[0]}'.");
                return prop.GetValue(current);
            }
        }
    }
}
