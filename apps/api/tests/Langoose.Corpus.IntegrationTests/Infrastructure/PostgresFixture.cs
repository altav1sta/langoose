using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Langoose.Corpus.IntegrationTests.Infrastructure;

/// <summary>
/// Boots a disposable Postgres container per test class. Tests within a
/// class share the container; each test owns the data it inserts.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("langoose_corpus_test")
        .Build();

    private NpgsqlDataSource? _dataSource;

    public NpgsqlDataSource DataSource =>
        _dataSource ?? throw new InvalidOperationException("Fixture has not been initialised.");

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
            await _dataSource.DisposeAsync();

        await _container.DisposeAsync();
    }
}
