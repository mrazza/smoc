namespace Smoc.Cli.Auth;

/// <summary>
/// Result of migrating plaintext credentials and cookies to encrypted OS keyring storage.
/// </summary>
/// <param name="Success">Whether the migration completed without critical failures.</param>
/// <param name="MigratedItems">List of credentials successfully migrated into the credential store.</param>
/// <param name="CleanedFiles">List of plaintext files sanitized or backed up.</param>
/// <param name="Errors">List of non-fatal errors or warnings encountered during migration.</param>
public record MigrationResult(
  bool Success,
  IReadOnlyList<string> MigratedItems,
  IReadOnlyList<string> CleanedFiles,
  IReadOnlyList<string> Errors
);
