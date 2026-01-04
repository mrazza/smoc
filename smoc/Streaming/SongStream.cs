namespace Smoc.Streaming;

public sealed record SongStream(string Id, string Codec, Stream Stream);