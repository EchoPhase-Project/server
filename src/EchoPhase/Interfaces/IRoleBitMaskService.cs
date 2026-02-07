using System.Collections;
using EchoPhase.Types.Results;

namespace EchoPhase.Interfaces
{
    public interface IRolesBitMaskService : IBitMaskService
    {
        IServiceResult<BitArray> Encode(string[] roles);
        IServiceResult<string[]> Decode(BitArray bitmask);
    }
}
