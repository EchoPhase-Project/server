using System.Collections;

namespace EchoPhase.Interfaces
{
    public interface IIntentsBitMaskService : IBitMaskService
    {
        IServiceResult<BitArray> Encode(string[] roles);
        IServiceResult<string[]> Decode(BitArray bitmask);
    }
}
