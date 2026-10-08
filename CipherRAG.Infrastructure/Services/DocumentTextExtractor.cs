using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;

namespace CipherRAG.Infrastructure.Services;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default);
}

public class DocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return string.Empty;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        return extension switch
        {
            ".docx" => await ExtractTextFromDocxAsync(file, ct),
            ".pdf" => await ExtractTextFromPdfAsync(file, ct),
            _ => await ExtractTextFromPlainTextAsync(file, ct)
        };
    }

    private static async Task<string> ExtractTextFromPlainTextAsync(IFormFile file, CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    private static async Task<string> ExtractTextFromDocxAsync(IFormFile file, CancellationToken ct)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return string.Empty;

            using var entryStream = entry.Open();
            var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, ct);
            
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var sb = new StringBuilder();

            // Extract text paragraph by paragraph and table by table
            var paragraphs = doc.Descendants(w + "p");
            foreach (var p in paragraphs)
            {
                var texts = p.Descendants(w + "t").Select(e => e.Value);
                var text = string.Concat(texts).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                    sb.AppendLine(); // Preserve paragraph boundaries for chunking
                }
            }

            var result = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                // Fallback to all w:t if paragraphs returned empty
                var fallback = doc.Descendants(w + "t").Select(e => e.Value);
                result = string.Join(" ", fallback).Trim();
            }

            return string.IsNullOrWhiteSpace(result) 
                ? await ExtractTextFromPlainTextAsync(file, ct) 
                : result;
        }
        catch
        {
            return await ExtractTextFromPlainTextAsync(file, ct);
        }
    }

    private static async Task<string> ExtractTextFromPdfAsync(IFormFile file, CancellationToken ct)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(memoryStream);
            var sb = new StringBuilder();

            foreach (var page in pdfDoc.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    sb.AppendLine(page.Text);
                }
            }

            string extracted = sb.ToString().Trim();
            return extracted;
        }
        catch
        {
            return string.Empty;
        }
    }
}
