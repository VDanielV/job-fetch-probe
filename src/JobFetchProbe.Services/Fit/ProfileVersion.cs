using System.Security.Cryptography;
using System.Text;

namespace JobFetchProbe.Services.Fit;

/// <summary>
/// A short fingerprint of the three Fit files: the first 12 hex characters of a SHA-256 over them. Stored with each
/// assessment so scores made with an older profile, policy or rubric can be recognised; the files' text is never stored.
/// </summary>
internal static class ProfileVersion
{
    public static string Of(FitPack pack)
    {
        var text = pack.Profile + "\n\u0000\n" + pack.Policy + "\n\u0000\n" + pack.Rubric;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(hash)[..12];
    }
}
