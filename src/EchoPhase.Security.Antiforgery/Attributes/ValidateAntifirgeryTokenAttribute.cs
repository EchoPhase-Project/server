using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using EchoPhase.Security.Antiforgery.Filters;

namespace EchoPhase.Security.Antiforgery.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class BearerOrValidateAntiForgeryTokenAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        public int Order { get; set; } = int.MinValue;
        public bool IsReusable => false;

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<BearerOrValidateAntiForgeryTokenFilter>();
        }
    }
}
