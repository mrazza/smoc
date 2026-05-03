namespace Smoc.Services.Util;

/// <summary>
/// This object takes ownership and manages the lifecycle of a single resource.
/// </summary>
/// <remarks>
/// Replace/Release/Read operations against this class are thread-safe; in that, they are atomic.
/// Disposals of UniqueResource are _not_ thread-safe. This object should be disposed only when no
/// other accesses to it are expected.
/// </remarks>
/// <typeparam name="T">Type of resource to manage. Must implement <see cref="IDisposable"/></typeparam>
public sealed class UniqueResource<T> : IDisposable where T : class, IDisposable {
  private T? _resource;
  private bool _disposed;
  private readonly Action<T> _onDispose;

  /// <summary>
  /// Gets the managed resource.
  /// </summary>
  public T? Resource => _resource;

  /// <summary>
  /// Creates a new instance of <see cref="UniqueResource{T}"/>.
  /// </summary>
  /// <param name="resource">The resource to manage.</param>
  /// <param name="onDispose">The action to perform when the resource is disposed.</param>
  public UniqueResource(T resource, Action<T> onDispose)
    : this(onDispose) => _resource = resource;

  /// <summary>
  /// Creates a new instance of <see cref="UniqueResource{T}"/> with a null resource.
  /// </summary>
  /// <param name="onDispose">The action to perform when the resource is disposed.</param>
  public UniqueResource(Action<T> onDispose) {
    _resource = null;
    _disposed = false;
    _onDispose = onDispose;
  }

  /// <summary>
  /// Creates a new instance of <see cref="UniqueResource{T}"/> with a null resource and no-op dispose action.
  /// </summary>
  public UniqueResource() : this((_) => { }) { }

  /// <summary>
  /// Disposes of the current resource and replaces it with the new resource.
  /// </summary>
  /// <param name="resource">The new resource to manage.</param>
  /// <returns>The new resource (for fluent syntax).</returns>
  public T Replace(T resource) {
    if (_disposed) {
      throw new ObjectDisposedException(nameof(UniqueResource<T>));
    }

    var oldResource = Interlocked.Exchange(ref _resource, resource);
    DisposeResourceIfExists(oldResource);
    return resource;
  }

  /// <summary>
  /// Releases ownership of the managed resource and returns it.
  /// </summary>
  public T? Release() => Interlocked.Exchange(ref _resource, null);

  /// <summary>
  /// Disposes of the managed resource.
  /// </summary>
  public void Dispose() {
    if (!_disposed) {
      DisposeResourceIfExists(_resource);
      _resource = null;
      _disposed = true;
    }
  }

  private void DisposeResourceIfExists(T? oldResource) {
    if (oldResource is { }) {
      _onDispose(oldResource);
      oldResource.Dispose();
    }
  }
}