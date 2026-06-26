namespace DataRetriever.Application.Runs;

public sealed record DataRetrievalRunOptions(
    string? Currency,
    IReadOnlyList<string> InternalIds)
{
    public static DataRetrievalRunOptions All { get; } = new(null, []);

    public static DataRetrievalRunOptions FromRequest(
        string? currency,
        IEnumerable<string>? internalIds)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? null
            : currency.Trim().ToUpperInvariant();

        var normalizedIds = (internalIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DataRetrievalRunOptions(normalizedCurrency, normalizedIds);
    }
}
