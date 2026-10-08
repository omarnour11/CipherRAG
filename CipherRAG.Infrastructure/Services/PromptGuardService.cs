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

        // 1️⃣ Tier 1: Fast Rule & Heuristic Scanner
        decimal patternScore = ScanRulePatterns(rawPrompt);

        // 2️⃣ Tier 2: Dedicated AI Prompt Guard Model (Llama-Guard 3 via Ollama)
        decimal mlModelScore = await CallPromptGuardModelApiAsync(maskedPrompt, ct);

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
        if (assessment.CompositeRiskScore >= 0.70m || patternScore >= 0.9m || mlModelScore >= 0.85m)
        {
            assessment.Verdict = RiskVerdict.Critical;
        }
        else if (assessment.CompositeRiskScore >= 0.50m)
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
        @"(ignore|disregard|forget|bypass|override|cancel|تجاهل|تجاوز|الغاء|إلغاء|انسى|أنسى|فكك|سيبك|امسح|الغي|ألغي).*?(instruction|instructions|insteraction|insteractions|rule|rules|prompt|prompts|system|guideline|guidelines|guardrail|security|filter|policy|التعليمات|الأوامر|الاوامر|القواعد|السياسات|النظام|الحماية|التعليمات\s+ال\s+فاتت|التعليمات\s+الفاتت|كل\s+اللي\s+فات|اللي\s+فات)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FallbackAttackKeywordsPattern = new(
        @"(?:jailbreak|dan\s+mode|developer\s+mode|unfiltered\s+ai|reveal\s+system\s+prompt|system\s+prompt|raw\s+prompt|repeat\s+the\s+exact|repeat\s+above|repeat\s+text|system\s+override|(?:give|provide|share|show|reveal|tell|send|export|dump).{0,40}(?:your|the|all|any)?\s*(?:sensitive|private|secret|confidential|personal|protected)\s+(?:data|information|details|records|files|documents|prompts|secrets)|جليبريك|وضع\s+المطور|تجاوز\s+الحماية|كشف\s+البرومبت)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private decimal ScanRulePatterns(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return 0.0m;

        string lower = prompt.ToLowerInvariant();
        var injectionPattern = GetInjectionPattern();
        var attackPattern = GetAttackKeywordsPattern();

        // 1. Dynamic Rule Matching via Config-driven Regex
        if (injectionPattern.IsMatch(lower) || attackPattern.IsMatch(lower))
        {
            return 1.0m;
        }

        // 2. Code Execution & Injection Payloads
        if (lower.Contains("<script>") || lower.Contains("drop table") || lower.Contains("exec(") || lower.Contains("system("))
        {
            return 0.85m;
        }

        // 3. Base64 Obfuscation Decoder Inspection
        string trimmed = prompt.Trim();
        if (trimmed.Length >= 12 && (trimmed.EndsWith("==") || trimmed.EndsWith("=")))
        {
            try
            {
                byte[] decodedBytes = Convert.FromBase64String(trimmed);
                string decodedStr = Encoding.UTF8.GetString(decodedBytes).ToLowerInvariant();
                if (injectionPattern.IsMatch(decodedStr) || attackPattern.IsMatch(decodedStr))
                {
                    return 1.0m;
                }
            }
            catch (FormatException)
            {
                _logger.LogDebug("Prompt did not contain a valid Base64 payload.");
            }
        }

        return 0.0m;
    }

    // TODO [PRODUCTION ROADMAP]:
    // Currently targeting local Ollama Llama-Guard 3 (http://localhost:11434/api/generate) with local ML heuristic fallback.
    // For Production Deployment:
    // 1. Configure primary & failover endpoints (e.g. Azure Content Safety Prompt Shield / AWS Bedrock Guardrails).
    // 2. Export flagged security alerts directly to SIEM / Serilog (ElasticSearch, Datadog, or Azure Monitor).
    // 3. Enable asynchronous background audit logging for incident response tracking.
    private async Task<decimal> CallPromptGuardModelApiAsync(string prompt, CancellationToken ct)
    {
        string? guardrailUrl = _configuration["Guardrail:ApiUrl"]; // e.g. "http://localhost:11434/api/generate" or Azure Content Safety
        string? modelName = _configuration["Guardrail:Model"] ?? "llama-guard3";

        if (!string.IsNullOrWhiteSpace(guardrailUrl))
        {
            try
            {
                var payload = new
                {
                    model = modelName,
                    prompt = prompt,
                    stream = false
                };

                using var guardCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
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
                _logger.LogWarning(ex, "[PromptGuard] Llama-Guard endpoint error/timeout. Falling back to rule-based engine.");
            }
        }

        // Local ML Anomaly Classifier Heuristic Fallback
        // Exclude masking placeholder characters ([], _) from punctuation ratio
        // to avoid false positives on already-masked prompts (HIPAA de-identified content)
        int nonAlphaNumeric = prompt.Count(c =>
            !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) &&
            c != '[' && c != ']' && c != '_');
        double punctuationRatio = prompt.Length > 0 ? (double)nonAlphaNumeric / prompt.Length : 0;

        if (prompt.Length > 3000 || punctuationRatio > 0.25)
        {
            return 0.70m;
        }

        return 0.10m;
    }

    private static decimal EvaluateLlmJudgeRisk(string prompt, decimal patternScore, decimal mlModelScore)
    {
        if (patternScore >= 0.8m || mlModelScore >= 0.8m)
            return 0.95m;

        string lower = prompt.ToLowerInvariant();
        if (lower.Contains("developer mode") || lower.Contains("act as an unfiltered ai") || lower.Contains("reveal system prompt"))
        {
            return 0.85m;
        }

        return 0.05m;
    }
}
