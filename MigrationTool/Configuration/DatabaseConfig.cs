namespace IdentitySuite.MigrationTool.Configuration;

public class DatabaseConfig
{
    public SourceDatabaseConfig SourceDatabase { get; set; } = new();
    public TargetDatabaseConfig TargetDatabase { get; set; } = new();
}

public class SourceDatabaseConfig
{
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public class TargetDatabaseConfig
{
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
    MySql
}