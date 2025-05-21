using System.Web;
using System.Reflection;
using System.Text.Json.Serialization;

namespace EchoPhase.Helpers.Builders
{
	public class QueryStringBuilder : DataBuilderBase<QueryStringBuilder>
	{
		private char _delimiter = '.';

		public QueryStringBuilder() {}

		public override QueryStringBuilder Clone()
		{
			return base.Clone()
				.WithDelimiter(_delimiter);
		}

		public QueryStringBuilder WithDelimiter(char delimiter)
		{
			this._delimiter = delimiter;
			return this;
		}

		public override string Build(object? obj)
		{
			if (obj == null)
				return string.Empty;

			var queryParameters = new List<string>();
			BuildQueryString(obj, queryParameters, string.Empty);
			return string.Join("&", queryParameters);
		}

		private void BuildQueryString(object obj, List<string> queryParameters, string parentKey)
		{
			if (obj == null)
				return;

			Type type = obj.GetType();

			if (_options.IncludeProperties)
			{
				var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var property in properties)
				{
					var value = property.GetValue(obj);
					if (value == null)
						continue;

					var jsonPropertyNameAttr = property.GetCustomAttribute<JsonPropertyNameAttribute>();
					var propertyName = jsonPropertyNameAttr?.Name ?? property.Name;

					string key = string.IsNullOrEmpty(parentKey)
						? propertyName
						: $"{parentKey}{_delimiter}{propertyName}";

					ProcessValue(value, queryParameters, key);
				}
			}

			if (_options.IncludeFields)
			{
				var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
				foreach (var field in fields)
				{
					var value = field.GetValue(obj);
					if (value == null)
						continue;

					var jsonPropertyNameAttr = field.GetCustomAttribute<JsonPropertyNameAttribute>();
					var fieldName = jsonPropertyNameAttr?.Name ?? field.Name;

					string key = string.IsNullOrEmpty(parentKey)
						? fieldName
						: $"{parentKey}{_delimiter}{fieldName}";

					ProcessValue(value, queryParameters, key);
				}
			}
		}

		private void ProcessValue(object value, List<string> queryParameters, string key)
		{
			if (IsSimple(value.GetType()))
			{
				queryParameters.Add($"{HttpUtility.UrlEncode(key)}={HttpUtility.UrlEncode(value.ToString())}");
			}
			else if (value is IEnumerable<object> collection && !(value is string))
			{
				foreach (var item in collection)
				{
					BuildQueryString(item, queryParameters, key);
				}
			}
			else
			{
				BuildQueryString(value, queryParameters, key);
			}
		}
	}
}
