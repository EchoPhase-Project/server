using System.Collections;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.BitMasks
{
    public interface IIntentsBitMask : IBitMask
    {
        IServiceResult<BitArray> Encode(string[] roles);
        IServiceResult<string[]> Decode(BitArray bitmask);
    }
}
