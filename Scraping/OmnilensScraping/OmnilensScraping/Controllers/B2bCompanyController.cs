using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[Authorize]
[ApiController]
[Route("api/b2b/company")]
public sealed class B2bCompanyController : ControllerBase
{
    private static readonly HashSet<string> AllowedMemberRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Owner",
        "Admin",
        "Member",
        "Analyst",
        "Viewer"
    };

    private readonly OmnilensDbContext _dbContext;
    private readonly LocalOpaqueTokenService _opaqueTokenService;

    public B2bCompanyController(OmnilensDbContext dbContext, LocalOpaqueTokenService opaqueTokenService)
    {
        _dbContext = dbContext;
        _opaqueTokenService = opaqueTokenService;
    }

    [Authorize(Roles = "B2B,ADMIN")]
    [HttpGet("me")]
    public async Task<ActionResult<CompanyResponse>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var membership = await LoadMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return NotFound("Nessuna azienda associata all'utente corrente.");
        }

        return Ok(MapCompany(membership.Company));
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Il nome azienda e obbligatorio.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId.Value && item.IsActive, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var alreadyMember = await _dbContext.CompanyMembers
            .AnyAsync(item => item.UserId == userId.Value, cancellationToken);

        if (alreadyMember)
        {
            return Conflict("L'utente corrente appartiene gia a un'azienda.");
        }

        var company = new Company
        {
            Name = request.Name.Trim(),
            VatCode = string.IsNullOrWhiteSpace(request.VatCode) ? null : request.VatCode.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Companies.Add(company);
        _dbContext.CompanyMembers.Add(new CompanyMember
        {
            Company = company,
            UserId = userId.Value,
            Role = "Owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await PromoteUserToBusinessAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Companies
            .AsNoTracking()
            .Include(item => item.Members)
                .ThenInclude(item => item.User)
            .Include(item => item.Invites)
            .SingleAsync(item => item.Id == company.Id, cancellationToken);

        return Ok(MapCompany(created));
    }

    [Authorize(Roles = "B2B,ADMIN")]
    [HttpPost("invites")]
    public async Task<ActionResult<CompanyInviteResponse>> CreateInvite(
        [FromBody] CreateCompanyInviteRequest request,
        CancellationToken cancellationToken)
    {
        var managerContext = await RequireManagerAsync(cancellationToken);
        if (managerContext.Result is not null)
        {
            return managerContext.Result;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email obbligatoria.");
        }

        var role = NormalizeMemberRole(request.Role);
        if (!AllowedMemberRoles.Contains(role))
        {
            return BadRequest("Ruolo non valido.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var company = managerContext.Company!;

        var activeMembers = company.Members.Count;
        var pendingInvites = company.Invites.Count(item => item.Status == "Pending" && item.RevokedAtUtc == null);
        var teamLimit = await GetTeamMemberLimitAsync(managerContext.UserId!.Value, cancellationToken);

        if (activeMembers + pendingInvites >= teamLimit)
        {
            return Conflict($"Limite membri/inviti raggiunto per il piano attivo. Limite corrente: {teamLimit}.");
        }

        if (company.Members.Any(item => item.User.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Utente gia membro dell'azienda.");
        }

        if (company.Invites.Any(item =>
                item.Status == "Pending" &&
                item.RevokedAtUtc == null &&
                item.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("Esiste gia un invito pendente per questa email.");
        }

        var rawToken = _opaqueTokenService.GenerateToken();
        var invite = new CompanyInvite
        {
            CompanyId = company.Id,
            Email = normalizedEmail,
            Role = role,
            TokenHash = _opaqueTokenService.HashToken(rawToken),
            Status = "Pending",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        _dbContext.CompanyInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CompanyInviteResponse
        {
            Id = invite.Id,
            Email = invite.Email,
            Role = invite.Role,
            Status = invite.Status,
            CreatedAtUtc = invite.CreatedAtUtc,
            ExpiresAtUtc = invite.ExpiresAtUtc,
            PreviewToken = rawToken
        });
    }

    [Authorize]
    [HttpPost("invites/accept")]
    public async Task<ActionResult<CompanyResponse>> AcceptInvite(
        [FromBody] AcceptCompanyInviteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Token invito obbligatorio.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId.Value && item.IsActive, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        var hashedToken = _opaqueTokenService.HashToken(request.Token);
        var invite = await _dbContext.CompanyInvites
            .Include(item => item.Company)
                .ThenInclude(item => item.Members)
                    .ThenInclude(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == hashedToken, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (invite is null ||
            invite.Status != "Pending" ||
            invite.RevokedAtUtc.HasValue ||
            invite.ExpiresAtUtc <= now)
        {
            return Unauthorized("Invito non valido.");
        }

        if (!invite.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var existingMembership = await _dbContext.CompanyMembers
            .AnyAsync(item => item.UserId == user.Id, cancellationToken);

        if (existingMembership)
        {
            return Conflict("L'utente appartiene gia a un'azienda.");
        }

        invite.Status = "Accepted";
        invite.AcceptedAtUtc = now;
        invite.AcceptedByUserId = user.Id;

        await PromoteUserToBusinessAsync(user, cancellationToken);
        _dbContext.CompanyMembers.Add(new CompanyMember
        {
            CompanyId = invite.CompanyId,
            UserId = user.Id,
            Role = invite.Role,
            CreatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var company = await _dbContext.Companies
            .AsNoTracking()
            .Include(item => item.Members)
                .ThenInclude(item => item.User)
            .Include(item => item.Invites)
            .SingleAsync(item => item.Id == invite.CompanyId, cancellationToken);

        return Ok(MapCompany(company));
    }

    [Authorize(Roles = "B2B,ADMIN")]
    [HttpPatch("members/{memberId:guid}")]
    public async Task<ActionResult<CompanyResponse>> UpdateMemberRole(
        Guid memberId,
        [FromBody] UpdateCompanyMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var managerContext = await RequireManagerAsync(cancellationToken);
        if (managerContext.Result is not null)
        {
            return managerContext.Result;
        }

        var newRole = NormalizeMemberRole(request.Role);
        if (!AllowedMemberRoles.Contains(newRole))
        {
            return BadRequest("Ruolo non valido.");
        }

        var company = managerContext.Company!;
        var member = company.Members.SingleOrDefault(item => item.Id == memberId);
        if (member is null)
        {
            return NotFound();
        }

        if (member.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
            !newRole.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
            company.Members.Count(item => item.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase)) == 1)
        {
            return Conflict("Deve rimanere almeno un owner nell'azienda.");
        }

        member.Role = newRole;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(MapCompany(company));
    }

    [Authorize(Roles = "B2B,ADMIN")]
    [HttpDelete("members/{memberId:guid}")]
    public async Task<ActionResult> RemoveMember(Guid memberId, CancellationToken cancellationToken)
    {
        var managerContext = await RequireManagerAsync(cancellationToken);
        if (managerContext.Result is not null)
        {
            return managerContext.Result;
        }

        var company = managerContext.Company!;
        var member = company.Members.SingleOrDefault(item => item.Id == memberId);
        if (member is null)
        {
            return NotFound();
        }

        if (member.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
            company.Members.Count(item => item.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase)) == 1)
        {
            return Conflict("Non puoi rimuovere l'ultimo owner dell'azienda.");
        }

        _dbContext.CompanyMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<(ActionResult? Result, Company? Company, Guid? UserId)> RequireManagerAsync(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return (Unauthorized(), null, null);
        }

        var membership = await LoadMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return (NotFound("Nessuna azienda associata all'utente corrente."), null, userId);
        }

        if (!membership.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
            !membership.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return (Forbid(), null, userId);
        }

        return (null, membership.Company, userId);
    }

    private async Task<CompanyMember?> LoadMembershipAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.CompanyMembers
            .Include(item => item.Company)
                .ThenInclude(item => item.Members)
                    .ThenInclude(item => item.User)
            .Include(item => item.Company)
                .ThenInclude(item => item.Invites)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
    }

    private async Task<int> GetTeamMemberLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("ADMIN"))
        {
            return int.MaxValue;
        }

        var subscription = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(item => item.Plan)
                .ThenInclude(item => item.PlanEntitlements)
                    .ThenInclude(item => item.Entitlement)
            .Where(item => item.UserId == userId && item.Status == "Active")
            .ToListAsync(cancellationToken);

        var activeSubscription = subscription
            .Where(item => item.Plan.Audience == "B2B")
            .OrderByDescending(item => item.StartedAtUtc)
            .FirstOrDefault();
        var teamEntitlement = activeSubscription?.Plan.PlanEntitlements
            .FirstOrDefault(item => item.Entitlement.Code == "TEAM_MEMBERS");

        return teamEntitlement?.NumericValue is > 0m and <= int.MaxValue
            ? (int)teamEntitlement.NumericValue.Value
            : 1;
    }

    private async Task EnsureBusinessSubscriptionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var hasActiveBusinessSubscription = await _dbContext.UserSubscriptions
            .Include(item => item.Plan)
            .AnyAsync(item => item.UserId == userId && item.Status == "Active" && item.Plan.Audience == "B2B", cancellationToken);

        if (hasActiveBusinessSubscription)
        {
            return;
        }

        var plan = await _dbContext.Plans.SingleAsync(item => item.Code == "B2B_STARTER", cancellationToken);
        _dbContext.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = "Active",
            AutoRenew = true,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private async Task PromoteUserToBusinessAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (!user.UserType.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            user.UserType = "B2B";
        }

        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var b2bRole = await _dbContext.Roles.SingleAsync(item => item.Code == "B2B", cancellationToken);
        var existingB2bRole = await _dbContext.UserRoles
            .AnyAsync(item => item.UserId == user.Id && item.RoleId == b2bRole.Id, cancellationToken);

        if (!existingB2bRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = b2bRole.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var b2cRoleIds = await _dbContext.Roles
            .Where(item => item.Code == "B2C")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var removableRoles = await _dbContext.UserRoles
            .Where(item => item.UserId == user.Id && b2cRoleIds.Contains(item.RoleId))
            .ToListAsync(cancellationToken);

        if (removableRoles.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(removableRoles);
        }

        await EnsureBusinessSubscriptionAsync(user.Id, cancellationToken);
    }

    private static string NormalizeMemberRole(string? role)
    {
        return role?.Trim() switch
        {
            null or "" => "Member",
            var value when value.Equals("owner", StringComparison.OrdinalIgnoreCase) => "Owner",
            var value when value.Equals("admin", StringComparison.OrdinalIgnoreCase) => "Admin",
            var value when value.Equals("analyst", StringComparison.OrdinalIgnoreCase) => "Analyst",
            var value when value.Equals("viewer", StringComparison.OrdinalIgnoreCase) => "Viewer",
            _ => role!.Trim()
        };
    }

    private static CompanyResponse MapCompany(Company company)
    {
        return new CompanyResponse
        {
            Id = company.Id,
            Name = company.Name,
            VatCode = company.VatCode,
            CreatedAtUtc = company.CreatedAtUtc,
            Members = company.Members
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new CompanyMemberResponse
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    Email = item.User.Email,
                    DisplayName = item.User.DisplayName,
                    Role = item.Role,
                    CreatedAtUtc = item.CreatedAtUtc
                })
                .ToArray(),
            PendingInvites = company.Invites
                .Where(item => item.Status == "Pending" && item.RevokedAtUtc == null)
                .OrderByDescending(item => item.CreatedAtUtc)
                .Select(item => new CompanyInviteResponse
                {
                    Id = item.Id,
                    Email = item.Email,
                    Role = item.Role,
                    Status = item.Status,
                    CreatedAtUtc = item.CreatedAtUtc,
                    ExpiresAtUtc = item.ExpiresAtUtc
                })
                .ToArray()
        };
    }
}
