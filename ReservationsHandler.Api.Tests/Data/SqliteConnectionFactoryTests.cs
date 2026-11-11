using System.Data;
using ReservationsHandler.Api.Data;

namespace ReservationsHandler.Api.Tests.Data;

[TestFixture]
public class SqliteConnectionFactoryTests
{
    // An in-memory database needs no file, so these stay pure unit tests.
    private const string InMemory = "Data Source=:memory:";

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Constructor_Throws_WhenConnectionStringIsMissing(string? connectionString)
    {
        Assert.That(
            () => new SqliteConnectionFactory(connectionString!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task OpenConnectionAsync_ReturnsAnOpenConnection()
    {
        IDbConnectionFactory factory = new SqliteConnectionFactory(InMemory);

        await using var connection = await factory.OpenConnectionAsync();

        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public async Task OpenConnectionAsync_ReturnsANewConnectionEachTime()
    {
        IDbConnectionFactory factory = new SqliteConnectionFactory(InMemory);

        await using var first = await factory.OpenConnectionAsync();
        await using var second = await factory.OpenConnectionAsync();

        Assert.That(first, Is.Not.SameAs(second));
    }

    [Test]
    public void OpenConnectionAsync_Throws_WhenTheDatabaseCannotBeOpened()
    {
        // A directory that does not exist; the connection must be disposed, not leaked, on failure.
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nope", "db.sqlite");
        IDbConnectionFactory factory = new SqliteConnectionFactory($"Data Source={missing};Mode=ReadWrite");

        Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => factory.OpenConnectionAsync());
    }
}
