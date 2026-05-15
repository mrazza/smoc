namespace Smoc.Services.Cast;

public interface IStreamingProxyService : IDisposable {
    string StartProxy(Stream stream, string contentType);
    void StopProxy();
    string? CurrentProxyUrl { get; }
}