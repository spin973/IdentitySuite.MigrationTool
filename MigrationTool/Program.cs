using IdentitySuite.MigrationTool.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace IdentitySuite.MigrationTool;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("migration-log-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("=================================================");
            Log.Information("IdentitySuite Database Migration Tool v1.x -> v2.x");
            Log.Information("=================================================");
            Log.Information("");

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IMigrationService, MigrationService>();
                })
                .Build();

            var migrationService = host.Services.GetRequiredService<IMigrationService>();

            await migrationService.RunMigrationWizardAsync();

            Log.Information("");
            Log.Information("Migration process completed. Check the log file for details.");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred during migration");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}