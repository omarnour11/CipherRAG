using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Domain.Entities;
using CipherRAG.Domain.Enums;
using CipherRAG.Domain.ValueObjects;
using CipherRAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.Infrastructure.Services;

public class RagService : IRagService
{
    private readonly AppDbContext _context;
    private readonly ILlmProvider _llmProvider;
    private readonly IPresidioService _presidioService;
    private readonly IGuardrailService _guardrailService;
    private readonly IVectorEmbeddingService _embeddingService;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public RagService(
        AppDbContext context,
        ILlmProvider llmProvider,
        IPresidioService presidioService,
        IGuardrailService guardrailService,
        IVectorEmbeddingService embeddingService,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _llmProvider = llmProvider;
        _presidioService = presidioService;
        _guardrailService = guardrailService;
        _embeddingService = embeddingService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<RagAskResponse> AskAsync(RagAskRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid();
        string rawPrompt = request.UserPrompt ?? string.Empty;

        // Load User details and Access Policies for authorization checks
        User? user = null;
        if (request.UserId != Guid.Empty)
        {
            user = await _context.Users
                .Include(u => u.Role)
                .ThenInclude(r => r!.AccessPolicies)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, ct);
        }

        if (user == null)
        {
            throw new InvalidOperationException("No user profile found in system database for RAG operations.");
        }

        var userId = user.UserId;

        // =========================================================================
        // ABAC Security Check 1: User Account Deactivation Verification
        // =========================================================================
        if (!user.IsActive)
        {
            stopwatch.Stop();
            string deactAnswer = "[SECURITY ALERT]: Access denied. Your user account is deactivated by the security administrator.";

            var failedRagRequest = new RagRequest
            {
                RequestId = requestId,
                UserId = userId,
                MaskedPrompt = rawPrompt,
                MaskedFinalResponse = deactAnswer,
                CreatedAt = DateTimeOffset.UtcNow,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Status = RequestStatus.Failed
            };
            _context.RagRequests.Add(failedRagRequest);

            var deactAuditLog = new AuditLogEntry
            {
                TsUtc = DateTimeOffset.UtcNow,
                RawPrompt = rawPrompt,
                MaskedPrompt = rawPrompt,          // no masking at this stage (blocked before masking)
                MaskedFinalAnswer = deactAnswer,
                FinalAnswer = deactAnswer,
                ActionStatus = ActionStatus.Blocked,
                RetrievedChunkIds = [],
                InjectionDetected = false,
                PoisonDetected = false,
                GroundingScore = 0.0m,
                UserId = userId,
                RoleId = user.RoleId,
                RequestId = requestId,
                SessionId = null,
                PrevRowHash = await GetLastAuditLogHashAsync(ct)
            };
            // HIPAA §164.312(b) — Tamper-evident audit: compute hash AFTER DB assigns LogId
            _context.AuditLogs.Add(deactAuditLog);
            await _context.SaveChangesAsync(ct);
            deactAuditLog.RowHash = ComputeAuditRowHash(deactAuditLog);
            await _context.SaveChangesAsync(ct);

            return new RagAskResponse
            {
                RequestId = requestId,
                RawPrompt = rawPrompt,
                MaskedPrompt = rawPrompt,
                FinalAnswer = deactAnswer,
                RetrievedChunks = [],
                SecurityMetadata = new RagSecurityMetadata
                {
                    SessionId = null,
                    AssessmentId = null,
                    InjectionDetected = false,
                    PoisonDetected = false,
                    GroundingScore = 0.0m,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    MaskedTokenCount = 0,
                    RiskVerdict = RiskVerdict.Critical,
                    CompositeRiskScore = 1.0m
                }
            };
        }

        // =========================================================================
        //  ABAC Security Check 2: Dynamic Time-Window & Context Policy Evaluation
        // =========================================================================
        var activePolicies = user.Role?.AccessPolicies?.Where(p => p.IsActive).ToList();
        if (activePolicies != null && activePolicies.Count > 0)
        {
            var reqIp = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var requestCtx = new RequestContext
            {
                UserDepartment = string.IsNullOrWhiteSpace(request.Department) ? (user.Department ?? "") : request.Department,
                IpAddress = reqIp,
                Country = "EG", // System default location
                RequestTime = DateTimeOffset.UtcNow
            };

            // Administrators (Role 'Admin' or CanManagePolicies) have global administrative access across networks and hours.
            bool isAdmin = string.Equals(user.Role?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase) || 
                           user.Role?.CanManagePolicies == true;

            var evaluations = activePolicies.Select(p => p.EvaluateWithReason(requestCtx)).ToList();
            bool isAllowedByPolicy = isAdmin || evaluations.Any(e => e.IsAllowed);

            if (!isAllowedByPolicy)
            {
                stopwatch.Stop();
                var specificReasons = evaluations
                    .Select(e => e.FailureReason)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct()
                    .ToList();

                string failureDetails = specificReasons.Count > 0
                    ? string.Join(" | ", specificReasons)
                    : "Request context falls outside authorized IP, country, or department constraints.";

                string policyAnswer = $"[SECURITY ALERT]: Access denied by ABAC Security Policy for user '{user.FullName}' ({requestCtx.UserDepartment}). {failureDetails}";

                var failedRagRequest = new RagRequest
                {
                    RequestId = requestId,
                    UserId = userId,
                    MaskedPrompt = rawPrompt,
                    MaskedFinalResponse = policyAnswer,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    Status = RequestStatus.Failed
                };
                _context.RagRequests.Add(failedRagRequest);

                var policyAuditLog = new AuditLogEntry
                {
                    TsUtc = DateTimeOffset.UtcNow,
                    RawPrompt = rawPrompt,
                    MaskedPrompt = rawPrompt,
                    MaskedFinalAnswer = policyAnswer,
                    FinalAnswer = policyAnswer,
                    ActionStatus = ActionStatus.Blocked,
                    RetrievedChunkIds = [],
                    InjectionDetected = false,
                    PoisonDetected = false,
                    GroundingScore = 0.0m,
                    UserId = userId,
                    RoleId = user.RoleId,
                    RequestId = requestId,
                    SessionId = null,
                    PrevRowHash = await GetLastAuditLogHashAsync(ct)
                };
                // HIPAA §164.312(b) — Tamper-evident audit: compute hash AFTER DB assigns LogId
                _context.AuditLogs.Add(policyAuditLog);
                await _context.SaveChangesAsync(ct);
                policyAuditLog.RowHash = ComputeAuditRowHash(policyAuditLog);
                await _context.SaveChangesAsync(ct);

                return new RagAskResponse
                {
                    RequestId = requestId,
                    RawPrompt = rawPrompt,
                    MaskedPrompt = rawPrompt,
                    FinalAnswer = policyAnswer,
                    RetrievedChunks = [],
                    SecurityMetadata = new RagSecurityMetadata
                    {
                        SessionId = null,
                        AssessmentId = null,
                        InjectionDetected = false,
                        PoisonDetected = false,
                        GroundingScore = 0.0m,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        MaskedTokenCount = 0,
                        RiskVerdict = RiskVerdict.HighRisk,
                        CompositeRiskScore = 0.85m
                    }
                };
            }
        }

        // =========================================================================
        //  Parallel Execution: PII Masking & Security Risk Assessment
        // =========================================================================
        var sessionId = Guid.NewGuid();
        var maskingExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        // âš ï¸ HIPAA Â§164.312(e)(2) â€” Transmission security:
        // Masking MUST complete BEFORE the guardrail call.
        // The guardrail is an external API; sending unmasked PII would be a HIPAA violation.
        var (maskedPrompt, maskedTokens) = await _presidioService.AnalyzeAndMaskAsync(rawPrompt, sessionId, maskingExpiresAt, ct);
        var riskAssessment = await _guardrailService.EvaluateRiskAsync(rawPrompt, maskedPrompt, ct);

        var maskingSession = new MaskingSession
        {
            SessionId = sessionId,
            RequestId = requestId,
            UserId = userId,
            ExpiresAt = maskingExpiresAt,
            MaskedTokens = maskedTokens
        };

        _context.MaskingSessions.Add(maskingSession);
        _context.RiskAssessments.Add(riskAssessment);

        bool isHighRisk = riskAssessment.Verdict == RiskVerdict.Critical || riskAssessment.Verdict == RiskVerdict.HighRisk;

        if (isHighRisk)
        {
            stopwatch.Stop();
            long blockedExecutionMs = stopwatch.ElapsedMilliseconds;

            string blockedAnswer = "[SECURITY ALERT]: This request was blocked because it asks for protected or sensitive information. Ask for an authorized, non-sensitive summary instead.";

            var failedRagRequest = new RagRequest
            {
                RequestId = requestId,
                UserId = userId,
                MaskedPrompt = maskedPrompt,
                MaskedFinalResponse = blockedAnswer,
                CreatedAt = DateTimeOffset.UtcNow,
                ExecutionTimeMs = blockedExecutionMs,
                Status = RequestStatus.Failed
            };
            _context.RagRequests.Add(failedRagRequest);

            var blockedAuditLog = new AuditLogEntry
            {
                TsUtc = DateTimeOffset.UtcNow,
                RawPrompt = rawPrompt,
                MaskedPrompt = maskedPrompt,
                MaskedFinalAnswer = blockedAnswer,
                FinalAnswer = blockedAnswer,
                ActionStatus = ActionStatus.Blocked,
                RetrievedChunkIds = [],
                InjectionDetected = true,
                PoisonDetected = false,
                GroundingScore = 0.0m,
                UserId = userId,
                RoleId = user?.RoleId,
                RequestId = requestId,
                SessionId = sessionId == Guid.Empty ? null : sessionId,
                RiskAssessmentId = riskAssessment.AssessmentId,
                PrevRowHash = (await GetLastAuditLogHashAsync(ct))
            };
            // HIPAA §164.312(b) — Tamper-evident audit: compute hash AFTER DB assigns LogId
            _context.AuditLogs.Add(blockedAuditLog);
            await _context.SaveChangesAsync(ct);
            blockedAuditLog.RowHash = ComputeAuditRowHash(blockedAuditLog);
            await _context.SaveChangesAsync(ct);

            return new RagAskResponse
            {
                RequestId = requestId,
                RawPrompt = rawPrompt,
                MaskedPrompt = maskedPrompt,
                FinalAnswer = blockedAnswer,
                RetrievedChunks = [],
                SecurityMetadata = new RagSecurityMetadata
                {
                    SessionId = sessionId,
                    AssessmentId = riskAssessment.AssessmentId,
                    InjectionDetected = true,
                    PoisonDetected = false,
                    GroundingScore = 0.0m,
                    ExecutionTimeMs = blockedExecutionMs,
                    MaskedTokenCount = maskedTokens.Count,
                    RiskVerdict = riskAssessment.Verdict,
                    CompositeRiskScore = riskAssessment.CompositeRiskScore
                }
            };
        }

        // =========================================================================
        //  Vector Knowledge Retrieval & Context Assembly (Clearance-Gated)
        // =========================================================================
        List<RagChunkResultDto> retrievedChunkDtos = [];
        string maskedContext = string.Empty;
        bool poisonDetected = false;

        // HIPAA §164.312(a)(1) — Clearance-Level Retrieval & Poison Guard
        int userClearance = user.Role?.ClinicalClearance ?? 1;
        if (request.ClearanceLevel > 0)
        {
            // Cap at authorized user role clearance to prevent privilege escalation
            userClearance = Math.Min(request.ClearanceLevel, userClearance);
        }

        var queryEmbedding = _embeddingService.GenerateEmbedding(maskedPrompt);

        // Fetch candidates meeting clearance and clean status
        var candidateChunks = await _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.ClearanceLevel <= userClearance &&
                        c.PoisonStatus == PoisonStatus.Clean &&
                        c.Document != null &&
                        c.Document.ApprovalStatus == ApprovalStatus.Approved)
            .ToListAsync(ct);

