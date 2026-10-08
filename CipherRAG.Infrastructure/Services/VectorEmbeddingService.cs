using System.Security.Cryptography;
using System.Text;

namespace CipherRAG.Infrastructure.Services;

public interface IVectorEmbeddingService
{
    float[] GenerateEmbedding(string text);
    double ComputeCosineSimilarity(float[] vectorA, float[] vectorB);
    List<string> ChunkText(string text, int chunkSize = 500, int overlap = 100);
}

public class VectorEmbeddingService : IVectorEmbeddingService
{
    private const int VectorDimensions = 64;

    public float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[VectorDimensions];

        var vector = new float[VectorDimensions];
        var words = text.ToLowerInvariant()
                        .Split(new[] { ' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?', '-', '_', '(', ')', '[', ']', '"', '\'', '/', '\\' },
                               StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return new float[VectorDimensions];

        // Frequency mapping for TF weighting
        var wordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var w in words)
        {
            if (w.Length >= 2)
            {
                wordFreq[w] = wordFreq.GetValueOrDefault(w, 0) + 1;
            }
        }

        foreach (var (word, freq) in wordFreq)
        {
            // Full word hashing with log TF weight
            float tfWeight = 1.0f + MathF.Log(freq);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(word));
            for (int i = 0; i < VectorDimensions; i++)
            {
                vector[i] += ((hash[i % hash.Length] / 255.0f) - 0.5f) * tfWeight * 1.5f;
            }

            // Character 3-grams for morphological and subword matching
            if (word.Length >= 3)
            {
                for (int j = 0; j <= word.Length - 3; j++)
                {
                    string ngram = word.Substring(j, 3);
                    byte[] ngHash = SHA256.HashData(Encoding.UTF8.GetBytes(ngram));
                    for (int i = 0; i < VectorDimensions; i++)
                    {
                        vector[i] += ((ngHash[i % ngHash.Length] / 255.0f) - 0.5f) * 0.4f;
                    }
                }
            }
        }

        // Normalize vector to unit length
        float norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 1e-6f)
        {
            for (int i = 0; i < VectorDimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    public double ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length != vectorB.Length)
            return 0.0;

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denominator <= 1e-6)
            return 0.0;

        double cosine = dotProduct / denominator;
        return Math.Clamp(cosine, 0.0, 1.0);
    }

    public List<string> ChunkText(string text, int chunkSize = 500, int overlap = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        chunkSize = Math.Max(chunkSize, 200);
        overlap = Math.Clamp(overlap, 0, chunkSize / 2);

        // 1. Break into natural text segments (paragraphs or sentences)
        var rawParagraphs = text
            .Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var atomicSegments = new List<string>();
        foreach (var p in rawParagraphs)
        {
            if (p.Length <= chunkSize)
            {
                atomicSegments.Add(p);
            }
            else
            {
                // Sub-split large paragraphs by sentence or line breaks
                var sentences = System.Text.RegularExpressions.Regex.Split(p, @"(?<=[.!?;\n])\s+")
                    .Where(s => !string.IsNullOrWhiteSpace(s));

                foreach (var s in sentences)
                {
                    if (s.Length <= chunkSize)
                    {
                        atomicSegments.Add(s);
                    }
                    else
                    {
                        // Paragraph/sentence still exceeds chunkSize: split by words
                        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var subSb = new StringBuilder();
                        foreach (var w in words)
                        {
                            if (subSb.Length + w.Length + 1 > chunkSize && subSb.Length > 0)
                            {
                                atomicSegments.Add(subSb.ToString().Trim());
                                subSb.Clear();
                            }
                            subSb.Append(w).Append(' ');
                        }
                        if (subSb.Length > 0)
                        {
                            atomicSegments.Add(subSb.ToString().Trim());
                        }
                    }
                }
            }
        }

        // If no atomic segments were generated
        if (atomicSegments.Count == 0)
        {
            atomicSegments.Add(text.Trim());
        }

        // 2. Sliding window accumulation with true overlap
        var currentChunk = new StringBuilder();

        for (int i = 0; i < atomicSegments.Count; i++)
        {
            var segment = atomicSegments[i];

            if (currentChunk.Length + segment.Length + 2 > chunkSize && currentChunk.Length > 0)
            {
                string chunkStr = currentChunk.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(chunkStr))
                {
                    chunks.Add(chunkStr);
                }

                // Prepare sliding overlap from tail of currentChunk
                string overlapSeed = string.Empty;
                if (overlap > 0 && chunkStr.Length > overlap)
                {
                    int startIdx = chunkStr.Length - overlap;
                    int spaceIdx = chunkStr.IndexOf(' ', startIdx);
                    if (spaceIdx != -1 && spaceIdx < chunkStr.Length - 10)
                    {
                        overlapSeed = chunkStr.Substring(spaceIdx + 1);
                    }
                    else
                    {
                        overlapSeed = chunkStr.Substring(startIdx);
                    }
                }

                currentChunk.Clear();
                if (!string.IsNullOrWhiteSpace(overlapSeed))
                {
                    currentChunk.Append(overlapSeed).Append("\n\n");
                }
            }

            if (currentChunk.Length > 0 && !currentChunk.ToString().EndsWith("\n\n"))
            {
                currentChunk.Append("\n\n");
            }
            currentChunk.Append(segment);
        }

        if (currentChunk.Length > 0)
        {
            string finalChunk = currentChunk.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk) && (chunks.Count == 0 || chunks[^1] != finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks;
    }
}
