using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CipherRAG.Application.Interfaces;
using CipherRAG.Domain.Enums;
using CipherRAG.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CipherRAG.Infrastructure.Services;

/// <summary>
/// Professional, dynamic, context-aware PII masking pipeline.
/// Stage 1: Microsoft Presidio AI Service via Docker (PERSON, LOCATION, EMAIL, CREDIT_CARD, IBAN, DATE_TIME, IP, SSN)
/// Stage 2: Local Regex NER (Egyptian phones, National IDs, IBAN, Credit Cards, Emails, Dates, English person/location names)
/// Stage 3: Post-processing deduplication, overlap resolution and sequential re-indexing.
/// </summary>
public class PresidioService : IPresidioService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PresidioService> _logger;

    // ── English stop-words that can never be a person name ─────────────────
    private static readonly HashSet<string> EnglishStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","in","on","at","to","for","of","with","by","from",
        "get","fetch","find","show","retrieve","query","data","info","information","details","his","her","their",
        "file","record","report","patient","doctor","nurse","staff","user","person","client","individual","someone","this","that",
        "phone","mobile","number","num","tel","contact","address","location","city","street","area",
        "lives","living","residing","resides","stay","staying",
        "mr","mrs","ms","dr","prof","sir","madam",
        // Greeting & self-intro words (never names)
        "hi","hello","hey","greetings","howdy",
        "im","am","is","my","me","give","please",
        // Common CV/document words
        "cv","resume","profile","about","summary",
    };

    // ── Compiled regexes (static/readonly = compiled once at class load) ────

    // Egyptian mobile: 010/011/012/015 followed by 8 digits
    private static readonly Regex EgyptianPhoneRegex = new(
        @"(?<!\d)01[0125]\d{8}(?!\d)",
        RegexOptions.Compiled);

    // International format phone (+20 10..., +1-555-..., etc.) – min 7 digits total
    private static readonly Regex InternationalPhoneRegex = new(
        @"\+?\d{1,3}[\s\-\.]?\(?\d{1,4}\)?[\s\-\.]?\d{3,4}[\s\-\.]?\d{3,5}",
        RegexOptions.Compiled);

    // Egyptian National ID: exactly 14 digits starting with 2 or 3
    private static readonly Regex EgyptianNidRegex = new(
        @"(?<!\d)[23]\d{13}(?!\d)",
        RegexOptions.Compiled);

    // IPv4
    private static readonly Regex IpRegex = new(
        @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b",
        RegexOptions.Compiled);

    // Email
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);

    // Credit card (13-19 digits, optional spaces/dashes between groups)
    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d[ \-]?){13,18}\d\b",
        RegexOptions.Compiled);

    // Egyptian IBAN: EG followed by 27 digits
    private static readonly Regex EgyptianIbanRegex = new(
        @"\bEG\d{27}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Date patterns: DD/MM/YYYY, DD-MM-YYYY, YYYY-MM-DD, DD.MM.YYYY
    private static readonly Regex DateRegex = new(
        @"\b(?:(?:0?[1-9]|[12]\d|3[01])[/\-\.](?:0?[1-9]|1[0-2])[/\-\.](?:19|20)\d{2}|(?:19|20)\d{2}[/\-\.](?:0?[1-9]|1[0-2])[/\-\.](?:0?[1-9]|[12]\d|3[01]))\b",
        RegexOptions.Compiled);

    // English context-window NER: captures names after titles, role words, or self-intro phrases
    // Examples: "patient John", "dr. Smith", "my name is Omar", "info about Omar Nour"
    private static readonly Regex EnglishPersonContextRegex = new(
        @"(?:patient|mr\.?|mrs\.?|ms\.?|dr\.?|prof\.?|named?|name\s+is|called|user|client|about|info\s+about|information\s+about|give\s+me|i(?:'m|\s+am)|my\s+name\s+is|this\s+is|it(?:'s|\s+is)|im)\s+(?<name>[a-zA-Z]{2,}\.?(?:\s+[a-zA-Z\-]{2,}\.?){0,2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // English Location context: "lives in X", "located in X", "based in X"
    private static readonly Regex EnglishLocationContextRegex = new(
        @"(?:lives?\s+in|residing?\s+in|located\s+in|resides?\s+in|city\s+of|address\s+is|based\s+in)\s+(?<loc>[a-zA-Z][a-zA-Z\s]{2,20}?)(?=\s+(?:and|with|whose|number|phone)\b|[,.]|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Mask placeholder pattern (to skip already-masked spans)
    private static readonly Regex AlreadyMaskedRegex = new(
        @"\[MASKED_[A-Z_]+_\d+\]",
        RegexOptions.Compiled);

    public PresidioService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PresidioService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(string MaskedPrompt, List<MaskedToken> Tokens)> AnalyzeAndMaskAsync(
        string rawPrompt,
        Guid sessionId,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawPrompt))
            return (string.Empty, []);

        var allSpans = new List<DetectedSpan>();

        // ── STAGE 1: Local Regex NER ───────────────────────────────────────
        // Common PII is deterministic and can be masked without a network
        // round-trip. Presidio remains the fallback for text requiring NER.
        var localSpans = RunLocalNerPass(rawPrompt);
        allSpans.AddRange(localSpans);

        // ── STAGE 2: Microsoft Presidio Docker ────────────────────────────
        if (localSpans.Count == 0)
        {
            string presidioUrl = _configuration["Presidio:ApiUrl"] ?? "http://localhost:5001";
            try
            {
                var presidioSpans = await RunPresidioAnalyzeAsync(rawPrompt, presidioUrl, ct);
                allSpans.AddRange(presidioSpans);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[PII Security Gateway] Presidio at {Url} unavailable. Continuing with local NER.", presidioUrl);
            }
        }

        // ── STAGE 3: Post-processing ───────────────────────────────────────
        var dedupedSpans = DeduplicateSpans(allSpans);
        dedupedSpans.Sort((a, b) => b.Start.CompareTo(a.Start));

        var sb = new StringBuilder(rawPrompt);
        var tokens = new List<MaskedToken>();
        int counter = 1;

        foreach (var span in dedupedSpans)
        {
            if (span.Start < 0 || span.End > sb.Length || span.Start >= span.End)
                continue;

            string realValue = sb.ToString(span.Start, span.End - span.Start);

            if (AlreadyMaskedRegex.IsMatch(realValue))
                continue;

            string placeholder = $"[MASKED_{span.Label}_{counter++}]";
            tokens.Add(new MaskedToken(placeholder, realValue, span.Category, sessionId, expiresAt));
            sb.Remove(span.Start, span.End - span.Start);
            sb.Insert(span.Start, placeholder);
        }

        return (sb.ToString(), tokens);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STAGE 1 – Microsoft Presidio Docker
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<List<DetectedSpan>> RunPresidioAnalyzeAsync(
        string text, string presidioUrl, CancellationToken ct)
    {
        var payload = new
        {
            text,
            language = "en",
            entities = new[]
            {
                "PERSON", "EMAIL_ADDRESS", "PHONE_NUMBER", "LOCATION",
                "CREDIT_CARD", "IBAN_CODE", "DATE_TIME", "IP_ADDRESS", "US_SSN", "MEDICAL_LICENSE"
            },
            score_threshold = 0.35
        };

        var endpoint = $"{presidioUrl.TrimEnd('/')}/analyze";
        var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Presidio returned HTTP {Code}", response.StatusCode);
            return [];
        }

        var results = await response.Content
            .ReadFromJsonAsync<List<PresidioEntityResult>>(ct) ?? [];

        var spans = new List<DetectedSpan>();
        foreach (var r in results)
        {
            if (r.Start < 0 || r.End <= r.Start || r.End > text.Length)
                continue;

            string rawValue = text.Substring(r.Start, r.End - r.Start).Trim();
            string entityType = r.Entity_Type?.ToUpperInvariant() ?? "PII";

            // False-positive filter
            if (entityType == "PERSON" && IsStopWord(rawValue))
                continue;

            // Minimum quality gate
            if (rawValue.Length < 2 || rawValue.All(c => !char.IsLetterOrDigit(c)))
                continue;

            string label = entityType switch
            {
                "GPE" or "LOC" or "CITY" => "LOCATION",
                _ => entityType
            };

            // Guard: prevent 14-digit Egyptian National IDs from matching CREDIT_CARD
            if (label == "CREDIT_CARD")
            {
                string digits = Regex.Replace(rawValue, @"[\s\-]", "");
                if (digits.Length == 14 && (digits.StartsWith("2") || digits.StartsWith("3")))
                    continue;
            }

            EntityType category = entityType switch
            {
                "CREDIT_CARD" or "IBAN_CODE" or "CRYPTO" => EntityType.PCI,
                "MEDICAL_LICENSE" or "US_PASSPORT"        => EntityType.PHI,
                _                                         => EntityType.PII
            };

            spans.Add(new DetectedSpan(r.Start, r.End, label, category, r.Score, Source.Presidio));
        }

        return spans;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STAGE 2 – Local Regex NER (runs always, even without Presidio)
    // ═══════════════════════════════════════════════════════════════════════

    private static List<DetectedSpan> RunLocalNerPass(string text)
    {
        var spans = new List<DetectedSpan>();

        // 1. Email (highest priority structural pattern)
        AddRegexSpans(text, EmailRegex, "EMAIL_ADDRESS", EntityType.PII, 1.0, spans);

        // 2. Egyptian mobile (structural: starts 010/011/012/015 + 8 digits)
        AddRegexSpans(text, EgyptianPhoneRegex, "PHONE_NUMBER", EntityType.PII, 0.95, spans);

        // 3. International phone
        AddRegexSpans(text, InternationalPhoneRegex, "PHONE_NUMBER", EntityType.PII, 0.75, spans,
            raw =>
            {
                string digits = Regex.Replace(raw, @"[\s\-\+\(\)\.]", "");
                if (digits.Length == 14 && (digits.StartsWith("2") || digits.StartsWith("3")))
                    return false;
                return digits.Length >= 9 && digits.Any(char.IsDigit) && !EgyptianPhoneRegex.IsMatch(raw);
            });

        // 4. Egyptian National ID (14 digits starting with 2 or 3 + valid birth date & gov)
        AddRegexSpans(text, EgyptianNidRegex, "NATIONAL_ID", EntityType.PII, 0.95, spans, IsValidEgyptianNid);

        // 5. Credit card (with Luhn check)
        AddRegexSpans(text, CreditCardRegex, "CREDIT_CARD", EntityType.PCI, 0.9, spans,
            raw =>
            {
                string digits = Regex.Replace(raw, @"[\s\-]", "");
                if (digits.Length == 14 && (digits.StartsWith("2") || digits.StartsWith("3")))
                    return false;
                return digits.Length is >= 13 and <= 19 && LuhnCheck(digits);
            });

        // 6. IP address
        AddRegexSpans(text, IpRegex, "IP_ADDRESS", EntityType.PII, 0.9, spans);

        // 7. Egyptian IBAN
        AddRegexSpans(text, EgyptianIbanRegex, "IBAN_CODE", EntityType.PCI, 0.98, spans);

        // 8. Date/Time patterns
        AddRegexSpans(text, DateRegex, "DATE_TIME", EntityType.PII, 0.9, spans);

        // 9. English person – context window NER
        AddContextNameSpans(text, EnglishPersonContextRegex, "name", "PERSON", spans);

        // 10. English location phrase NER
        AddContextNameSpans(text, EnglishLocationContextRegex, "loc", "LOCATION", spans);

        return spans;
    }

    // ── Helper: add spans for a compiled Regex ──────────────────────────────
    private static void AddRegexSpans(
        string text,
        Regex regex,
        string label,
        EntityType category,
        double score,
        List<DetectedSpan> spans,
        Func<string, bool>? extraValidation = null)
    {
        foreach (Match m in regex.Matches(text))
        {
            string raw = m.Value.Trim();
            if (extraValidation != null && !extraValidation(raw))
                continue;
            if (raw.Length < 2)
                continue;
            spans.Add(new DetectedSpan(m.Index, m.Index + m.Length, label, category, score, Source.Local));
        }
    }

    // ── Helper: add spans using named capture group from context regex ───────
    private static void AddContextNameSpans(
        string text,
        Regex regex,
        string groupName,
        string label,
        List<DetectedSpan> spans)
    {
        foreach (Match m in regex.Matches(text))
        {
            var group = m.Groups[groupName];
            if (!group.Success) continue;

            string captured = group.Value;
            var words = captured.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            int charCount = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (EnglishStopWords.Contains(w.ToLowerInvariant()))
                    break;

                charCount += w.Length;
                if (i < words.Length - 1) charCount++;
            }

            if (charCount < 2) continue;

            int newEnd = group.Index + charCount;
            string finalName = text.Substring(group.Index, charCount).Trim();

            if (!finalName.Any(char.IsLetter)) continue;

            spans.Add(new DetectedSpan(group.Index, newEnd, label, EntityType.PII, 0.85, Source.Local));
        }
    }

    // ── Helper: check if a string is an English stop-word ────────────────
    private static bool IsStopWord(string value)
    {
        string trimmed = value.Trim();
        if (EnglishStopWords.Contains(trimmed)) return true;
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w => EnglishStopWords.Contains(w.ToLowerInvariant()));
    }

    // ── Helper: Validate Egyptian National ID ────────────────────────────
    private static bool IsValidEgyptianNid(string nid)
    {
        if (string.IsNullOrEmpty(nid) || nid.Length != 14) return false;
        if (nid[0] != '2' && nid[0] != '3') return false;
        if (!int.TryParse(nid.Substring(3, 2), out int month) || month < 1 || month > 12) return false;
        if (!int.TryParse(nid.Substring(5, 2), out int day)   || day   < 1 || day   > 31) return false;
        if (!int.TryParse(nid.Substring(7, 2), out int gov)   || gov   < 1 || gov   > 88) return false;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STAGE 3 – Deduplication: resolve overlapping spans
    // ═══════════════════════════════════════════════════════════════════════

    private static List<DetectedSpan> DeduplicateSpans(List<DetectedSpan> spans)
    {
        if (spans.Count == 0) return [];

        var sorted = spans.OrderByDescending(s => s.Score).ThenByDescending(s => s.End - s.Start).ToList();
        var kept = new List<DetectedSpan>();

        foreach (var candidate in sorted)
        {
            bool overlaps = kept.Any(k => candidate.Start < k.End && candidate.End > k.Start);
            if (!overlaps)
                kept.Add(candidate);
        }

        return kept;
    }

    // ── Luhn algorithm for credit card validation ─────────────────────────
    private static bool LuhnCheck(string digits)
    {
        int sum = 0;
        bool alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(digits[i])) return false;
            int n = digits[i] - '0';
            if (alternate) { n *= 2; if (n > 9) n -= 9; }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    // ── Internal record types ─────────────────────────────────────────────
    private record DetectedSpan(int Start, int End, string Label, EntityType Category, double Score, Source Origin);
    private enum Source { Presidio, Local }
}

/// <summary>
/// Maps the official Microsoft Presidio Docker analyzer JSON response.
/// The official image returns snake_case: entity_type, start, end, score.
/// </summary>
public class PresidioEntityResult
{
    [JsonPropertyName("entity_type")]
    public string Entity_Type { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}
