namespace Smoc.Streaming;

public sealed record Album(string Id, Artist Artist, string Name, int? ReleaseYear = null, string? ThumbnailUrl = null) : Entity(Id);