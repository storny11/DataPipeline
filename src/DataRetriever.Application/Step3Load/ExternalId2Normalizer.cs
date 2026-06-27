// Normalizes Step 3 external identifiers so request and response rows can be matched reliably.
namespace DataRetriever.Application.Step3Load;

public sealed class ExternalId2Normalizer
{
    public bool TryNormalize(string? value, out NormalizedExternalId2 normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = default;
            return false;
        }

        normalized = new NormalizedExternalId2(value.Trim().ToUpperInvariant());
        return true;
    }
}

public readonly record struct NormalizedExternalId2(string Value);
