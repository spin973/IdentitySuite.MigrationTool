using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentitySuite.MigrationTool.Models.V1;

// Identity User Tables
[Table("User", Schema = "IdentityUser")]
public class UserV1
{
    [Key]
    public int UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

[Table("Role", Schema = "IdentityUser")]
public class RoleV1
{
    [Key]
    public int RoleId { get; set; }
    public string? Name { get; set; }
    public string? NormalizedName { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

[Table("UserRole", Schema = "IdentityUser")]
public class UserRoleV1
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

[Table("UserClaim", Schema = "IdentityUser")]
public class UserClaimV1
{
    [Key]
    public int UserClaimId { get; set; }
    public int UserId { get; set; }
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

[Table("RoleClaim", Schema = "IdentityUser")]
public class RoleClaimV1
{
    [Key]
    public int RoleClaimId { get; set; }
    public int RoleId { get; set; }
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

[Table("UserLogin", Schema = "IdentityUser")]
public class UserLoginV1
{
    public string LoginProvider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
    public int UserId { get; set; }
}

[Table("UserToken", Schema = "IdentityUser")]
public class UserTokenV1
{
    public int UserId { get; set; }
    public string LoginProvider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

// OpenIddict Tables
[Table("Applications", Schema = "IdentityServer")]
public class ApplicationV1
{
    [Key]
    public int Id { get; set; }
    public string? ApplicationType { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ClientType { get; set; }
    public string? ConcurrencyToken { get; set; }
    public string? ConsentType { get; set; }
    public string? DisplayName { get; set; }
    public string? DisplayNames { get; set; }
    public string? JsonWebKeySet { get; set; }
    public string? Permissions { get; set; }
    public string? PostLogoutRedirectUris { get; set; }
    public string? Properties { get; set; }
    public string? RedirectUris { get; set; }
    public string? Requirements { get; set; }
    public string? Settings { get; set; }
}

[Table("Scopes", Schema = "IdentityServer")]
public class ScopeV1
{
    [Key]
    public int Id { get; set; }
    public string? ConcurrencyToken { get; set; }
    public string? Description { get; set; }
    public string? Descriptions { get; set; }
    public string? DisplayName { get; set; }
    public string? DisplayNames { get; set; }
    public string? Name { get; set; }
    public string? Properties { get; set; }
    public string? Resources { get; set; }
}

[Table("Authorizations", Schema = "IdentityServer")]
public class AuthorizationV1
{
    [Key]
    public int Id { get; set; }
    public int? ApplicationId { get; set; }
    public string? ConcurrencyToken { get; set; }
    public DateTime? CreationDate { get; set; }
    public string? Properties { get; set; }
    public string? Scopes { get; set; }
    public string? Status { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
}

[Table("Tokens", Schema = "IdentityServer")]
public class TokenV1
{
    [Key]
    public int Id { get; set; }
    public int? ApplicationId { get; set; }
    public int? AuthorizationId { get; set; }
    public string? ConcurrencyToken { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Payload { get; set; }
    public string? Properties { get; set; }
    public DateTime? RedemptionDate { get; set; }
    public string? ReferenceId { get; set; }
    public string? Status { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
}

// Other Tables (no key change needed)
[Table("DataProtectionKeys")]
public class DataProtectionKey
{
    [Key]
    public int Id { get; set; }
    public string? FriendlyName { get; set; }
    public string? Xml { get; set; }
}

[Table("MessageTemplates")]
public class MessageTemplate
{
    [Key]
    public int Id { get; set; }
    public string? ClientId { get; set; }
    public string? MessageType { get; set; }
    public string? LanguageCode { get; set; }
    public string? MessageHtml { get; set; }
    public string? MessageCode { get; set; }
}

[Table("SessionCache")]
public class SessionCache
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public byte[] Value { get; set; } = Array.Empty<byte>();
    public DateTimeOffset ExpiresAtTime { get; set; }
    public long? SlidingExpirationInSeconds { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}