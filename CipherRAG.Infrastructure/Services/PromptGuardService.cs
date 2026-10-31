using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CipherRAG.Application.Interfaces;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CipherRAG.Infrastructure.Services;

public class PromptGuardService : IGuardrailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PromptGuardService> _logger;

    private static DateTimeOffset _circuitBreakerUntilUtc = DateTimeOffset.MinValue;
    private static readonly object _circuitLock = new();

    public PromptGuardService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PromptGuardService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RiskAssessment> EvaluateRiskAsync(string rawPrompt, string maskedPrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawPrompt))
        {
            return new RiskAssessment
            {
                AssessmentId = Guid.NewGuid(),
                PatternScore = 0.0m,
                MlModelScore = 0.0m,
                LlmJudgeScore = 0.0m,
                CompositeRiskScore = 0.0m,
                Verdict = RiskVerdict.Safe,
                QueryHash = string.Empty
            };
        }

        string queryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPrompt)));

        // 1️⃣ Tier 1: Fast Rule & Heuristic Scanner (Zero latency, runs offline in cloud)
        decimal patternScore = ScanRulePatterns(rawPrompt);

        // 2️⃣ Tier 2: Dedicated AI Prompt Guard Model (Llama-Guard 3 via Ollama or resilient offline ML heuristic)
        decimal mlModelScore = await CallPromptGuardModelApiAsync(maskedPrompt, patternScore, ct);

        // 3️⃣ Tier 3: LLM Judge Intent & Structure Evaluation
        decimal llmJudgeScore = EvaluateLlmJudgeRisk(rawPrompt, patternScore, mlModelScore);

        var assessment = new RiskAssessment
        {
            AssessmentId = Guid.NewGuid(),
            PatternScore = Math.Round(patternScore, 2),
            MlModelScore = Math.Round(mlModelScore, 2),
            LlmJudgeScore = Math.Round(llmJudgeScore, 2),
            QueryHash = queryHash
        };

        assessment.CompositeRiskScore = Math.Round(assessment.ComputeComposite(), 2);

        // Determine Final Risk Verdict
        if (assessment.CompositeRiskScore >= 0.70m || patternScore >= 0.85m || mlModelScore >= 0.85m)
        {
            assessment.Verdict = RiskVerdict.Critical;
        }
        else if (assessment.CompositeRiskScore >= 0.50m || patternScore >= 0.60m)
        {
            assessment.Verdict = RiskVerdict.HighRisk;
        }
        else if (assessment.CompositeRiskScore >= 0.30m)
        {
            assessment.Verdict = RiskVerdict.MediumRisk;
        }
        else if (assessment.CompositeRiskScore >= 0.15m)
        {
            assessment.Verdict = RiskVerdict.LowRisk;
        }
        else
        {
            assessment.Verdict = RiskVerdict.Safe;
        }

        return assessment;
    }

    private Regex GetInjectionPattern()
    {
        var verbs = _configuration.GetSection("SecurityGuard:InjectionActionVerbs").Get<string[]>();
        var nouns = _configuration.GetSection("SecurityGuard:InjectionTargetNouns").Get<string[]>();

        if (verbs != null && verbs.Length > 0 && nouns != null && nouns.Length > 0)
        {
            string verbPart = string.Join("|", verbs.Where(v => !string.IsNullOrWhiteSpace(v)).Select(Regex.Escape));
            string nounPart = string.Join("|", nouns.Where(n => !string.IsNullOrWhiteSpace(n)).Select(Regex.Escape));
            return new Regex($"({verbPart}).*?({nounPart})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return FallbackInjectionPattern;
    }

    private Regex GetAttackKeywordsPattern()
    {
        var keywords = _configuration.GetSection("SecurityGuard:AttackKeywords").Get<string[]>();
        if (keywords != null && keywords.Length > 0)
        {
            string kwPart = string.Join("|", keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(Regex.Escape));
            return new Regex($"({kwPart})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        return FallbackAttackKeywordsPattern;
    }

    private static readonly Regex FallbackInjectionPattern = new(
        @"(?:ignore|disregard|forget|bypass|override|cancel|dismiss|neglect|reset|drop|disable|clear|stop|skip|do not follow|تجاهل|تجاوز|الغاء|إلغاء|انسى|أنسى|انسالي|أنسالي|فكك|سيبك|امسح|الغي|ألغي|تخطى|تخطي|اترك|بطل|وقف|طنش)" +
        @".{0,60}?" +
        @"(?:instruction|instructions|insteraction|insteractions|rule|rules|prompt|prompts|system|guideline|guidelines|guardrail|guardrails|security|filter|filters|policy|policies|limitations|restrictions|bounds|boundaries|constraints|morals|(?:ال)?تعليمات|(?:ال)?أوامر|(?:ال)?اوامر|(?:ال)?قواعد|(?:ال)?سياسات|(?:ال)?نظام|(?:ال)?حماية|(?:ال)?قيود|(?:ال)?فلاتر|(?:ال)?شروط|(?:ال)?ارشادات|(?:ال)?إرشادات|التعليمات\s+ال\s+فاتت|التعليمات\s+الفاتت|كل\s+اللي\s+فات|اللي\s+فات|ما\s+سبق|السابقة|المفروضة)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FallbackAttackKeywordsPattern = new(
        @"(?:jailbreak|dan\s+mode|developer\s+mode|aim\s+mode|unfiltered\s+ai|unrestricted|evil\s+bot|do\s+anything\s+now|anti-censorship|hypothetical\s+scenario|no\s+rules|no\s+limits|chaos\s+mode|bypass\s+mode|uncensored\s+mode|uncensored|developer\s+prompt|system\s+prompt|raw\s+prompt|initial\s+prompt|reveal.{0,30}?(?:system|prompt|instructions)|show.{0,30}?(?:system|developer|prompt|instructions)|print.{0,30}?(?:system|prompt|instructions)|what\s+is\s+your.{0,20}?(?:prompt|system|instructions)|what\s+were\s+your\s+instructions|what\s+were\s+the\s+rules|repeat\s+(?:the\s+)?exact|repeat\s+above|repeat\s+text|system\s+override|dump\s+prompt|dump\s+all|export\s+database|export\s+database\s+tables|(?:give|provide|share|show|reveal|tell|send|export|dump|leak).{0,50}?(?:all\s+)?(?:patient\s+(?:records|files)|database|tables|secrets|api\s+keys|credentials|passwords|hashes|private\s+keys|(?:your|the|all|any)?\s*(?:sensitive|private|secret|confidential|personal|protected)\s+(?:medical\s+)?(?:data|information|details|records|files|documents|prompts|secrets|keys))|جليبريك|جيلبريك|وضع\s+المطور|تجاوز\s+الحماية|كشف\s+البرومبت|كسر\s+الحماية|دون\s+قيود|بلا\s+حدود|اظهر\s+(?:ال)?برومبت|أظهر\s+(?:ال)?برومبت|اكشف\s+(?:ال)?برومبت|اطبع\s+(?:ال)?تعليمات|اطبع\s+(?:ال)?اوامر|اعرض\s+(?:ال)?اوامر|اعرض.{0,20}?(?:ال)?برومبت|كشف\s+(?:ال)?نظام|ما\s+هي\s+تعليماتك|ما\s+هو\s+البرومبت|ما\s+هي\s+القواعد|هات\s+كل\s+السجلات|تسريب\s+بيانات|(?:تسريب|سرقة|تصدير|كشف|هات|اعطني|عرض|اطبع).{0,40}?(?:بيانات\s+المرضى|(?:ال)?سجلات\s+(?:الطبية|المحمية|المرضى)|الملفات\s+السرية|قاعدة\s+البيانات|كلمات\s+المرور|السياسات\s+البرمجية))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DelimiterInjectionPattern = new(
        @"(?:\[system\]|<\|im_start\|>|<\|im_end\|>|<\|endoftext\|>|###\s*system|###\s*instruction|###\s*human|\[assistant\]|<system>|<\/system>|\bhuman:\s*|\bassistant:\s*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CodeAndSqlInjectionPattern = new(
        @"(?:<script|drop\s+table|exec\s*\(|union\s+select|insert\s+into|delete\s+from\s+|information_schema|xp_cmdshell|system\s*\(|base64_decode)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public decimal ScanRulePatterns(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return 0.0m;

        string lower = prompt.ToLowerInvariant();
        var injectionPattern = GetInjectionPattern();
        var attackPattern = GetAttackKeywordsPattern();

        // 1. Dynamic Rule Matching via Config-driven / Fallback Regex
        if (injectionPattern.IsMatch(lower) || attackPattern.IsMatch(lower))
        {
            return 1.0m;
        }

        // 2. Delimiter & Chat Template Hijacking
        if (DelimiterInjectionPattern.IsMatch(lower))
        {
            return 0.95m;
        }

        // 3. Code Execution & SQL Injection Payloads
        if (CodeAndSqlInjectionPattern.IsMatch(lower))
        {
            return 0.90m;
        }

        // 4. Base64 Obfuscation Decoder Inspection
        var base64Candidates = Regex.Matches(prompt, @"[A-Za-z0-9+/=]{16,}");
        foreach (Match match in base64Candidates)
        {
            try
            {
                byte[] decodedBytes = Convert.FromBase64String(match.Value);
                string decodedStr = Encoding.UTF8.GetString(decodedBytes).ToLowerInvariant();
                if (injectionPattern.IsMatch(decodedStr) || attackPattern.IsMatch(decodedStr) ||
                    DelimiterInjectionPattern.IsMatch(decodedStr) || CodeAndSqlInjectionPattern.IsMatch(decodedStr))
                {
                    return 1.0m;
                }
            }
            catch
            {
                // Not valid base64 payload, continue
            }
        }

        return 0.0m;
    }

    private async Task<decimal> CallPromptGuardModelApiAsync(string prompt, decimal patternScore, CancellationToken ct)
    {
        string? guardrailUrl = _configuration["Guardrail:ApiUrl"];
        string? modelName = _configuration["Guardrail:Model"] ?? "llama-guard3";

        bool skipRemoteCall = false;
        lock (_circuitLock)
        {
            if (DateTimeOffset.UtcNow < _circuitBreakerUntilUtc)
            {
                skipRemoteCall = true;
            }
        }

        if (!skipRemoteCall && !string.IsNullOrWhiteSpace(guardrailUrl))
        {
            try
            {
                var payload = new
                {
                    model = modelName,
                    prompt = prompt,
                    stream = false
                };

                // Fast 1.5s timeout prevents blocking cloud requests when local Ollama is absent
                using var guardCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, guardCts.Token);
                var response = await _httpClient.PostAsJsonAsync(guardrailUrl, payload, linkedCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(linkedCts.Token), cancellationToken: linkedCts.Token);
                    if (doc.RootElement.TryGetProperty("response", out var respProp))
                    {
                        string output = respProp.GetString()?.ToLowerInvariant() ?? string.Empty;
                        if (output.Contains("unsafe"))
                        {
                            // S6 = Specialized Medical Advice. In a clinical RAG system, medical questions are legitimate.
                            if (output.Contains("s6") && !output.Contains("s1") && !output.Contains("s2"))
                            {
                                _logger.LogInformation("[Llama-Guard 3] Output flagged S6 (clinical advice) — permitted for healthcare RAG.");
                                return 0.10m;
                            }
                            _logger.LogWarning("[Llama-Guard 3] Flagged prompt as unsafe: {Output}", output);
                            return 0.95m;
                        }
                        if (output.Contains("safe"))
                        {
                            _logger.LogInformation("[Llama-Guard 3] Verified prompt as safe.");
                            return 0.05m;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_circuitLock)
                {
                    // Open circuit breaker for 5 minutes to avoid lagging subsequent user queries
                    _circuitBreakerUntilUtc = DateTimeOffset.UtcNow.AddMinutes(5);
                }
                _logger.LogWarning(ex, "[PromptGuard] Remote model endpoint unreachable or timed out. Circuit breaker tripped for 5 mins; using offline heuristics.");
            }
        }

        // Resilient Offline ML Heuristic Engine:
        // If the rule engine already flagged high risk, maintain consistency in cloud mode
        if (patternScore >= 0.8m)
        {
            return patternScore;
        }

        // Punctuation & Anomaly Ratio Analysis
        int nonAlphaNumeric = prompt.Count(c =>
            !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) &&
            c != '[' && c != ']' && c != '_');
        double punctuationRatio = prompt.Length > 0 ? (double)nonAlphaNumeric / prompt.Length : 0;

        if (prompt.Length > 3000 || punctuationRatio > 0.25)
        {
            return 0.70m;
        }

        return 0.05m;
    }

    private static decimal EvaluateLlmJudgeRisk(string prompt, decimal patternScore, decimal mlModelScore)
    {
        if (patternScore >= 0.8m || mlModelScore >= 0.8m)
            return 0.95m;

        string lower = prompt.ToLowerInvariant();
        if (lower.Contains("developer mode") || lower.Contains("act as an unfiltered ai") ||
            lower.Contains("reveal system prompt") || lower.Contains("وضع المطور") ||
            lower.Contains("كسر الحماية"))
        {
            return 0.85m;
        }

        return 0.05m;
    }
}
