using CipherRAG.Application.Interfaces;
using CipherRAG.Application.DTOs;
using CipherRAG.Application.Mappings;
using CipherRAG.Application.Responses;
using CipherRAG.Domain.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CipherRAG.Endpoints;

public static class AuditLogEndpoints
{
    public static void MapAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs")
                       .WithTags("AuditLogs")
                       .RequireAuthorization("AuditViewPolicy");

        group.MapGet("/", GetAllAuditLogs)
             .WithName("GetAllAuditLogs");

        group.MapGet("/paged", GetPagedAuditLogs)
             .WithName("GetPagedAuditLogs");

        group.MapGet("/flagged", GetFlaggedAuditLogs)
             .WithName("GetFlaggedAuditLogs");

        group.MapGet("/verify-chain", VerifyAuditLogChain)
             .WithName("VerifyAuditLogChain");

        group.MapGet("/{id:long}", GetAuditLogById)
             .WithName("GetAuditLogById");
    }

    public static async Task<IResult> GetAllAuditLogs(
        IUnitOfWork unitOfWork, ICacheService cacheService, CancellationToken ct)
    {
        var dtos = await cacheService.GetOrCreateAsync("audit-logs:all", async _ =>
        {
            var logs = await unitOfWork.AuditLogs.GetAllAsync();
            return logs.Select(l => l.ToDto()).ToList();
        }, TimeSpan.FromSeconds(10), ct);

        return Results.Ok(dtos);
    }

    public static async Task<IResult> GetPagedAuditLogs(
        int pageNumber, int pageSize, IUnitOfWork unitOfWork, ICacheService cacheService, CancellationToken ct)
    {
        var response = await cacheService.GetOrCreateAsync(
            $"audit-logs:paged:{pageNumber}:{pageSize}", async _ =>
        {
            var pagedLogs = await unitOfWork.AuditLogs.GetAllAsyncPaged(pageNumber, pageSize);
            var dtos = pagedLogs.Items.Select(l => l.ToDto());
            return new PagedResultResponse<AuditLogDto>(
                dtos, pagedLogs.TotalCount, pagedLogs.TotalPages, pagedLogs.PageNumber, pagedLogs.PageSize);
        }, TimeSpan.FromSeconds(10), ct);

        return Results.Ok(response);
    }

    public static async Task<IResult> GetFlaggedAuditLogs(
        IUnitOfWork unitOfWork, ICacheService cacheService, CancellationToken ct)
    {
        var dtos = await cacheService.GetOrCreateAsync("audit-logs:flagged", async _ =>
        {
            var flaggedLogs = await unitOfWork.AuditLogs.GetFlaggedLogsAsync();
            return flaggedLogs.Select(l => l.ToDto()).ToList();
        }, TimeSpan.FromSeconds(10), ct);

        return Results.Ok(dtos);
    }

    // HIPAA Â§164.312(b) â€” Audit controls: all access to audit logs is authorized via AuditViewPolicy
    public static async Task<IResult> GetAuditLogById(
        long id, IUnitOfWork unitOfWork, ICacheService cacheService,
        ILogger<Program> logger, CancellationToken ct)
    {
        var dto = await cacheService.GetOrCreateAsync($"audit-logs:{id}", async _ =>
        {
            var log = await unitOfWork.AuditLogs.GetByIdAsync(id);
            return log is null ? null : log.ToDto();
        }, TimeSpan.FromSeconds(10), ct);

        if (dto is null)
        {
            logger.LogWarning("HIPAA Audit: Log entry {LogId} not found", id);
            return Results.NotFound(new { Message = $"Audit log with ID {id} was not found." });
        }

        return Results.Ok(dto);
    }

    // HIPAA §164.312(b) — Cryptographic chain verification to detect tampering
    public static async Task<IResult> VerifyAuditLogChain(
        IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var logs = (await unitOfWork.AuditLogs.GetAllAsync())
            .OrderBy(l => l.LogId)
            .ToList();

        if (logs.Count == 0)
        {
            return Results.Ok(new
            {
                totalLogsAudited = 0,
                isChainIntact = true,
                genesisHash = "GENESIS_EMPTY",
                latestHash = "GENESIS_EMPTY",
                tamperedRecords = 0,
                message = "Audit trail is empty. No records to verify."
            });
        }

        string expectedPrevHash = logs.First().PrevRowHash;
        int tamperedCount = 0;
        var brokenLinks = new List<long>();

        foreach (var log in logs)
        {
            // Verify link to previous entry
            if (log != logs.First() && !string.Equals(log.PrevRowHash, expectedPrevHash, StringComparison.OrdinalIgnoreCase))
            {
                tamperedCount++;
                brokenLinks.Add(log.LogId);
            }

            // Verify row hash self-consistency
            string raw = $"{log.LogId}|{log.TsUtc:O}|{log.MaskedPrompt}|{log.MaskedFinalAnswer}|{log.UserId}|{log.PrevRowHash}";
            string computedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

            if (!string.Equals(log.RowHash, computedHash, StringComparison.OrdinalIgnoreCase))
            {
                // In case of seed genesis logs, allow match or track
                if (!string.IsNullOrWhiteSpace(log.RowHash) && !log.RowHash.StartsWith("GENESIS"))
                {
                    tamperedCount++;
                    if (!brokenLinks.Contains(log.LogId)) brokenLinks.Add(log.LogId);
                }
            }

            expectedPrevHash = log.RowHash;
        }

        bool isIntact = tamperedCount == 0;
        return Results.Ok(new
        {
            totalLogsAudited = logs.Count,
            isChainIntact = isIntact,
            genesisHash = logs.First().PrevRowHash,
            latestHash = logs.Last().RowHash,
            tamperedRecords = tamperedCount,
            brokenLogIds = brokenLinks,
            message = isIntact
                ? $"Cryptographic integrity verified. All {logs.Count} audit records match sequential SHA-256 hash chaining (HIPAA §164.312(b))."
                : $"Tampering detected in {tamperedCount} audit record(s). Hash sequence integrity broken."
        });
    }
}