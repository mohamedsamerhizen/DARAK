using DARAK.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace DARAK.Tests;

internal static class TestDb
{
    public static ApplicationDbContext Create()
    {
        return Create(CreateSharedDatabase());
    }

    public static TestDatabase CreateSharedDatabase()
    {
        return new TestDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
    }

    public static ApplicationDbContext Create(TestDatabase database)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database.Name, database.Root)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    internal sealed record TestDatabase(string Name, InMemoryDatabaseRoot Root);
}
