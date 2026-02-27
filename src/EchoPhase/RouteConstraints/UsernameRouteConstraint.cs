using System.Text.RegularExpressions;

namespace EchoPhase.RouteConstraints
{
    public partial class UsernameRouteConstraint : IRouteConstraint
    {
        [GeneratedRegex(@"^[\w\d]+$", RegexOptions.IgnoreCase)]
        private static partial Regex UsernameRegex();

        public bool Match(
            HttpContext? httpContext,
            IRouter? route,
            string routeKey,
            RouteValueDictionary values,
            RouteDirection routeDirection
        )
        {
            if (!values.TryGetValue(routeKey, out var value) || value is null)
                return false;

            return UsernameRegex().IsMatch(value.ToString()!);
        }
    }
}
