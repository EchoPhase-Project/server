using System.Collections;
using EchoPhase.Types.Results;

namespace EchoPhase.Interfaces
{
    public interface IIntentsBitMaskService : IBitMaskService
    {
        IServiceResult<BitArray> Encode(string[] roles);
        IServiceResult<string[]> Decode(BitArray bitmask);
    }
}
