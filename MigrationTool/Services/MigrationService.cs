using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using IdentitySuite.MigrationTool.Configuration;
using IdentitySuite.MigrationTool.Data;
using IdentitySuite.MigrationTool.Models.V1;
using IdentitySuite.MigrationTool.Models.V2;

namespace IdentitySuite.MigrationTool.Services;

public interface IMigrationService
{
    Task RunMigrationWizardAsync();
}

public class MigrationService(IConfiguration configuration) : IMigrationService
{
    private readonly Dictionary<int, Guid> _userIdMapping = [];
    private readonly Dictionary<int, Guid> _roleIdMapping = [];
    private readonly Dictionary<int, Guid> _applicationIdMapping = [];
    private readonly Dictionary<int, Guid> _authorizationIdMapping = [];
    private readonly List<MigrationResult> _results = [];

    public async Task RunMigrationWizardAsync()
    {
        try
        {
            // Load configuration
            var config = configuration.Get<DatabaseConfig>()
                ?? throw new InvalidOperationException("Failed to load database configuration");

            Log.Information("Source Database: {Provider}", config.SourceDatabase.Provider);
            Log.Information("Target Database: {Provider}", config.TargetDatabase.Provider);
            Log.Information("");

            // Test connections
            Log.Information("Testing database connections...");

            var sourceOptions = BuildDbContextOptions<SourceDbContext>(
                config.SourceDatabase.Provider,
                config.SourceDatabase.ConnectionString);

            var targetOptions = BuildDbContextOptions<TargetDbContext>(
                config.TargetDatabase.Provider,
                config.TargetDatabase.ConnectionString);

            await using (var sourceDb = new SourceDbContext(sourceOptions))
            {
                if (!await sourceDb.Database.CanConnectAsync())
                {
                    throw new InvalidOperationException("Cannot connect to source database");
                }
                Log.Information("✓ Source database connection successful");
            }

            await using (var targetDb = new TargetDbContext(targetOptions))
            {
                if (!await targetDb.Database.CanConnectAsync())
                {
                    throw new InvalidOperationException("Cannot connect to target database");
                }
                Log.Information("✓ Target database connection successful");
            }

            Log.Information("");
            Log.Warning("WARNING: This will copy data from source to target database.");
            Log.Warning("Make sure the target database is properly initialized and ready.");
            Console.Write("Do you want to proceed? (yes/no): ");

            var response = Console.ReadLine()?.Trim().ToLower();
            if (response != "yes" && response != "y")
            {
                Log.Information("Migration cancelled by user");
                return;
            }

            Log.Information("");
            Log.Information("Starting migration process...");
            Log.Information("=================================================");
            Log.Information("");

            // Execute migration
            await ExecuteMigrationAsync(sourceOptions, targetOptions);

            // Print summary
            PrintMigrationSummary();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fatal error during migration wizard");
            throw;
        }
    }

    private async Task ExecuteMigrationAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        // Step 1: Migrate tables without foreign key dependencies
        await MigrateSimpleTablesAsync(sourceOptions, targetOptions);

        // Step 2: Migrate Identity base tables (Users, Roles)
        await MigrateUsersAsync(sourceOptions, targetOptions);
        await MigrateRolesAsync(sourceOptions, targetOptions);

        // Step 3: Migrate OpenIddict base tables
        await MigrateApplicationsAsync(sourceOptions, targetOptions);
        await MigrateScopesAsync(sourceOptions, targetOptions);

        // Step 4: Migrate dependent Identity tables
        await MigrateUserClaimsAsync(sourceOptions, targetOptions);
        await MigrateRoleClaimsAsync(sourceOptions, targetOptions);
        await MigrateUserLoginsAsync(sourceOptions, targetOptions);
        await MigrateUserRolesAsync(sourceOptions, targetOptions);
        await MigrateUserTokensAsync(sourceOptions, targetOptions);

