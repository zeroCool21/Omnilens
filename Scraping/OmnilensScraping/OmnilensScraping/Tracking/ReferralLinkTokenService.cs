using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OmnilensScraping.Auth;

namespace OmnilensScraping.Tracking;

public sealed class ReferralLinkTokenService
{
    private readonly byte[] _secretKey;
    private readonly ReferralOptions _options;

    public ReferralLinkTokenService(IOptions<AuthOptions> authOptions, IOptions<ReferralOptions> referralOptions)
    {
        _secretKey = Encoding.UTF8.GetBytes(authOptions.Value.SigningKey);
        _options = referralOptions.Value;
    }

    public ReferralToken Create(Guid offerId, string? utmSource, string? utmCampaign, DateTimeOffset now)
    {
        var payload = new ReferralTokenPayload
        {
            Version = 1,
            OfferId = offerId,
            UtmSource = NormalizeNullable(utmSource),
            UtmCampaign = NormalizeNullable(utmCampaign),
            IssuedAtUnixSeconds = now.ToUnixTimeSeconds()
        };

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadEncoded = Base64UrlEncoder.Encode(payloadBytes);
        var signatureBytes = ComputeSignature(payloadBytes);
        var signatureEncoded = Base64UrlEncoder.Encode(signatureBytes);

        var token = $"{payloadEncoded}.{signatureEncoded}";
        var expiresAtUtc = now.AddDays(Math.Clamp(_options.TokenExpirationDays, 1, 3650));

        return new ReferralToken
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public bool TryParse(string token, DateTimeOffset now, out ReferralTokenPayload payload, out DateTimeOffset expiresAtUtc)
    {
        payload = new ReferralTokenPayload();
        expiresAtUtc = DateTimeOffset.MinValue;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signatureBytes;

        try
        {
            payloadBytes = Base64UrlEncoder.DecodeBytes(parts[0]);
            signatureBytes = Base64UrlEncoder.DecodeBytes(parts[1]);
        }
        catch
        {
            return false;
        }

        var expectedSignature = ComputeSignature(payloadBytes);
        if (signatureBytes.Length != expectedSignature.Length ||
            !CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
        {
            return false;
        }

        ReferralTokenPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ReferralTokenPayload>(payloadBytes);
        }
        catch
        {
            return false;
        }

        if (parsed is null || parsed.Version != 1 || parsed.OfferId == Guid.Empty)
        {
            return false;
        }

        var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(parsed.IssuedAtUnixSeconds);
        expiresAtUtc = issuedAtUtc.AddDays(Math.Clamp(_options.TokenExpirationDays, 1, 3650));
        if (now > expiresAtUtc)
        {
            return false;
        }

        payload = parsed;
        return true;
    }

    private byte[] ComputeSignature(byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(payloadBytes);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class ReferralToken
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class ReferralTokenPayload
{
    public int Version { get; set; }
    public Guid OfferId { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmCampaign { get; set; }
    public long IssuedAtUnixSeconds { get; set; }
}

