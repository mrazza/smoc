namespace smoc.Tests.TestInfra;

/// <summary>
/// A latch that can be used to synchronize between threads.
/// </summary>
class AsyncLatch : IDisposable {
  private readonly SemaphoreSlim _semaphore;

  /// <summary>
  /// Creates a new <see cref="AsyncLatch"/>.
  /// </summary>
  /// <param name="startLatched">If true, the latch will be locked on creation.</param>
  public AsyncLatch(bool startLatched) {
    _semaphore = new SemaphoreSlim(startLatched ? 0 : 1, 1);
  }

  /// <summary>
  /// Locks the latch, blocking until it is free.
  /// </summary>
  public void Latch() {
    if (_semaphore.CurrentCount == 0) {
      throw new InvalidOperationException("Latch is already latched");
    }

    if (!_semaphore.Wait(30000)) {
      throw new TimeoutException("Timeout waiting for latch");
    }
  }

  /// <summary>
  /// Waits for the latch to be free.
  /// </summary>
  /// <param name="timeoutMilliseconds">The timeout in milliseconds.</param>
  public async Task GetWaiter(int timeoutMilliseconds = 30000) {
    if (!await _semaphore.WaitAsync(timeoutMilliseconds)) {
      throw new TimeoutException("Timeout waiting for latch");
    }
  }

  /// <summary>
  /// Releases the latch.
  /// </summary>
  public void Release() {
    _semaphore.Release();
  }

  /// <summary>
  /// Disposes the underlying semaphore without releasing unreleased waiters.
  /// </summary>
  public void Dispose() {
    _semaphore.Dispose();
  }
}
