namespace Smoc.Streaming;

/// <summary>
/// Base record for a streaming entity with a unique ID.
/// </summary>
/// <param name="Id">The unique identifier.</param>
public abstract record Entity(string Id);
