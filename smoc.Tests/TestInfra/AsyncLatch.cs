namespace smoc.Tests.TestInfra;

class AsyncLatch : IDisposable {
  private readonly SemaphoreSlim _semaphore;

  public AsyncLatch(bool startLatched) {
    _semaphore = new SemaphoreSlim(startLatched ? 0 : 1, 1);
  }

  public void Latch() {
    if (_semaphore.CurrentCount == 0) throw new InvalidOperationException("Latch is already latched");

    _semaphore.Wait();
  }

  public Task GetWaiter() {
    return _semaphore.WaitAsync();
  }

  public void Release() {
    _semaphore.Release();
  }

  public void Dispose() {
    _semaphore.Dispose();
  }
}