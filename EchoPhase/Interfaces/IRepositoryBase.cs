namespace EchoPhase.Interfaces
{
    public interface IRepositoryBase<TR, TO> 
	{
		public TR WithOptions(TO options);
		public TR WithOptions(Action<TO> configure);
		public IQueryable Build();
	}
}
