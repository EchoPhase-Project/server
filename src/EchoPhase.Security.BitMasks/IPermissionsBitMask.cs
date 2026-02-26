using System.Collections;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Types.BitMask;
using EchoPhase.Types.Result;

namespace EchoPhase.Security.BitMasks
{
    public interface IPermissionsBitMask : IBitMask
    {
        IServiceResult<IReadOnlyDictionary<Resources, BitArray>> Encode(IReadOnlyDictionary<Resources, string[]> dict);
        IServiceResult<IReadOnlyDictionary<Resources, string[]>> Decode(IReadOnlyDictionary<Resources, BitArray> bitmasks);
    }
}
