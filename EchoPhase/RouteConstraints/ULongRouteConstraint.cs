namespace EchoPhase.RouteConstraints
{
	public class ULongRouteConstraint : IRouteConstraint
	{
		public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
		{
			if (values[routeKey] is string valueAsString)
			{
				return ulong.TryParse(valueAsString, out _);
			}
			return false;
		}
	}
}
