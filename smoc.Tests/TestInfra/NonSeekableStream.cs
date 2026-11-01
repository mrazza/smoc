namespace smoc.Tests.TestInfra;

/// <summary>
/// A stream implementation that does not support seeking, for testing purposes.
/// </summary>
public sealed class NonSeekableStream : Stream {
  private readonly MemoryStream _inner;

  /// <summary>
  /// Gets whether the stream has been disposed.
  /// </summary>
  public bool IsDisposed { get; private set; }

  /// <summary>
  /// Initializes a new instance of the <see cref="NonSeekableStream"/> class.
  /// </summary>
  /// <param name="data">The byte data to expose in the stream.</param>
  public NonSeekableStream(byte[] data) {
    _inner = new MemoryStream(data);
  }

  /// <inheritdoc/>
  public override bool CanRead => true;

  /// <inheritdoc/>
  public override bool CanSeek => false;

  /// <inheritdoc/>
  public override bool CanWrite => false;

  /// <inheritdoc/>
  public override long Length => _inner.Length;

  /// <inheritdoc/>
  public override long Position {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  /// <inheritdoc/>
  public override void Flush() => _inner.Flush();

  /// <inheritdoc/>
  public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

  /// <inheritdoc/>
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

  /// <inheritdoc/>
  public override void SetLength(long value) => throw new NotSupportedException();

  /// <inheritdoc/>
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

  /// <inheritdoc/>
  protected override void Dispose(bool disposing) {
    if (disposing) {
      IsDisposed = true;
      _inner.Dispose();
    }
    base.Dispose(disposing);
  }
}
