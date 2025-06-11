using System.Collections.Concurrent;

using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
    public class BitMaskServiceBase : IBitMaskService
    {
        private readonly ConcurrentDictionary<string, int> _map = new();
        private int _nextBit = 0;

        public BitMaskServiceBase()
        {
        }

        public BitMaskServiceBase(params IEnumerable<string> values)
        {
            foreach (var v in values)
                GetOrRegisterBit(v);
        }

        public long Add(long currentValue, params IEnumerable<string> values)
        {
            foreach (var v in values)
            {
                int bit = GetOrRegisterBit(v);
                currentValue |= (1L << bit);
            }
            return currentValue;
        }

        public long Remove(long currentValue, params IEnumerable<string> values)
        {
            foreach (var v in values)
            {
                if (_map.TryGetValue(v, out int bit))
                {
                    currentValue &= ~(1L << bit);
                }
            }
            return currentValue;
        }

        public bool Has(long currentValue, params IEnumerable<string> values)
        {
            foreach (var v in values)
            {
                if (_map.TryGetValue(v, out int bit))
                {
                    if ((currentValue & (1L << bit)) == 0)
                        return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        public long All()
        {
            return _nextBit >= 64 ? ~0L : (1L << _nextBit) - 1;
        }

        public IEnumerable<string> ListAll()
        {
            return _map
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key);
        }

        private int GetOrRegisterBit(string v)
        {
            return _map.GetOrAdd(v, _ =>
            {
                int newBit = Interlocked.Increment(ref _nextBit) - 1;
                if (newBit >= 64)
                    throw new InvalidOperationException("Maximum of 64 values supported.");
                return newBit;
            });
        }
    }
}
