using System.Collections;

namespace EchoPhase.Interfaces
{
    public interface IBitMaskService
    {
        /// <summary>
        /// Adds the specified flags to the bitmask.
        /// </summary>
        /// <param name="bitmask">The original bitmask.</param>
        /// <param name="values">The flag names to add.</param>
        /// <returns>A new BitArray with the specified flags set.</returns>
        BitArray Add(BitArray bitmask, params string[] values);

        BitArray Add(BitArray bitmask, bool registerIfMissing, params string[] values);

        bool IsRegistered(params string[] keys);

        /// <summary>
        /// Removes the specified flags from the bitmask.
        /// </summary>
        /// <param name="bitmask">The original bitmask.</param>
        /// <param name="values">The flag names to remove.</param>
        /// <returns>A new BitArray with the specified flags unset.</returns>
        BitArray Remove(BitArray bitmask, params string[] values);

        /// <summary>
        /// Checks whether the specified flags are all present in the bitmask.
        /// </summary>
        /// <param name="bitmask">The bitmask to check.</param>
        /// <param name="values">The flag names to check.</param>
        /// <returns>True if all specified flags are present and set; otherwise, false.</returns>
        bool Has(BitArray bitmask, params string[] values);

        /// <summary>
        /// Gets the names of the flags that are set in the provided bitmask.
        /// </summary>
        /// <param name="bitmask">The bitmask to inspect.</param>
        /// <returns>An enumerable of flag names that are set in the bitmask.</returns>
        IEnumerable<string> GetFlags(BitArray bitmask);
    }
}
