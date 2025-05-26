using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
    public abstract class DataServiceBase<TC, TR, TO> : IDataService<TC, TO>
		where TC : DataServiceBase<TC, TR, TO>
		where TR : IRepositoryBase<TR, TO>
	{
		protected readonly TR _repository;

		public DataServiceBase(
			TR repository
		)
		{
			_repository = repository;
		}

		public virtual TC WithOptions(TO options)
		{
			_repository.WithOptions(options);
			return (TC)this;
		}

		public virtual TC WithOptions(Action<TO> configure)
		{
			_repository.WithOptions(configure);
			return (TC)this;
		}
	}
}
