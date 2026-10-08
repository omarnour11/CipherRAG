using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CipherRAG.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CipherRAG.Infrastructure.Services;

public class LlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public LlmProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GenerateResponseAsync(string prompt, string context, CancellationToken ct = default)
    {
        // 1. Check if RAGFlow API key is configured
        string? ragflowApiKey = _configuration["RAGFlow:ApiKey"];
        string? ragflowBaseUrl = _configuration["RAGFlow:BaseUrl"] ?? "http://localhost:9380";

        if (!string.IsNullOrWhiteSpace(ragflowApiKey) && ragflowApiKey != "changeme")
        {
            var cleanBaseUrl = ragflowBaseUrl.TrimEnd('/');
            string[] candidateEndpoints = new[]
            {
                $"{cleanBaseUrl}/v1/api/completion",
                $"{cleanBaseUrl}/api/v1/completion",
                $"{cleanBaseUrl}/v1/chats/completions"
            };

            foreach (var endpoint in candidateEndpoints)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                    string authHeader = ragflowApiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? ragflowApiKey
                        : $"Bearer {ragflowApiKey}";
                    request.Headers.TryAddWithoutValidation("Authorization", authHeader);

                    var body = new
                    {
                        question = string.IsNullOrWhiteSpace(context) ? prompt : $"Context:\n{context}\n\nUser Question:\n{prompt}",
                        stream = false
                    };
                    request.Content = JsonContent.Create(body);

                    var response = await _httpClient.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                        var root = doc.RootElement;

                        // Check root level answer / content
                        if (root.TryGetProperty("answer", out var rootAns) && !string.IsNullOrWhiteSpace(rootAns.GetString()))
                        {
                            return rootAns.GetString()!;
                        }
                        if (root.TryGetProperty("content", out var rootContent) && !string.IsNullOrWhiteSpace(rootContent.GetString()))
                        {
                            return rootContent.GetString()!;
                        }

                        // Check data level answer / content
                        if (root.TryGetProperty("data", out var dataProp))
                        {
                            if (dataProp.ValueKind == JsonValueKind.Object)
                            {
                                if (dataProp.TryGetProperty("answer", out var ansProp) && !string.IsNullOrWhiteSpace(ansProp.GetString()))
                                {
                                    return ansProp.GetString()!;
                                }
                                if (dataProp.TryGetProperty("content", out var cntProp) && !string.IsNullOrWhiteSpace(cntProp.GetString()))
                                {
                                    return cntProp.GetString()!;
                                }
                            }
                            else if (dataProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(dataProp.GetString()))
                            {
                                return dataProp.GetString()!;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue to next candidate endpoint if offline or unreachable
                }
            }
        }

        // 2. Check if external Ollama endpoint is configured in appsettings
        string? ollamaEndpoint = _configuration["LLM:OllamaEndpoint"];
        if (!string.IsNullOrWhiteSpace(ollamaEndpoint))
        {
            try
            {
                var requestBody = new
                {
                    model = _configuration["LLM:Model"] ?? "llama3",
                    prompt = string.IsNullOrWhiteSpace(context) ? prompt : $"Context:\n{context}\n\nUser Question:\n{prompt}",
                    stream = false
                };
                var response = await _httpClient.PostAsJsonAsync(ollamaEndpoint, requestBody, ct);
                if (response.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("response", out var respProp))
                    {
                        var respStr = respProp.GetString();
                        if (!string.IsNullOrWhiteSpace(respStr)) return respStr;
                    }
                }
            }
            catch
            {
                // Fallback to local synthesis engine if external LLM service is offline
            }
        }

        // 3. Conversational Heuristic Fallback
        string trimmed = prompt.Trim().ToLower();
        if (trimmed == "مرحبا" || trimmed == "مرحباً" || trimmed == "السلام عليكم" || trimmed == "hi" || trimmed == "hello")
        {
            return "أهلاً بك! أنا نظام بوابة الأمان CipherRAG. كيف يمكنني مساعدتك اليوم؟";
        }

        if (!string.IsNullOrWhiteSpace(context))
        {
            var cleanLines = context.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(l => l.Trim())
                                     .Where(l => !string.IsNullOrWhiteSpace(l) &&
                                                 !l.StartsWith("[Document:") &&
                                                 !Regex.IsMatch(l, @"^\d+\s+\d+\s+obj") &&
                                                 !l.Contains("/FlateDecode") &&
                                                 !l.Contains("endstream") &&
                                                 !l.Contains("endobj"))
                                     .Distinct();

            string formattedContent = string.Join("\n\n", cleanLines);
            if (!string.IsNullOrWhiteSpace(formattedContent))
            {
                return $"Based on the authorized retrieved documentation:\n\n{formattedContent}";
            }
        }

        // ⚠️ Do NOT echo the prompt here – it may contain [MASKED_*] tokens that would be
        // visible to the caller before unmasking, or expose sensitive structure.
        return $"تم استلام طلبك بنجاح عبر بوابة الأمان CipherRAG. لا يوجد نظام LLM متاح حالياً للرد على استفساراتك. يرجى التواصل مع المسؤول لتفعيل خدمة الذكاء الاصطناعي.";
    }
}