        // Step 5: Migrate dependent OpenIddict tables
        await MigrateAuthorizationsAsync(sourceOptions, targetOptions);
        await MigrateTokensAsync(sourceOptions, targetOptions);
    }

    private async Task MigrateSimpleTablesAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        // DataProtectionKeys - Let DB generate new IDs
        await MigrateTableAsync(
            "IdentitySuite.DataProtectionKeys",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var items = await src.DataProtectionKeys.AsNoTracking().ToListAsync();
                foreach (var item in items)
                {
                    // Don't set Id, let the DB generate it
                    var newItem = new DataProtectionKeyV2
                    {
                        FriendlyName = item.FriendlyName,
                        Xml = item.Xml
                    };
                    await tgt.DataProtectionKeys.AddAsync(newItem);
                }
                if (items.Any())
                {
                    await tgt.SaveChangesAsync();
                }
                return items.Count;
            });

        // MessageTemplates - Let DB generate new IDs
        await MigrateTableAsync(
            "IdentitySuite.MessageTemplates",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var items = await src.MessageTemplates.AsNoTracking().ToListAsync();
                foreach (var item in items)
                {
                    var existingMessage = await tgt.MessageTemplates
                        .FirstOrDefaultAsync(u => u.ClientId == item.ClientId && u.MessageType == item.MessageType && u.LanguageCode == item.LanguageCode);

                    if (existingMessage != null)
                    {
                        existingMessage.MessageHtml = item.MessageHtml;
                        existingMessage.MessageCode = item.MessageCode;
                        tgt.MessageTemplates.Update(existingMessage);
                    }
                    else
                    {
                        // Don't set Id, let the DB generate it
                        var newItem = new MessageTemplateV2
                        {
                            ClientId = item.ClientId,
                            MessageType = item.MessageType,
                            LanguageCode = item.LanguageCode,
                            MessageHtml = item.MessageHtml,
                            MessageCode = item.MessageCode
                        };
                        await tgt.MessageTemplates.AddAsync(newItem);
                    }
                }
                if (items.Any())
                {
                    await tgt.SaveChangesAsync();
                }
                return items.Count;
            });

        // SessionCache
        await MigrateTableAsync(
            "IdentitySuite.SessionCaches",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var items = await src.SessionCache.AsNoTracking().ToListAsync();
                foreach (var item in items)
                {
                    var newItem = new SessionCacheV2
                    {
                        Id = item.Id,
                        Value = item.Value,
                        ExpiresAtTime = item.ExpiresAtTime,
                        SlidingExpirationInSeconds = item.SlidingExpirationInSeconds,
                        AbsoluteExpiration = item.AbsoluteExpiration
                    };
                    await tgt.SessionCache.AddAsync(newItem);
                }
                if (items.Any())
                {
                    await tgt.SaveChangesAsync();
                }
                return items.Count;
            });
    }

    private async Task MigrateUsersAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.Users",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var users = await src.Users.AsNoTracking().OrderBy(p => p.UserId).ToListAsync();
                var migratedCount = 0;

                foreach (var user in users)
                {
                    // Check if user already exists by NormalizedUserName
                    var existingUser = await tgt.Users
                        .FirstOrDefaultAsync(u => u.NormalizedUserName == user.NormalizedUserName);

                    if (existingUser != null)
                    {
                        // UPDATE existing user
                        _userIdMapping[user.UserId] = existingUser.UserId;

                        existingUser.UserName = user.UserName;
                        existingUser.Email = user.Email;
                        existingUser.NormalizedEmail = user.NormalizedEmail;
                        existingUser.FirstName = user.FirstName;
                        existingUser.LastName = user.LastName;
                        existingUser.EmailConfirmed = user.EmailConfirmed;
                        existingUser.PasswordHash = user.PasswordHash;
                        existingUser.SecurityStamp = user.SecurityStamp;
                        existingUser.ConcurrencyStamp = user.ConcurrencyStamp;
                        existingUser.PhoneNumber = user.PhoneNumber;
                        existingUser.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
                        existingUser.TwoFactorEnabled = user.TwoFactorEnabled;
                        existingUser.LockoutEnd = user.LockoutEnd;
                        existingUser.LockoutEnabled = user.LockoutEnabled;
                        existingUser.AccessFailedCount = user.AccessFailedCount;
                        existingUser.CreatedOn = user.CreatedOn;
                        existingUser.LastUpdated = user.LastUpdated;

                        tgt.Users.Update(existingUser);
                        Log.Information("Updating existing user: {UserName}", user.UserName);
                    }
                    else
                    {
                        // INSERT new user
                        var newGuid = Guid.CreateVersion7();
                        _userIdMapping[user.UserId] = newGuid;

                        var userV2 = new UserV2
                        {
                            UserId = newGuid,
                            UserName = user.UserName,
                            NormalizedUserName = user.NormalizedUserName,
                            Email = user.Email,
                            NormalizedEmail = user.NormalizedEmail,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            EmailConfirmed = user.EmailConfirmed,
                            PasswordHash = user.PasswordHash,
                            SecurityStamp = user.SecurityStamp,
                            ConcurrencyStamp = user.ConcurrencyStamp,
                            PhoneNumber = user.PhoneNumber,
                            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                            TwoFactorEnabled = user.TwoFactorEnabled,
                            LockoutEnd = user.LockoutEnd,
                            LockoutEnabled = user.LockoutEnabled,
                            AccessFailedCount = user.AccessFailedCount,
                            CreatedOn = user.CreatedOn,
                            LastUpdated = user.LastUpdated
                        };

                        await tgt.Users.AddAsync(userV2);
                        Log.Information("Inserting new user: {UserName}", user.UserName);
                    }

                    migratedCount++;
                }

                await tgt.SaveChangesAsync();
                return migratedCount;
            });
    }

    private async Task MigrateRolesAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.Roles",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var roles = await src.Roles.AsNoTracking().OrderBy(p => p.RoleId).ToListAsync();
                var migratedCount = 0;

                foreach (var role in roles)
                {
                    // Check if role already exists by NormalizedName
                    var existingRole = await tgt.Roles
                        .FirstOrDefaultAsync(r => r.NormalizedName == role.NormalizedName);

                    if (existingRole != null)
                    {
                        // UPDATE existing role
                        _roleIdMapping[role.RoleId] = existingRole.RoleId;

                        existingRole.Name = role.Name;
                        existingRole.ConcurrencyStamp = role.ConcurrencyStamp;
                        existingRole.CreatedOn = role.CreatedOn;
                        existingRole.LastUpdated = role.LastUpdated;

                        tgt.Roles.Update(existingRole);
                        Log.Information("Updating existing role: {RoleName}", role.Name);
                    }
                    else
                    {
                        // INSERT new role
                        var newGuid = Guid.CreateVersion7();
                        _roleIdMapping[role.RoleId] = newGuid;

                        var roleV2 = new RoleV2
                        {
                            RoleId = newGuid,
                            Name = role.Name,
                            NormalizedName = role.NormalizedName,
                            ConcurrencyStamp = role.ConcurrencyStamp,
                            CreatedOn = role.CreatedOn,
                            LastUpdated = role.LastUpdated
                        };

                        await tgt.Roles.AddAsync(roleV2);
                        Log.Information("Inserting new role: {RoleName}", role.Name);
                    }

                    migratedCount++;
                }

                await tgt.SaveChangesAsync();
                return migratedCount;
            });
    }

    private async Task MigrateApplicationsAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityServer.Applications",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var apps = await src.Applications.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
                var migratedCount = 0;

                foreach (var app in apps)
                {
                    // Check if application already exists by ClientId
                    var existingApp = await tgt.Applications
                        .FirstOrDefaultAsync(a => a.ClientId == app.ClientId);

                    if (existingApp != null)
                    {
                        // UPDATE existing application
                        _applicationIdMapping[app.Id] = existingApp.Id;

                        existingApp.ApplicationType = app.ApplicationType;
                        existingApp.ClientSecret = app.ClientSecret;
                        existingApp.ClientType = app.ClientType;
                        existingApp.ConcurrencyToken = app.ConcurrencyToken;
                        existingApp.ConsentType = app.ConsentType;
                        existingApp.DisplayName = app.DisplayName;
                        existingApp.DisplayNames = app.DisplayNames;
                        existingApp.JsonWebKeySet = app.JsonWebKeySet;
                        existingApp.Permissions = app.Permissions;
                        existingApp.PostLogoutRedirectUris = app.PostLogoutRedirectUris;
                        existingApp.Properties = app.Properties;
                        existingApp.RedirectUris = app.RedirectUris;
                        existingApp.Requirements = app.Requirements;
                        existingApp.Settings = app.Settings;

                        tgt.Applications.Update(existingApp);
                        Log.Information("Updating existing application: {ClientId}", app.ClientId);
                    }
                    else
                    {
                        // INSERT new application
                        var newGuid = Guid.CreateVersion7();
                        _applicationIdMapping[app.Id] = newGuid;

                        var appV2 = new ApplicationV2
                        {
                            Id = newGuid,
                            ApplicationType = app.ApplicationType,
                            ClientId = app.ClientId,
                            ClientSecret = app.ClientSecret,
                            ClientType = app.ClientType,
                            ConcurrencyToken = app.ConcurrencyToken,
                            ConsentType = app.ConsentType,
                            DisplayName = app.DisplayName,
                            DisplayNames = app.DisplayNames,
                            JsonWebKeySet = app.JsonWebKeySet,
                            Permissions = app.Permissions,
                            PostLogoutRedirectUris = app.PostLogoutRedirectUris,
                            Properties = app.Properties,
                            RedirectUris = app.RedirectUris,
                            Requirements = app.Requirements,
                            Settings = app.Settings
                        };

                        await tgt.Applications.AddAsync(appV2);
                        Log.Information("Inserting new application: {ClientId}", app.ClientId);
                    }

                    migratedCount++;
                }

                await tgt.SaveChangesAsync();
                return migratedCount;
            });
    }

    private async Task MigrateScopesAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityServer.Scopes",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var scopes = await src.Scopes.AsNoTracking().ToListAsync();
                var migratedCount = 0;

                foreach (var scope in scopes)
                {
                    // Check if scope already exists by Name
                    var existingScope = await tgt.Scopes
                        .FirstOrDefaultAsync(s => s.Name == scope.Name);

                    if (existingScope != null)
                    {
                        // UPDATE existing scope
                        existingScope.ConcurrencyToken = scope.ConcurrencyToken;
                        existingScope.Description = scope.Description;
                        existingScope.Descriptions = scope.Descriptions;
                        existingScope.DisplayName = scope.DisplayName;
                        existingScope.DisplayNames = scope.DisplayNames;
                        existingScope.Properties = scope.Properties;
                        existingScope.Resources = scope.Resources;

                        tgt.Scopes.Update(existingScope);
                        Log.Information("Updating existing scope: {Name}", scope.Name);
                    }
                    else
                    {
                        // INSERT new scope
                        var scopeV2 = new ScopeV2
                        {
                            Id = Guid.CreateVersion7(),
                            ConcurrencyToken = scope.ConcurrencyToken,
                            Description = scope.Description,
                            Descriptions = scope.Descriptions,
                            DisplayName = scope.DisplayName,
                            DisplayNames = scope.DisplayNames,
                            Name = scope.Name,
                            Properties = scope.Properties,
                            Resources = scope.Resources
                        };

                        await tgt.Scopes.AddAsync(scopeV2);
                        Log.Information("Inserting new scope: {Name}", scope.Name);
                    }

                    migratedCount++;
                }

                await tgt.SaveChangesAsync();
                return migratedCount;
            });
    }

    private async Task MigrateUserClaimsAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.UserClaims",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var claims = await src.UserClaims.AsNoTracking().ToListAsync();
                var migratedCount = 0;

                foreach (var claim in claims)
                {
                    if (!_userIdMapping.TryGetValue(claim.UserId, out var newUserId))
                    {
                        Log.Warning("Skipping UserClaim {Id} - User {UserId} not found in mapping",
                            claim.UserClaimId, claim.UserId);
                        continue;
                    }

                    // Don't set Id, let DB generate it
                    var claimV2 = new UserClaimV2
                    {
                        UserId = newUserId,
                        ClaimType = claim.ClaimType,
                        ClaimValue = claim.ClaimValue,
                        CreatedOn = claim.CreatedOn,
                        LastUpdated = claim.LastUpdated
                    };

                    await tgt.UserClaims.AddAsync(claimV2);
                    migratedCount++;
                }

                if (migratedCount > 0)
                {
                    await tgt.SaveChangesAsync();
                }
                return migratedCount;
            });
    }

    private async Task MigrateRoleClaimsAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.RoleClaims",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var claims = await src.RoleClaims.AsNoTracking().ToListAsync();
                var migratedCount = 0;

                foreach (var claim in claims)
                {
                    if (!_roleIdMapping.TryGetValue(claim.RoleId, out var newRoleId))
                    {
                        Log.Warning("Skipping RoleClaim {Id} - Role {RoleId} not found in mapping",
                            claim.RoleClaimId, claim.RoleId);
                        continue;
                    }

                    // Don't set Id, let DB generate it
                    var claimV2 = new RoleClaimV2
                    {
                        RoleId = newRoleId,
                        ClaimType = claim.ClaimType,
                        ClaimValue = claim.ClaimValue,
                        CreatedOn = claim.CreatedOn,
                        LastUpdated = claim.LastUpdated
                    };

                    await tgt.RoleClaims.AddAsync(claimV2);
                    migratedCount++;
                }

                if (migratedCount > 0)
                {
                    await tgt.SaveChangesAsync();
                }
                return migratedCount;
            });
    }

    private async Task MigrateUserLoginsAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.UserLogins",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var logins = await src.UserLogins.AsNoTracking().ToListAsync();

                foreach (var login in logins)
                {
                    if (!_userIdMapping.TryGetValue(login.UserId, out var newUserId))
                    {
                        Log.Warning("Skipping UserLogin {Provider}/{Key} - User {UserId} not found in mapping",
                            login.LoginProvider, login.ProviderKey, login.UserId);
                        continue;
                    }

                    var existingItem = await tgt.UserLogins.FirstOrDefaultAsync(p => p.LoginProvider == login.LoginProvider && p.ProviderKey == login.ProviderKey);

                    if (existingItem == null)
                    {
                        var loginV2 = new UserLoginV2
                        {
                            LoginProvider = login.LoginProvider,
                            ProviderKey = login.ProviderKey,
                            ProviderDisplayName = login.ProviderDisplayName,
                            UserId = newUserId
                        };

                        await tgt.UserLogins.AddAsync(loginV2);
                    }
                    else
                    {
                        existingItem.ProviderDisplayName = login.ProviderDisplayName;
                        existingItem.UserId = newUserId;

                        tgt.UserLogins.Update(existingItem);
                    }
                }

                await tgt.SaveChangesAsync();
                return logins.Count;
            });
    }

    private async Task MigrateUserRolesAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.UserRoles",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var userRoles = await src.UserRoles.AsNoTracking().ToListAsync();

                foreach (var ur in userRoles)
                {
                    if (!_userIdMapping.TryGetValue(ur.UserId, out var newUserId))
                    {
                        Log.Warning("Skipping UserRole - User {UserId} not found in mapping", ur.UserId);
                        continue;
                    }

                    if (!_roleIdMapping.TryGetValue(ur.RoleId, out var newRoleId))
                    {
                        Log.Warning("Skipping UserRole - Role {RoleId} not found in mapping", ur.RoleId);
                        continue;
                    }

                    var existingLink = await tgt.UserRoles
                        .FirstOrDefaultAsync(r => r.UserId == newUserId && r.RoleId == newRoleId);

                    if (existingLink == null)
                    {
                        var urV2 = new UserRoleV2
                        {
                            UserId = newUserId,
                            RoleId = newRoleId
                        };

                        await tgt.UserRoles.AddAsync(urV2);
                    }
                }

                await tgt.SaveChangesAsync();
                return userRoles.Count;
            });
    }

    private async Task MigrateUserTokensAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityUser.UserTokens",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var tokens = await src.UserTokens.AsNoTracking().ToListAsync();

                foreach (var token in tokens)
                {
                    if (!_userIdMapping.TryGetValue(token.UserId, out var newUserId))
                    {
                        Log.Warning("Skipping UserToken {Provider}/{Name} - User {UserId} not found in mapping",
                            token.LoginProvider, token.Name, token.UserId);
                        continue;
                    }

                    var existingItem = await tgt.UserTokens.FirstOrDefaultAsync(p => p.UserId == newUserId && p.LoginProvider == token.LoginProvider && p.Name == token.Name);

                    if (existingItem == null)
                    {
                        var tokenV2 = new UserTokenV2
                        {
                            UserId = newUserId,
                            LoginProvider = token.LoginProvider,
                            Name = token.Name,
                            Value = token.Value
                        };

                        await tgt.UserTokens.AddAsync(tokenV2);
                    }
                    else
                    {
                        existingItem.Value = token.Value;

                        tgt.UserTokens.Update(existingItem);
                    }
                }

                await tgt.SaveChangesAsync();
                return tokens.Count;
            });
    }

    private async Task MigrateAuthorizationsAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityServer.Authorizations",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var auths = await src.Authorizations.AsNoTracking().ToListAsync();

                foreach (var auth in auths)
                {
                    var newGuid = Guid.CreateVersion7();
                    _authorizationIdMapping[auth.Id] = newGuid;

                    Guid? newAppId = null;
                    if (auth.ApplicationId.HasValue)
                    {
                        if (!_applicationIdMapping.TryGetValue(auth.ApplicationId.Value, out var mappedAppId))
                        {
                            Log.Warning("Authorization {Id} references unknown Application {AppId}",
                                auth.Id, auth.ApplicationId.Value);
                        }
                        else
                        {
                            newAppId = mappedAppId;
                        }
                    }

                    var authV2 = new AuthorizationV2
                    {
                        Id = newGuid,
                        ApplicationId = newAppId,
                        ConcurrencyToken = auth.ConcurrencyToken,
                        CreationDate = auth.CreationDate,
                        Properties = auth.Properties,
                        Scopes = auth.Scopes,
                        Status = auth.Status,
                        Subject = auth.Subject,
                        Type = auth.Type
                    };

                    await tgt.Authorizations.AddAsync(authV2);
                }

                await tgt.SaveChangesAsync();
                return auths.Count;
            });
    }

    private async Task MigrateTokensAsync(
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions)
    {
        await MigrateTableAsync(
            "IdentityServer.Tokens",
            sourceOptions,
            targetOptions,
            async (src, tgt) =>
            {
                var tokens = await src.Tokens.AsNoTracking().ToListAsync();

                foreach (var token in tokens)
                {
                    Guid? newAppId = null;
                    if (token.ApplicationId.HasValue)
                    {
                        if (!_applicationIdMapping.TryGetValue(token.ApplicationId.Value, out var mappedAppId))
                        {
                            Log.Warning("Token {Id} references unknown Application {AppId}",
                                token.Id, token.ApplicationId.Value);
                        }
                        else
                        {
                            newAppId = mappedAppId;
                        }
                    }

                    Guid? newAuthId = null;
                    if (token.AuthorizationId.HasValue)
                    {
                        if (!_authorizationIdMapping.TryGetValue(token.AuthorizationId.Value, out var mappedAuthId))
                        {
                            Log.Warning("Token {Id} references unknown Authorization {AuthId}",
                                token.Id, token.AuthorizationId.Value);
                        }
                        else
                        {
                            newAuthId = mappedAuthId;
                        }
                    }

                    var existingItem = await tgt.Tokens.FirstOrDefaultAsync(p => p.ReferenceId == token.ReferenceId);

                    if (existingItem == null)
                    {
                        var tokenV2 = new TokenV2
                        {
                            Id = Guid.CreateVersion7(),
                            ApplicationId = newAppId,
                            AuthorizationId = newAuthId,
                            ConcurrencyToken = token.ConcurrencyToken,
                            CreationDate = token.CreationDate,
                            ExpirationDate = token.ExpirationDate,
                            Payload = token.Payload,
                            Properties = token.Properties,
                            RedemptionDate = token.RedemptionDate,
                            ReferenceId = token.ReferenceId,
                            Status = token.Status,
                            Subject = token.Subject,
                            Type = token.Type
                        };

                        await tgt.Tokens.AddAsync(tokenV2);
                    }
                }

                await tgt.SaveChangesAsync();
                return tokens.Count;
            });
    }

    private async Task MigrateTableAsync(
        string tableName,
        DbContextOptions<SourceDbContext> sourceOptions,
        DbContextOptions<TargetDbContext> targetOptions,
        Func<SourceDbContext, TargetDbContext, Task<int>> migrateFunc)
    {
        var result = new MigrationResult { TableName = tableName };

        try
        {
            await using var sourceDb = new SourceDbContext(sourceOptions);
            await using var targetDb = new TargetDbContext(targetOptions);

            Log.Information("Processing table {TableName}...", tableName);

            var sourceCount = await migrateFunc(sourceDb, targetDb);
            result.SourceCount = sourceCount;
            result.TargetCount = sourceCount;
            result.Success = true;

            Log.Information("✓ Table {TableName}: {Count} records migrated successfully",
                tableName, sourceCount);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            Log.Error(ex, "✗ Error migrating table {TableName}", tableName);
        }

        _results.Add(result);
    }

    private void PrintMigrationSummary()
    {
        Log.Information("");
        Log.Information("=================================================");
        Log.Information("MIGRATION SUMMARY");
        Log.Information("=================================================");
        Log.Information("");

        var successful = _results.Where(r => r.Success).ToList();
        var failed = _results.Where(r => !r.Success).ToList();

        if (successful.Any())
        {
            Log.Information("✅ Successfully migrated tables ({Count}):", successful.Count);
            foreach (var result in successful)
            {
                Log.Information("   {TableName}: {Count} records", result.TableName, result.SourceCount);
            }
            Log.Information("");
        }

        if (failed.Any())
        {
            Log.Error("❌ Failed migrations ({Count}):", failed.Count);
            foreach (var result in failed)
            {
                Log.Error("   {TableName}: {Error}", result.TableName, result.ErrorMessage);
            }
            Log.Information("");
        }

        Log.Information("Statistics:");
        Log.Information("   Total tables processed: {Total}", _results.Count);
        Log.Information("   Successful: {Success}", successful.Count);
        Log.Information("   Failed: {Failed}", failed.Count);
        Log.Information("   Total records migrated: {Total}", successful.Sum(r => r.SourceCount));
        Log.Information("");
    }

    private static DbContextOptions<T> BuildDbContextOptions<T>(string provider, string connectionString)
        where T : Microsoft.EntityFrameworkCore.DbContext
    {
        var builder = new DbContextOptionsBuilder<T>();

        switch (provider.ToLower())
        {
            case "sqlserver":
                builder.UseSqlServer(connectionString);
                break;
            case "postgresql":
                builder.UseNpgsql(connectionString);
                break;
            case "mysql":
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not supported");
        }

        return builder.Options;
    }
}

public class MigrationResult
{
    public string TableName { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public int TargetCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}