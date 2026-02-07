using EchoPhase.Runners.Roslyn.Contexts;

namespace EchoPhase.Runners.Roslyn
{
    public interface IScriptGlobals<out T>
    {
        public T Payload
        {
            get;
        }
        public IScriptContext Context
        {
            get;
        }
    }
}
