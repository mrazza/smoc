namespace smoc.Tests.TestInfra;

/// <summary>
/// A seekable stream that tracks whether it has been disposed.
/// </summary>
public sealed class TrackedSeekableStream : MemoryStream {
  /// <summary>
  /// Gets whether the stream has been disposed.
  /// </summary>
  public bool IsDisposed { get; private set; }

  /// <summary>
  /// Initializes a new instance of the <see cref="TrackedSeekableStream"/> class.
  /// </summary>
  /// <param name="buffer">The underlying buffer.</param>
  public TrackedSeekableStream(byte[] buffer) : base(buffer) {
  }

  /// <inheritdoc/>
  protected override void Dispose(bool disposing) {
    if (disposing) {
      IsDisposed = true;
    }
    base.Dispose(disposing);
  }
}
