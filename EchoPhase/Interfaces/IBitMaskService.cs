namespace EchoPhase.Interfaces
{
    public interface IBitMaskService
    {
        public long Add(long currentValue, params IEnumerable<string> values);
        public long Remove(long currentValue, params IEnumerable<string> values);
        public bool Has(long currentValue, params IEnumerable<string> values);
        public long All();
        public IEnumerable<string> ListAll();
    }
}
