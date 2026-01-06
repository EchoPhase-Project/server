using System.Collections;

namespace EchoPhase.Interfaces
{
    public interface IRolesBitMaskService : IBitMaskService
    {
        IServiceResult<BitArray> Encode(string[] roles);
        IServiceResult<string[]> Decode(BitArray bitmask);
    }
}