        // Extract distinct keywords for Lexical / Sparse hybrid matching (ignoring generic stop words)
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "is", "the", "for", "and", "in", "of", "to", "a", "an", "are", "about", "give", "me", "show", "tell",
            "من", "هو", "هي", "ما", "في", "على", "عن", "مع", "هل", "كل", "بيانات", "معلومات", "أريد", "اريد", "هات", "ماهي", "ماهو"
        };
        var queryKeywords = rawPrompt
            .Split(new[] { ' ', '\t', '\r', '\n', '?', '!', '.', ',', ';', ':', '-', '_', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && !stopWords.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateChunks.Count > 0)
        {
            var scoredChunks = new List<(DocumentChunk Chunk, double Similarity)>();

            foreach (var chunk in candidateChunks)
            {
                float[] chunkVector;
                if (!string.IsNullOrWhiteSpace(chunk.EmbeddingJson) && chunk.EmbeddingJson != "[]")
                {
                    try
                    {
                        chunkVector = JsonSerializer.Deserialize<float[]>(chunk.EmbeddingJson) 
                                      ?? _embeddingService.GenerateEmbedding(chunk.Content);
                    }
                    catch
                    {
                        chunkVector = _embeddingService.GenerateEmbedding(chunk.Content);
                    }
                }
                else
                {
                    chunkVector = _embeddingService.GenerateEmbedding(chunk.Content);
                }

                double vectorSim = _embeddingService.ComputeCosineSimilarity(queryEmbedding, chunkVector);

                // Hybrid Search: Combine Vector Dense Embedding + Lexical Exact Keyword Matching
                double keywordRatio = 0.0;
                double titleBonus = 0.0;
                int matchesInContent = 0;
                int matchesInTitle = 0;

                if (queryKeywords.Count > 0)
                {
                    matchesInContent = queryKeywords.Count(kw => chunk.Content.Contains(kw, StringComparison.OrdinalIgnoreCase));
                    matchesInTitle = queryKeywords.Count(kw => (chunk.Document?.Title ?? "").Contains(kw, StringComparison.OrdinalIgnoreCase));

                    keywordRatio = (double)matchesInContent / queryKeywords.Count;
                    if (matchesInTitle > 0)
                    {
                        titleBonus = 0.25 * ((double)matchesInTitle / queryKeywords.Count);
                    }
                }

                double hybridScore = queryKeywords.Count > 0
                    ? (vectorSim * 0.35) + (keywordRatio * 0.50) + titleBonus
                    : vectorSim;

                // Include chunk if hybrid score is relevant or if keywords strongly match
                if (hybridScore >= 0.18 || keywordRatio >= 0.40 || (matchesInTitle > 0 && hybridScore >= 0.12))
                {
                    scoredChunks.Add((chunk, Math.Clamp(hybridScore, 0.0, 1.0)));
                }
            }

            var topMatches = new List<(DocumentChunk Chunk, double Similarity)>();
            foreach (var match in scoredChunks.OrderByDescending(s => s.Similarity))
            {
                // Prevent duplicate or near-identical text from multiple copies of the same uploaded document
                bool isDuplicate = topMatches.Any(m =>
                    string.Equals(m.Chunk.Content.Trim(), match.Chunk.Content.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    (m.Chunk.Content.Length > 50 && match.Chunk.Content.Length > 50 &&
                     (m.Chunk.Content.Contains(match.Chunk.Content.Substring(0, Math.Min(60, match.Chunk.Content.Length)), StringComparison.OrdinalIgnoreCase) ||
                      match.Chunk.Content.Contains(m.Chunk.Content.Substring(0, Math.Min(60, m.Chunk.Content.Length)), StringComparison.OrdinalIgnoreCase))));

                if (!isDuplicate)
                {
                    topMatches.Add(match);
                }

                if (topMatches.Count >= 3)
                    break;
            }

            if (topMatches.Count > 0)
            {
                var contextBuilder = new StringBuilder();
                foreach (var match in topMatches)
                {
                    contextBuilder.AppendLine($"[Document: {match.Chunk.Document?.Title ?? "Internal Protocol"}]");
                    contextBuilder.AppendLine(match.Chunk.Content);
                    contextBuilder.AppendLine();

                    retrievedChunkDtos.Add(new RagChunkResultDto
                    {
                        ChunkId = match.Chunk.ChunkId,
                        DocumentId = match.Chunk.DocumentId,
                        DocumentTitle = match.Chunk.Document?.Title ?? "Internal Document",
                        Content = match.Chunk.Content,
                        RelevanceScore = Math.Round(match.Similarity, 4),
                        PoisonStatus = match.Chunk.PoisonStatus
                    });
                }
                maskedContext = contextBuilder.ToString().Trim();
            }
        }

        string maskedLlmResponse = await _llmProvider.GenerateResponseAsync(maskedPrompt, maskedContext, ct);
        decimal groundingScore = ComputeGroundingScore(maskedLlmResponse, maskedContext);

        // =========================================================================
        //  Unmasking & Response Delivery
        // =========================================================================
        string unmaskedFinalAnswer = UnmaskResponse(maskedLlmResponse, maskedTokens);

        //  HIPAA §164.514 & §164.312(b): Output Sanitization & Audit De-Identification
        // Final answers synthesized from document chunks or LLM output may contain PHI/PII.
        // Audit logs must NEVER store raw unmasked PHI/PII. De-identify via Presidio before logging.
        var (maskedAuditAnswer, _) = await _presidioService.AnalyzeAndMaskAsync(
            unmaskedFinalAnswer,
            sessionId,
            maskingExpiresAt,
            ct);

        stopwatch.Stop();
        long executionMs = stopwatch.ElapsedMilliseconds;

        // =========================================================================
        //  Audit & Request Logging
        // =========================================================================
        var ragRequest = new RagRequest
        {
            RequestId = requestId,
            UserId = userId,
            MaskedPrompt = maskedPrompt,
            MaskedFinalResponse = maskedAuditAnswer,
            CreatedAt = DateTimeOffset.UtcNow,
            ExecutionTimeMs = executionMs,
            Status = RequestStatus.Completed
        };
        _context.RagRequests.Add(ragRequest);

        var auditLog = new AuditLogEntry
        {
            TsUtc = DateTimeOffset.UtcNow,
            RawPrompt = rawPrompt,
            MaskedPrompt = maskedPrompt,
            MaskedFinalAnswer = maskedAuditAnswer,
            FinalAnswer = maskedAuditAnswer,
            ActionStatus = ActionStatus.Allowed,
            RetrievedChunkIds = retrievedChunkDtos.Select(c => c.ChunkId).ToList(),
            InjectionDetected = riskAssessment.PatternScore > 0.5m,
            PoisonDetected = poisonDetected,
            GroundingScore = groundingScore,
            UserId = userId,
            RoleId = user?.RoleId,
            RequestId = requestId,
            SessionId = sessionId == Guid.Empty ? null : sessionId,
            RiskAssessmentId = riskAssessment.AssessmentId,
            PrevRowHash = await GetLastAuditLogHashAsync(ct)
        };
        // HIPAA §164.312(b) — Tamper-evident audit: compute hash AFTER DB assigns LogId
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(ct);
        auditLog.RowHash = ComputeAuditRowHash(auditLog);
        await _context.SaveChangesAsync(ct);

        return new RagAskResponse
        {
            RequestId = requestId,
            RawPrompt = rawPrompt,
            MaskedPrompt = maskedPrompt,
            FinalAnswer = unmaskedFinalAnswer,
            RetrievedChunks = retrievedChunkDtos,
            SecurityMetadata = new RagSecurityMetadata
            {
                SessionId = sessionId,
                AssessmentId = riskAssessment.AssessmentId,
                InjectionDetected = riskAssessment.PatternScore > 0.5m,
                PoisonDetected = poisonDetected,
                GroundingScore = groundingScore,
                ExecutionTimeMs = executionMs,
                MaskedTokenCount = maskedTokens.Count,
                RiskVerdict = riskAssessment.Verdict,
                CompositeRiskScore = riskAssessment.CompositeRiskScore
            }
        };
    }

    // =========================================================================
    // Helper Security Methods
    // =========================================================================

    private static (string MaskedPrompt, List<MaskedToken> Tokens) PerformPiiMasking(string prompt, Guid sessionId, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return (string.Empty, []);

        var tokens = new List<MaskedToken>();
        string masked = prompt;
        int tokenCounter = 0;

        // Email Masking
        var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
        masked = emailRegex.Replace(masked, match =>
        {
            tokenCounter++;
            string placeholder = $"[MASKED_EMAIL_{tokenCounter}]";
            tokens.Add(new MaskedToken(placeholder, match.Value, EntityType.PII, sessionId, expiresAt));
            return placeholder;
        });

        // Phone Masking
        var phoneRegex = new Regex(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b");
        masked = phoneRegex.Replace(masked, match =>
        {
            tokenCounter++;
            string placeholder = $"[MASKED_PHONE_{tokenCounter}]";
            tokens.Add(new MaskedToken(placeholder, match.Value, EntityType.PII, sessionId, expiresAt));
            return placeholder;
        });

        // SSN Masking
        var ssnRegex = new Regex(@"\b\d{3}-\d{2}-\d{4}\b");
        masked = ssnRegex.Replace(masked, match =>
        {
            tokenCounter++;
            string placeholder = $"[MASKED_SSN_{tokenCounter}]";
            tokens.Add(new MaskedToken(placeholder, match.Value, EntityType.PII, sessionId, expiresAt));
            return placeholder;
        });

        return (masked, tokens);
    }

    private static RiskAssessment EvaluateSecurityRisk(string rawPrompt, string maskedPrompt)
    {
        string lower = rawPrompt.ToLowerInvariant();

        decimal patternScore = 0.0m;
        if (lower.Contains("ignore previous instructions") ||
            lower.Contains("system override") ||
            lower.Contains("jailbreak") ||
            lower.Contains("bypass security"))
        {
            patternScore = 1.0m;
        }
        else if (lower.Contains("<script>") || lower.Contains("drop table") || lower.Contains("admin_override"))
        {
            patternScore = 0.8m;
        }

        // Anomaly / ML score heuristic
        decimal mlScore = (rawPrompt.Length > 2000 || rawPrompt.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)) > 15) ? 0.6m : 0.1m;
        decimal llmJudgeScore = patternScore > 0 ? 0.9m : 0.05m;

        var assessment = new RiskAssessment
        {
            AssessmentId = Guid.NewGuid(),
            PatternScore = patternScore,
            MlModelScore = mlScore,
            LlmJudgeScore = llmJudgeScore,
            QueryHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPrompt)))
        };

        assessment.CompositeRiskScore = Math.Round(assessment.ComputeComposite(), 2);

        if (assessment.CompositeRiskScore >= 0.75m) assessment.Verdict = RiskVerdict.Critical;
        else if (assessment.CompositeRiskScore >= 0.50m) assessment.Verdict = RiskVerdict.HighRisk;
        else if (assessment.CompositeRiskScore >= 0.30m) assessment.Verdict = RiskVerdict.MediumRisk;
        else if (assessment.CompositeRiskScore >= 0.15m) assessment.Verdict = RiskVerdict.LowRisk;
        else assessment.Verdict = RiskVerdict.Safe;

        return assessment;
    }

    private static string UnmaskResponse(string maskedResponse, List<MaskedToken> tokens)
    {
        if (string.IsNullOrWhiteSpace(maskedResponse) || tokens == null || tokens.Count == 0)
            return maskedResponse;

        string unmasked = maskedResponse;
        foreach (var token in tokens)
        {
            unmasked = unmasked.Replace(token.PlaceHolder, token.EncryptedRealValue);
        }

        return unmasked;
    }

    private async Task<int> GetNextAuditLogIdAsync(CancellationToken ct)
    {
        var maxId = await _context.AuditLogs.MaxAsync(a => (int?)a.LogId, ct) ?? 0;
        return maxId + 1;
    }

    private async Task<string> GetLastAuditLogHashAsync(CancellationToken ct)
    {
        var lastLog = await _context.AuditLogs
            .OrderByDescending(a => a.LogId)
            .FirstOrDefaultAsync(ct);

        return lastLog?.RowHash ?? string.Empty;
    }

    private static decimal ComputeGroundingScore(string answer, string context)
    {
        if (string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(context))
            return 1.0m; // General query without specific context dependence

        var answerWords = answer.ToLowerInvariant()
            .Split(new[] { ' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?', '-', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Distinct()
            .ToList();

        if (answerWords.Count == 0)
            return 1.0m;

        var contextLower = context.ToLowerInvariant();
        int matchedWords = answerWords.Count(w => contextLower.Contains(w));

        decimal ratio = (decimal)matchedWords / answerWords.Count;
        return Math.Round(Math.Clamp(0.50m + (ratio * 0.50m), 0.10m, 1.00m), 2);
    }

    private static string ComputeAuditRowHash(AuditLogEntry entry)
    {
        string raw = $"{entry.LogId}|{entry.TsUtc:O}|{entry.MaskedPrompt}|{entry.MaskedFinalAnswer}|{entry.UserId}|{entry.PrevRowHash}";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
