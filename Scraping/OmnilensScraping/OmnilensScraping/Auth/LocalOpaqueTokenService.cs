using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OmnilensScraping.Auth;

public sealed class LocalOpaqueTokenService
{
    public string GenerateToken(int sizeInBytes = 48)
    {
        return Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(sizeInBytes));
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
