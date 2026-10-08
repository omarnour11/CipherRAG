using System.Text.RegularExpressions;

namespace CipherRAG.Application.Helpers;

/// <summary>
/// HIPAA §164.514(b) — De-identification: masks email addresses in audit output.
/// Converts omar@gmail.com → o***@gmail.com
/// </summary>
public static partial class EmailMasker
{
    private static readonly Regex EmailRegex = MyEmailRegex();

    /// <summary>
    /// Masks an email address. Returns "[INVALID_EMAIL]" if the format is not valid.
    /// Safe against null, empty, missing @, leading @, or multiple @ characters.
    /// </summary>
    public static string Mask(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "[EMPTY_EMAIL]";

        if (!EmailRegex.IsMatch(email))
            return "[INVALID_EMAIL]";

        var atIndex = email.IndexOf('@');
        var local   = email[..atIndex];
        var domain  = email[atIndex..];     // includes the @

        var maskedLocal = local.Length == 1
            ? "*"
            : $"{local[0]}{new string('*', Math.Min(local.Length - 1, 3))}";

        return $"{maskedLocal}{domain}";
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex MyEmailRegex();
}