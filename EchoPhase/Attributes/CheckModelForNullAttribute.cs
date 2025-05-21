using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EchoPhase.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class CheckModelForNullAttribute : ActionFilterAttribute
    {
        private readonly Func<IDictionary<string, object?>, bool> _validate;

        public CheckModelForNullAttribute() : this(arguments =>
            arguments.Values.Any(value => value == null))
        { }

        public CheckModelForNullAttribute(Func<IDictionary<string, object?>, bool> checkCondition) =>
            _validate = checkCondition;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_validate(context.ActionArguments))
                context.Result = new BadRequestObjectResult("The argument cannot be null");
        }
    }
}
