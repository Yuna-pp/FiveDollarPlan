using FiveDollarPlan.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Npgsql;
using Xunit;

public class DatabaseConfigurationTests
{
    [Fact]
    public void RenderUrlPreservesEscapedCredentialsAndRequiresTls()
    {
        var actual = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.NormalizePostgresConnection(
            "postgresql://demo:p%40ss%3Aword@db.example:5433/demo_db"));
        Assert.Equal("db.example", actual.Host);
        Assert.Equal(5433, actual.Port);
        Assert.Equal("demo_db", actual.Database);
        Assert.Equal("p@ss:word", actual.Password);
        Assert.Equal(SslMode.Require, actual.SslMode);
    }

    [Fact]
    public void RenderUrlUsesDefaultPort()
    {
        var actual = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.NormalizePostgresConnection(
            "postgres://demo:password@db.example/demo_db"));
        Assert.Equal(5432, actual.Port);
    }

    [Fact]
    public void NativeConnectionStringIsSupported()
    {
        var actual = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.NormalizePostgresConnection(
            "Host=db.example;Database=demo_db;Username=demo;Password=password;SSL Mode=Require"));
        Assert.Equal("demo_db", actual.Database);
        Assert.Equal(SslMode.Require, actual.SslMode);
    }

    [Fact]
    public void InvalidUrlIsRejectedRatherThanFallingBackToSqlite()
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.NormalizePostgresConnection(
            "postgres://db.example/demo_db"));
    }

    [Fact]
    public void LocalSqliteModelRetainsExistingTablesWithoutDataProtectionKeys()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        Assert.Null(db.Model.FindEntityType(typeof(DataProtectionKey)));
        Assert.Contains("CREATE TABLE \"Users\"", db.Database.GenerateCreateScript());
    }

    [Fact]
    public void PostgresSchemaIncludesPersistentKeysAndNativeTypes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=schema_only;Username=demo;Password=unused").Options);
        var sql = db.Database.GenerateCreateScript();
        Assert.NotNull(db.Model.FindEntityType(typeof(DataProtectionKey)));
        Assert.Contains("CREATE TABLE \"DataProtectionKeys\"", sql);
        Assert.Contains("uuid", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Users_Email\"", sql);
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Plans_InviteCode\"", sql);
    }
}
