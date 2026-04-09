namespace OmnilensScraping.Persistence;

public sealed class CatalogPersistenceResult
{
    public Guid SourceId { get; init; }
    public Guid SourceProductId { get; init; }
    public Guid CanonicalProductId { get; init; }
    public Guid ProductOfferId { get; init; }
    public Guid? PriceHistoryEntryId { get; init; }
    public bool CanonicalProductCreated { get; init; }
    public bool SourceProductCreated { get; init; }
}
