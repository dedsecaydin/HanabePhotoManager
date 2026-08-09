using System.Globalization;
using System.Text;

namespace HanabePhotoManager.Infrastructure.Search;

/// <summary>Chinese-CLIP's BERT-compatible vocabulary tokenizer.</summary>
public sealed class ClipTokenizer
{
    private readonly IReadOnlyDictionary<string, long> _vocabulary;
    private readonly long _unknownTokenId;
    private readonly long _startTokenId;
    private readonly long _endTokenId;
    private readonly long _paddingTokenId;

    public ClipTokenizer(string vocabularyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vocabularyPath);
        if (!File.Exists(vocabularyPath)) throw new FileNotFoundException("Chinese-CLIP vocabulary file is missing.", vocabularyPath);
        var tokens = File.ReadLines(vocabularyPath)
            .Select(static token => token.Trim())
            .Where(static token => token.Length > 0)
            .ToArray();
        _vocabulary = tokens.Select((token, index) => (token, index))
            .ToDictionary(static item => item.token, static item => (long)item.index, StringComparer.Ordinal);
        _unknownTokenId = GetRequiredTokenId("[UNK]");
        _startTokenId = GetRequiredTokenId("[CLS]");
        _endTokenId = GetRequiredTokenId("[SEP]");
        _paddingTokenId = GetRequiredTokenId("[PAD]");
    }

    public ClipTokenization Tokenize(string text, int maximumLength = 52)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (maximumLength < 3) throw new ArgumentOutOfRangeException(nameof(maximumLength));
        var tokenIds = Enumerable.Repeat(_paddingTokenId, maximumLength).ToArray();
        var attentionMask = new long[maximumLength];
        tokenIds[0] = _startTokenId;
        attentionMask[0] = 1;
        var index = 1;
        foreach (var token in SplitTokens(text))
        {
            if (index >= maximumLength - 1) break;
            tokenIds[index] = _vocabulary.TryGetValue(token, out var tokenId) ? tokenId : _unknownTokenId;
            attentionMask[index++] = 1;
        }

        tokenIds[index] = _endTokenId;
        attentionMask[index] = 1;
        return new ClipTokenization(tokenIds, attentionMask);
    }

    private long GetRequiredTokenId(string token) => _vocabulary.TryGetValue(token, out var id)
        ? id
        : throw new InvalidDataException($"Vocabulary is missing required token {token}.");

    private IEnumerable<string> SplitTokens(string text)
    {
        foreach (var rawWord in text.Normalize(NormalizationForm.FormKC).ToLower(CultureInfo.InvariantCulture)
                     .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = rawWord;
            while (remaining.Length > 0)
            {
                if (remaining.Length == 1 || IsChinese(remaining[0]))
                {
                    yield return remaining[..1];
                    remaining = remaining[1..];
                    continue;
                }

                var length = remaining.Length;
                string? match = null;
                while (length > 0)
                {
                    var rawCandidate = remaining[..length];
                    if (_vocabularyContains(rawCandidate)) { match = rawCandidate; break; }
                    if (_vocabularyContains("##" + rawCandidate)) { match = "##" + rawCandidate; break; }
                    length--;
                }
                yield return match ?? "[UNK]";
                remaining = remaining[Math.Max(1, length)..];
            }
        }

        bool _vocabularyContains(string token) => _vocabulary.ContainsKey(token);
    }

    private static bool IsChinese(char value) => value is >= '\u4e00' and <= '\u9fff';
}

public sealed record ClipTokenization(IReadOnlyList<long> TokenIds, IReadOnlyList<long> AttentionMask);
