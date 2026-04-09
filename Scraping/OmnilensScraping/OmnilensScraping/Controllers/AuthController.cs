using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        "B2B"
    };

    private readonly OmnilensDbContext _dbContext;
    private readonly LocalPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        OmnilensDbContext dbContext,
        LocalPasswordHasher passwordHasher,
        JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
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

        var userType = AllowedUserTypes.Contains(request.UserType)
            ? request.UserType.ToUpperInvariant()
            : "B2C";

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

        return Ok(BuildAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.IsActive, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Credenziali non valide.");
        }

        user.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(BuildAuthResponse(user));
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

        var user = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == userId.Value, cancellationToken);

        return user is null
            ? Unauthorized()
            : Ok(ToUserResponse(user));
    }

    private AuthResponse BuildAuthResponse(AppUser user)
    {
        return new AuthResponse
        {
            AccessToken = _jwtTokenService.CreateToken(user),
            User = ToUserResponse(user)
        };
    }

    private static AuthUserResponse ToUserResponse(AppUser user)
    {
        return new AuthUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            UserType = user.UserType
        };
    }
}
