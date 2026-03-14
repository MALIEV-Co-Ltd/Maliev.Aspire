using Testcontainers.PostgreSql;

namespace Maliev.Aspire.Tests.Fixtures;

/// <summary>
/// Test fixture for PostgreSQL database using Testcontainers.
/// Provides a real PostgreSQL 18 instance for integration tests.
/// IMPORTANT: Use this instead of InMemory databases for all tests.
/// </summary>
public class PostgresTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresTestFixture"/> class.
    /// </summary>
    public PostgresTestFixture()
    {
        _container =
                new PostgreSqlBuilder("postgres:18-alpine")  // Latest lightweight PostgreSQL
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(5432, true)  // Random port to avoid conflicts
            .WithCleanUp(true) // Automatically clean up container after tests
            .Build();
    }

    /// <summary>
    /// Initializes the fixture asynchronously by starting the PostgreSQL container.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Disposes the fixture asynchronously by stopping and removing the PostgreSQL container.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Gets the connection string for the PostgreSQL test database.
    /// </summary>
    /// <returns>The connection string.</returns>
    public string GetConnectionString() => _container.GetConnectionString();

    /// <summary>
    /// Gets the PostgreSQL container instance for advanced operations.
    /// </summary>
    public PostgreSqlContainer Container => _container;
}




