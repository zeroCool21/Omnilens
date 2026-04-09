using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmnilensScraping.Auth;
using OmnilensScraping.Models;
using OmnilensScraping.Persistence;
using OmnilensScraping.Persistence.Entities;

namespace OmnilensScraping.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private static readonly HashSet<string> AllowedUserTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "B2C",
        "B2B",
        "ADMIN",
        "PARTNER"
    };

    private readonly OmnilensDbContext _dbContext;
    private readonly LocalPasswordHasher _passwordHasher;
    private readonly LocalOpaqueTokenService _opaqueTokenService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthOptions _authOptions;

    public AuthController(
        OmnilensDbContext dbContext,
        LocalPasswordHasher passwordHasher,
        LocalOpaqueTokenService opaqueTokenService,
        JwtTokenService jwtTokenService,
        IOptions<AuthOptions> authOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _opaqueTokenService = opaqueTokenService;
        _jwtTokenService = jwtTokenService;
        _authOptions = authOptions.Value;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest("Email, password e displayName sono obbligatori.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(item => item.Email == normalizedEmail, cancellationToken))
        {
            return Conflict("Esiste gia un utente con questa email.");
        }

        var userType = NormalizeUserType(request.UserType);

        var user = new AppUser
        {
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            DisplayName = request.DisplayName.Trim(),
            UserType = userType,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureUserDefaultsAsync(user, cancellationToken);
        var response = await BuildAuthResponseAsync(user.Id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email e password sono obbligatorie.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Credenziali non valide.");
        }

        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureUserDefaultsAsync(user, cancellationToken);
        var response = await BuildAuthResponseAsync(user.Id, cancellationToken);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token obbligatorio.");
        }

        var hashedToken = _opaqueTokenService.HashToken(request.RefreshToken);
        var now = DateTimeOffset.UtcNow;
        var session = await _dbContext.UserRefreshTokens
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == hashedToken, cancellationToken);

        if (session is null ||
            session.RevokedAtUtc.HasValue ||
            session.ExpiresAtUtc <= now ||
            !session.User.IsActive)
        {
            return Unauthorized("Refresh token non valido.");
        }

        session.RevokedAtUtc = now;
        session.LastUsedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureUserDefaultsAsync(session.User, cancellationToken);
        var response = await BuildAuthResponseAsync(session.UserId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<ActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return NoContent();
        }

        var hashedToken = _opaqueTokenService.HashToken(request.RefreshToken);
        var session = await _dbContext.UserRefreshTokens
            .SingleOrDefaultAsync(item => item.TokenHash == hashedToken, cancellationToken);

        if (session is not null && !session.RevokedAtUtc.HasValue)
        {
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
            session.LastUsedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);

        if (user is null)
        {
            return Ok(new ForgotPasswordResponse
            {
                Accepted = true,
                Message = "Se l'account esiste, il reset e stato predisposto."
            });
        }

        var rawToken = _opaqueTokenService.GenerateToken();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _opaqueTokenService.HashToken(rawToken),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_authOptions.PasswordResetTokenExpirationMinutes)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ForgotPasswordResponse
        {
            Accepted = true,
            Message = "Reset password generato in locale.",
            PreviewResetToken = rawToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_authOptions.PasswordResetTokenExpirationMinutes)
        });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("Email, token e nuova password sono obbligatori.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);

        if (user is null)
        {
            return Unauthorized("Reset token non valido.");
        }

        var hashedToken = _opaqueTokenService.HashToken(request.Token);
        var now = DateTimeOffset.UtcNow;
        var resetToken = await _dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(
                item => item.UserId == user.Id &&
                        item.TokenHash == hashedToken &&
                        item.UsedAtUtc == null,
                cancellationToken);

        if (resetToken is null || resetToken.ExpiresAtUtc <= now)
        {
            return Unauthorized("Reset token non valido.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = now;
        resetToken.UsedAtUtc = now;

        var activeSessions = await _dbContext.UserRefreshTokens
            .Where(item => item.UserId == user.Id && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureUserDefaultsAsync(user, cancellationToken);
        var response = await BuildAuthResponseAsync(user.Id, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await LoadUserForAuthAsync(userId.Value, cancellationToken);
        return user is null ? Unauthorized() : Ok(ToUserResponse(user));
    }

    [Authorize]
    [HttpGet("me/profile")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await LoadUserForAuthAsync(userId.Value, cancellationToken);
        return user is null ? Unauthorized() : Ok(ToProfileResponse(user));
    }

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<ActionResult<UserProfileResponse>> UpdateProfile(
        [FromBody] UpdateUserProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .Include(item => item.ProfileSettings)
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                    .ThenInclude(item => item.RolePermissions)
                        .ThenInclude(item => item.Permission)
            .SingleOrDefaultAsync(item => item.Id == userId.Value, cancellationToken);

        if (user is null)
        {
            return Unauthorized();
        }

        await EnsureUserDefaultsAsync(user, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
        }

        var profile = user.ProfileSettings!;
        if (!string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            profile.LanguageCode = request.LanguageCode.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
        {
            profile.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
            user.CountryCode = profile.CountryCode;
        }

        if (request.SectorCode is not null)
        {
            profile.SectorCode = string.IsNullOrWhiteSpace(request.SectorCode) ? null : request.SectorCode.Trim();
        }

        if (request.NotificationEmailEnabled.HasValue)
        {
            profile.NotificationEmailEnabled = request.NotificationEmailEnabled.Value;
        }

        if (request.NotificationPushEnabled.HasValue)
        {
            profile.NotificationPushEnabled = request.NotificationPushEnabled.Value;
        }

        if (request.PrivacyConsentAccepted.HasValue)
        {
            profile.PrivacyConsentAccepted = request.PrivacyConsentAccepted.Value;
        }

        if (request.MarketingConsentAccepted.HasValue)
        {
            profile.MarketingConsentAccepted = request.MarketingConsentAccepted.Value;
        }

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        user.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToProfileResponse(user));
    }

    private async Task EnsureUserDefaultsAsync(AppUser user, CancellationToken cancellationToken)
    {
        var normalizedUserType = NormalizeUserType(user.UserType);
        user.UserType = normalizedUserType;

        var role = await _dbContext.Roles
            .SingleAsync(item => item.Code == normalizedUserType, cancellationToken);

        var hasRole = await _dbContext.UserRoles
            .AnyAsync(item => item.UserId == user.Id && item.RoleId == role.Id, cancellationToken);

        if (!hasRole)
        {
            _dbContext.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        var profile = await _dbContext.UserProfileSettings
            .SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            _dbContext.UserProfileSettings.Add(new UserProfileSettings
            {
                UserId = user.Id,
                CountryCode = user.CountryCode ?? "IT",
                LanguageCode = "it",
                PrivacyConsentAccepted = true,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var hasSubscription = await _dbContext.UserSubscriptions
            .AnyAsync(item => item.UserId == user.Id && item.Status == "Active", cancellationToken);

        if (!hasSubscription)
        {
            var planCode = normalizedUserType switch
            {
                "B2B" => "B2B_STARTER",
                "B2C" => "B2C_FREE",
                _ => null
            };

            if (planCode is not null)
            {
                var plan = await _dbContext.Plans
                    .SingleAsync(item => item.Code == planCode, cancellationToken);

                _dbContext.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = user.Id,
                    PlanId = plan.Id,
                    Status = "Active",
                    AutoRenew = true,
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadUserForAuthAsync(userId, cancellationToken)
                   ?? throw new InvalidOperationException("Utente non trovato durante la generazione del token.");

        var rawRefreshToken = _opaqueTokenService.GenerateToken();
        var refreshExpiry = DateTimeOffset.UtcNow.AddDays(_authOptions.RefreshTokenExpirationDays);

        _dbContext.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = user.Id,
            TokenHash = _opaqueTokenService.HashToken(rawRefreshToken),
            ExpiresAtUtc = refreshExpiry,
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = user.UserRoles
            .Select(item => item.Role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = user.UserRoles
            .SelectMany(item => item.Role.RolePermissions)
            .Select(item => item.Permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToArray();

        return new AuthResponse
        {
            AccessToken = _jwtTokenService.CreateToken(user, roles, permissions),
            RefreshToken = rawRefreshToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_authOptions.TokenExpirationMinutes),
            User = ToUserResponse(user)
        };
    }

    private async Task<AppUser?> LoadUserForAuthAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Include(item => item.UserRoles)
                .ThenInclude(item => item.Role)
                    .ThenInclude(item => item.RolePermissions)
                        .ThenInclude(item => item.Permission)
            .Include(item => item.ProfileSettings)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
    }

    private static AuthUserResponse ToUserResponse(AppUser user)
    {
        return new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            UserType = user.UserType,
            Roles = user.UserRoles.Select(item => item.Role.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToArray(),
            Permissions = user.UserRoles
                .SelectMany(item => item.Role.RolePermissions)
                .Select(item => item.Permission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray()
        };
    }

    private static UserProfileResponse ToProfileResponse(AppUser user)
    {
        var profile = user.ProfileSettings;
        return new UserProfileResponse
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            UserType = user.UserType,
            LanguageCode = profile?.LanguageCode ?? "it",
            CountryCode = profile?.CountryCode ?? user.CountryCode ?? "IT",
            SectorCode = profile?.SectorCode,
            NotificationEmailEnabled = profile?.NotificationEmailEnabled ?? true,
            NotificationPushEnabled = profile?.NotificationPushEnabled ?? false,
            PrivacyConsentAccepted = profile?.PrivacyConsentAccepted ?? false,
            MarketingConsentAccepted = profile?.MarketingConsentAccepted ?? false,
            Roles = user.UserRoles.Select(item => item.Role.Code).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToArray(),
            Permissions = user.UserRoles
                .SelectMany(item => item.Role.RolePermissions)
                .Select(item => item.Permission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item)
                .ToArray()
        };
    }

    private static string NormalizeUserType(string? userType)
    {
        return AllowedUserTypes.Contains(userType ?? string.Empty)
            ? userType!.Trim().ToUpperInvariant()
            : "B2C";
    }
}
