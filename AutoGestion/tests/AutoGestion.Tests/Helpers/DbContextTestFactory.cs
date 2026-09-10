using Microsoft.EntityFrameworkCore;
using AutoGestion.Tests.Data;

namespace AutoGestion.Tests.Helpers
{
    public static class DbContextTestFactory
    {
        public static AppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new AppDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
