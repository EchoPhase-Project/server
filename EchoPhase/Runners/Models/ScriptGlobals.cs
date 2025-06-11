using EchoPhase.Interfaces;

namespace EchoPhase.Runners.Models
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
