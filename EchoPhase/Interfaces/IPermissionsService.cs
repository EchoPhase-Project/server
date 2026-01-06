using System.Collections;
using EchoPhase.Enums;

namespace EchoPhase.Interfaces
{
    public interface IPermissionsBitMaskService : IBitMaskService
    {
        IServiceResult<IReadOnlyDictionary<Resources, BitArray>> Encode(IReadOnlyDictionary<Resources, string[]> dict);
        IServiceResult<IReadOnlyDictionary<Resources, string[]>> Decode(IReadOnlyDictionary<Resources, BitArray> bitmasks);
    }
}
