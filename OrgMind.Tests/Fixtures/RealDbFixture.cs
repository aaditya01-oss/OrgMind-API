using Microsoft.EntityFrameworkCore;
using OrgMind.API.Data;
using Testcontainers.PostgreSql;

namespace OrgMind.Tests;

// Starts one throwaway Postgres (same image as the real database) for all real-database tests,
// builds the real table layout in it, and deletes it at the end. Your own database is never touched.
public class RealDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "schema.sql");
        var sql = "\\set ON_ERROR_STOP on\n" + await File.ReadAllTextAsync(path);
        var result = await _container.ExecScriptAsync(sql);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Could not build the test database: " + result.Stderr);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // A fresh connection each time, so tests prove what really reached the database.
    public OrgMindDbContext NewDb() => new(
        new DbContextOptionsBuilder<OrgMindDbContext>().UseNpgsql(_container.GetConnectionString()).Options);
}

[CollectionDefinition("real-db")]
public class RealDbCollection : ICollectionFixture<RealDbFixture> { }