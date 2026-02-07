using EchoPhase.Runners.Roslyn.Contexts;

namespace EchoPhase.Runners.Roslyn
{
    public class ScriptGlobals<T> : IScriptGlobals<T>
    {
        public required T Payload
        {
            set; get;
        }
        public required IScriptContext Context
        {
            set; get;
        }
    }
}
