using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Z.Expressions;

namespace EchoPhase.Helpers
{
    /// <summary>
    /// Provides helper methods for parsing, evaluating, and processing expressions with variable support.
    /// </summary>
    public static class ExpressionHelper
    {
        /// <summary>
        /// Processes the input string by replacing variables and evaluating expressions enclosed in parentheses.
        /// Supports nested parentheses and skips evaluation of function calls.
        /// </summary>
        /// <param name="input">The input string potentially containing variables and expressions.</param>
        /// <param name="variables">
        /// A dictionary of variable names and their corresponding values used for substitution and evaluation.
        /// </param>
        /// <returns>
        /// The processed string with variables replaced and expressions evaluated.
        /// Returns <c>null</c> if the input is <c>null</c>.
        /// </returns>
        public static string ProcessStringWithExpressions(string input, IDictionary<string, object> variables)
        {
            if (input == null)
                return null!;

            input = ReplaceVariables(input, variables);

            var doubleParensRegex = new Regex(@"\(\(([^()]+)\)\)");
            var placeholders = new Dictionary<string, string>();
            int placeholderIndex = 0;

            input = doubleParensRegex.Replace(input, m =>
            {
                string inner = m.Groups[1].Value;
                string placeholder = $"__D_EXPR_{placeholderIndex++}__";
                placeholders[placeholder] = $"({inner})";
                return placeholder;
            });

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
                    string expr = m.Value.Substring(1, m.Value.Length - 2);

                    if (funcCallRegex.IsMatch(expr))
                        return m.Value;

                    var evalResult = EvaluateExpressionWithVariables(expr, variables);
                    return evalResult?.ToString() ?? "";
                });

                if (input == previous)
                    break;
            }

            foreach (var kvp in placeholders)
            {
                input = input.Replace(kvp.Key, kvp.Value);
            }

            return input;
        }

        /// <summary>
        /// Evaluates a given expression string after replacing variables with their values.
        /// </summary>
        /// <param name="expr">The expression string to evaluate.</param>
        /// <param name="variables">A dictionary containing variable names and their values for substitution.</param>
        /// <returns>
        /// The result of evaluating the expression, or <c>null</c> if the evaluation yields no result.
        /// </returns>
        /// <exception cref="Exception">
        /// Throws a new exception if evaluation fails, containing details about the original error and the expression.
        /// </exception>
        public static object? EvaluateExpressionWithVariables(string expr, IDictionary<string, object> variables)
        {
            string exprWithValues = ReplaceVariables(expr, variables);

            try
            {
                return Eval.Execute(exprWithValues, variables);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error evaluating expression '{expr}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Evaluates an expression with variables and converts the result to a boolean value.
        /// </summary>
        /// <param name="expression">The expression string to evaluate.</param>
        /// <param name="variables">A dictionary of variable names and their corresponding values.</param>
        /// <returns>
        /// The boolean result of the evaluated expression. Returns <c>false</c> if the result is <c>null</c>.
        /// </returns>
        /// <exception cref="InvalidCastException">
        /// Thrown if the evaluated result cannot be converted to a boolean.
        /// </exception>
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
        /// Replaces variable placeholders in the input text with their corresponding values from the variables dictionary.
        /// Variable placeholders are denoted by single curly braces, e.g. <c>{variableName}</c>.
        /// Double curly braces <c>{{</c> and <c>}}</c> are preserved as literal braces in the output.
        /// </summary>
        /// <param name="text">The input string containing variable placeholders.</param>
        /// <param name="variables">A dictionary containing variable names and their values for replacement.</param>
        /// <returns>
        /// A string with all recognized variable placeholders replaced by their values.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if a variable referenced in the text is not found in the dictionary.
        /// </exception>
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
        /// Resolves a variable path within the given variables dictionary, supporting nested properties using dot notation.
        /// </summary>
        /// <param name="variables">A dictionary of variable names and their corresponding values.</param>
        /// <param name="path">
        /// The variable path to resolve, which may include nested properties separated by dots (e.g., "user.name").
        /// </param>
        /// <returns>
        /// The resolved object value at the specified path, or <c>null</c> if the path is empty or points to a null JSON token.
        /// </returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if the root variable or any nested property in the path is not found.
        /// </exception>
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
