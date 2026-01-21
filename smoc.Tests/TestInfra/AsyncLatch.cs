namespace smoc.Tests.TestInfra;

class AsyncLatch : IDisposable {
  private readonly SemaphoreSlim _semaphore;

  public AsyncLatch(bool startLatched) {
    _semaphore = new SemaphoreSlim(startLatched ? 0 : 1, 1);
  }

  public void Latch() {
    if (_semaphore.CurrentCount == 0) throw new InvalidOperationException("Latch is already latched");

    if (!_semaphore.Wait(1000)) throw new TimeoutException("Timeout waiting for latch");
  }

  public async Task GetWaiter(int timeoutMilliseconds = 1000) {
    if (!await _semaphore.WaitAsync(timeoutMilliseconds)) throw new TimeoutException("Timeout waiting for latch");
  }

  public void Release() {
    _semaphore.Release();
  }

  public void Dispose() {
    try {
      _semaphore.Release();
    } catch {
      // Ignore
    }

    _semaphore.Dispose();
  }
}