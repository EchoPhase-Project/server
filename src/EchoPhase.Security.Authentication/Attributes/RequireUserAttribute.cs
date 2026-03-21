using EchoPhase.Security.Authentication.Filters;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Security.Authentication.Attributes
{
    public class RequireUserAttribute : TypeFilterAttribute
    {
        public RequireUserAttribute() : base(typeof(RequireUserFilter)) { }
    }
}
