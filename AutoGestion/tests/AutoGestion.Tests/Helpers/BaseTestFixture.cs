using AutoGestion.Tests.Data;

namespace AutoGestion.Tests.Helpers
{
    public abstract class BaseTestFixture : IDisposable
    {
        protected readonly AppDbContext Context;

        protected BaseTestFixture()
        {
            Context = DbContextTestFactory.CreateInMemoryDbContext();
        }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
            Context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
