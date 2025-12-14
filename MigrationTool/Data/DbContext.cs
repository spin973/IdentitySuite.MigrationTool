using IdentitySuite.MigrationTool.Models.V1;
using IdentitySuite.MigrationTool.Models.V2;
using Microsoft.EntityFrameworkCore;

namespace IdentitySuite.MigrationTool.Data;

public class SourceDbContext : DbContext
{
    public SourceDbContext(DbContextOptions<SourceDbContext> options) : base(options) { }

    // Identity Tables
    public DbSet<UserV1> Users { get; set; }
    public DbSet<RoleV1> Roles { get; set; }
    public DbSet<UserRoleV1> UserRoles { get; set; }
    public DbSet<UserClaimV1> UserClaims { get; set; }
    public DbSet<RoleClaimV1> RoleClaims { get; set; }
    public DbSet<UserLoginV1> UserLogins { get; set; }
    public DbSet<UserTokenV1> UserTokens { get; set; }

    // OpenIddict Tables
    public DbSet<ApplicationV1> Applications { get; set; }
    public DbSet<ScopeV1> Scopes { get; set; }
    public DbSet<AuthorizationV1> Authorizations { get; set; }
    public DbSet<TokenV1> Tokens { get; set; }

    // Other Tables
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<MessageTemplate> MessageTemplates { get; set; }
    public DbSet<SessionCache> SessionCache { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite keys
        modelBuilder.Entity<UserRoleV1>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<UserLoginV1>().HasKey(ul => new { ul.LoginProvider, ul.ProviderKey, ul.UserId });
        modelBuilder.Entity<UserTokenV1>().HasKey(ut => new { ut.UserId, ut.LoginProvider, ut.Name });
    }
}

public class TargetDbContext : DbContext
{
    public TargetDbContext(DbContextOptions<TargetDbContext> options) : base(options) { }

    // Identity Tables
    public DbSet<UserV2> Users { get; set; }
    public DbSet<RoleV2> Roles { get; set; }
    public DbSet<UserRoleV2> UserRoles { get; set; }
    public DbSet<UserClaimV2> UserClaims { get; set; }
    public DbSet<RoleClaimV2> RoleClaims { get; set; }
    public DbSet<UserLoginV2> UserLogins { get; set; }
    public DbSet<UserTokenV2> UserTokens { get; set; }

    // OpenIddict Tables
    public DbSet<ApplicationV2> Applications { get; set; }
    public DbSet<ScopeV2> Scopes { get; set; }
    public DbSet<AuthorizationV2> Authorizations { get; set; }
    public DbSet<TokenV2> Tokens { get; set; }

    // Other Tables (same structure)
    public DbSet<DataProtectionKeyV2> DataProtectionKeys { get; set; }
    public DbSet<MessageTemplateV2> MessageTemplates { get; set; }
    public DbSet<SessionCacheV2> SessionCache { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Composite keys
        modelBuilder.Entity<UserRoleV2>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<UserLoginV2>().HasKey(ul => new { ul.LoginProvider, ul.ProviderKey });
        modelBuilder.Entity<UserTokenV2>().HasKey(ut => new { ut.UserId, ut.LoginProvider, ut.Name });

        // Identity property for Claims (they use IDENTITY in the target DB)
        modelBuilder.Entity<UserClaimV2>().Property(uc => uc.Id).ValueGeneratedOnAdd();
        modelBuilder.Entity<RoleClaimV2>().Property(rc => rc.Id).ValueGeneratedOnAdd();
    }
}