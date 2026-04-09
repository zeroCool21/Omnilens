namespace OmnilensScraping.Models;

public sealed class CreateCompanyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? VatCode { get; set; }
}

public sealed class CompanyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? VatCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public IReadOnlyCollection<CompanyMemberResponse> Members { get; set; } = Array.Empty<CompanyMemberResponse>();
    public IReadOnlyCollection<CompanyInviteResponse> PendingInvites { get; set; } = Array.Empty<CompanyInviteResponse>();
}

public sealed class CompanyMemberResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class CompanyInviteResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string? PreviewToken { get; set; }
}

public sealed class CreateCompanyInviteRequest
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
}

public sealed class AcceptCompanyInviteRequest
{
    public string Token { get; set; } = string.Empty;
}

public sealed class UpdateCompanyMemberRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
