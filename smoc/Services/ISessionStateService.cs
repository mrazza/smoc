namespace Smoc.Services;

/// <summary>
/// Service for saving and restoring the playback session state.
/// </summary>
public interface ISessionStateService {
  /// <summary>
  /// Saves the specified session state to persistent storage.
  /// </summary>
  /// <param name="state">The session state to save.</param>
  void Save(SessionState state);

  /// <summary>
  /// Loads the previously saved session state, or returns <c>null</c> if no valid state exists.
  /// </summary>
  /// <returns>The loaded <see cref="SessionState"/>, or <c>null</c> if none is available.</returns>
  SessionState? Load();
}
