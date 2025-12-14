# IdentitySuite Migration Tool v1.x → v2.x

A console application to migrate IdentitySuite databases from version 1.x (int keys) to version 2.x (Guid keys).

## Prerequisites

- .NET 9.0 SDK
- Access to both source (v1.x) and target (v2.x) databases
- Target database must be already initialized with v2.x schema

## Project Structure

```
IdentitySuiteMigration/
├── Program.cs                          # Entry point
├── appsettings.json                    # Configuration
├── Configuration/
│   └── DatabaseConfig.cs               # Configuration models
├── Models/
│   ├── V1/                            # Source database entities (int keys)
│   │   └── EntitiesV1.cs
│   └── V2/                            # Target database entities (Guid keys)
│       └── EntitiesV2.cs
├── Data/
│   └── DbContexts.cs                  # EF Core contexts
└── Services/
    └── MigrationService.cs            # Migration logic
```

## Installation

### 1. Clone the repository
```bash
git clone https://github.com/spin973/IdentitySuite.MigrationTool.git
cd IdentitySuite.MigrationTool
```

### 2. Restore dependencies
```bash
dotnet restore
```

### 3. Build the project
```bash
dotnet build
```

## Configuration

Edit `appsettings.json` with your connection strings:

### SQL Server Example

```json
{
  "SourceDatabase": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=IdentitySuiteDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "TargetDatabase": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=IdentitySuiteDbV2;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### PostgreSQL Example

```json
{
  "SourceDatabase": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=IdentitySuiteDb;Username=postgres;Password=yourpassword;"
  },
  "TargetDatabase": {
    "Provider": "PostgreSql",
    "ConnectionString": "Host=localhost;Database=IdentitySuiteDbV2;Username=postgres;Password=yourpassword;"
  }
}
```

### MySQL Example

```json
{
  "SourceDatabase": {
    "Provider": "MySql",
    "ConnectionString": "Server=localhost;Database=IdentitySuiteDb;Uid=root;Pwd=yourpassword;AllowUserVariables=true"
  },
  "TargetDatabase": {
    "Provider": "MySql",
    "ConnectionString": "Server=localhost;Database=IdentitySuiteDbV2;Uid=root;Pwd=yourpassword;AllowUserVariables=true"
  }
}
```

**Important for MySQL**: Always include `AllowUserVariables=true` in the connection string.

## Usage

### Run the migration

```bash
dotnet run
```

### What happens:

1. **Connection Test**: The tool tests connections to both databases
2. **Confirmation**: You'll be asked to confirm before proceeding
3. **Migration**: Tables are migrated in the correct order
4. **Report**: A detailed summary is displayed at the end
5. **Log File**: Check `migration-log-{date}.txt` for complete details

## Troubleshooting

### Connection Errors

**Problem**: Cannot connect to database

**Solution**: 
- Verify connection strings in `appsettings.json`
- Check database server is running
- Verify credentials and permissions
- For MySQL, ensure `AllowUserVariables=true` is in the connection string

## Support

For issues related to:
- **IdentitySuite**: Check the official documentation or GitHub repository
- **This migration tool**: Review logs and error messages carefully

## License

This migration tool is provided as-is for IdentitySuite users migrating from v1.x to v2.x.

## Important Notes

⚠️ **Always backup your databases before running the migration**

⚠️ **Test the migration on a copy of your production database first**

⚠️ **The target database must be empty or properly initialized with v2.x schema**

⚠️ **Do not run this tool against a production database without testing**
