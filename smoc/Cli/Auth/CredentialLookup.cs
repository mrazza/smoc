namespace Smoc.Cli.Auth;

/// <summary>
/// Result of looking up a credential and determining its storage type.
/// </summary>
/// <param name="Value">The credential value, or <c>null</c> if not found.</param>
/// <param name="Storage">Where the credential was located.</param>
/// <param name="Error">Error message if an error occurred during retrieval, otherwise <c>null</c>.</param>
internal sealed record CredentialLookup(string? Value, CredentialStorageKind Storage, string? Error);

