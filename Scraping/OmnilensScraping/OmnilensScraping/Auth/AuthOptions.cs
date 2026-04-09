namespace OmnilensScraping.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "OmniLens.Local";
    public string Audience { get; set; } = "OmniLens.Local.Client";
    public string SigningKey { get; set; } = "CHANGE_ME_LOCAL_ONLY_SUPER_LONG_SIGNING_KEY_123456789";
    public int TokenExpirationMinutes { get; set; } = 480;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int PasswordResetTokenExpirationMinutes { get; set; } = 30;
}
