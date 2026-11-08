using System.CommandLine;
using Smoc.Cli.Auth.YouTubeMusic;
using Smoc.Configuration;

namespace Smoc.Cli.Auth;

/// <summary>
/// Configures and creates the <c>auth</c> subcommand group for authentication management.
/// </summary>
public static class AuthCommand {
  /// <summary>
  /// Creates the root <c>auth</c> command with all nested subcommands using an explicit service instance.
  /// </summary>
  /// <param name="authService">The auth service instance to use.</param>
  /// <param name="console">Optional console IO override (defaults to <see cref="SystemConsole"/>).</param>
  /// <returns>A configured <see cref="Command"/> instance.</returns>
  public static Command Create(IAuthService authService, IConsoleIO? console = null) {
    ArgumentNullException.ThrowIfNull(authService);
    return Create(() => authService, console);
  }

  /// <summary>
  /// Creates the root <c>auth</c> command with all nested subcommands using a lazy service factory.
  /// </summary>
  /// <param name="authServiceFactory">Optional factory for creating <see cref="IAuthService"/> on demand.</param>
  /// <param name="console">Optional console IO override (defaults to <see cref="SystemConsole"/>).</param>
  /// <returns>A configured <see cref="Command"/> instance.</returns>
  public static Command Create(Func<IAuthService>? authServiceFactory = null, IConsoleIO? console = null) {
    IConsoleIO io = console ?? new SystemConsole();
    Lazy<IAuthService> lazyService = new(authServiceFactory ?? (() => new AuthService()));

    Command authRoot = new("auth", "Manage streaming service authentication and credentials.");

    authRoot.Subcommands.Add(CreateLoginCommand(() => lazyService.Value, io));
    authRoot.Subcommands.Add(CreateImportCookiesCommand(() => lazyService.Value, io));
    authRoot.Subcommands.Add(CreateGenTokensCommand(() => lazyService.Value, io));
    authRoot.Subcommands.Add(CreateStatusCommand(() => lazyService.Value, io));
    authRoot.Subcommands.Add(CreateLogoutCommand(() => lazyService.Value, io));
    authRoot.Subcommands.Add(CreateMigrateCommand(() => lazyService.Value, io));

    authRoot.SetAction(async (parseResult, cancellationToken) => {
      await ExecuteStatusAsync(lazyService.Value, io, cancellationToken);
      return 0;
    });

    return authRoot;
  }

  /// <summary>
  /// Formats an <see cref="AuthStatusState"/> for display in the CLI status table.
  /// </summary>
  /// <param name="state">The status state enum value.</param>
  /// <returns>A human-readable status label.</returns>
  public static string FormatStatus(AuthStatusState state) => state switch {
    AuthStatusState.Authenticated => "Authenticated",
    AuthStatusState.CookiesStored => "Cookies Stored",
    AuthStatusState.ConfiguredInsecure => "Configured (Insecure)",
    AuthStatusState.MissingCredentials => "Missing Credentials",
    AuthStatusState.NotConfigured => "Not Configured",
    AuthStatusState.Error => "Error",
    _ => state.ToString()
  };

  /// <summary>
  /// Formats a <see cref="CredentialStorageKind"/> for display in the CLI status table.
  /// </summary>
  /// <param name="storage">The storage kind enum value.</param>
  /// <returns>A human-readable storage label.</returns>
  public static string FormatStorage(CredentialStorageKind storage) => storage switch {
    CredentialStorageKind.None => "None",
    CredentialStorageKind.Keyring => "Keyring",
    CredentialStorageKind.Environment => "Environment",
    CredentialStorageKind.PlaintextConfig => "Plaintext Config",
    CredentialStorageKind.PlaintextFile => "Plaintext File",
    _ => storage.ToString()
  };

  private static Command CreateLoginCommand(Func<IAuthService> getService, IConsoleIO io) {
    Argument<string> serviceArg = new("service") {
      Description = "Streaming service to authenticate with ('subsonic', 'soundcloud')."
    };
    serviceArg.AcceptOnlyFromAmong("subsonic", "soundcloud");

    Option<string?> hostOption = new("--host") {
      Description = "Subsonic server host or IP address."
    };
    Option<int?> portOption = new("--port", "-p") {
      Description = "Subsonic server port (defaults to 80 for http, 443 for https)."
    };
    Option<string?> schemeOption = new("--scheme") {
      Description = "Subsonic server scheme ('http' or 'https')."
    };
    schemeOption.AcceptOnlyFromAmong("http", "https");

    Option<string?> usernameOption = new("--username", "-u") {
      Description = "Subsonic username."
    };
    Option<string?> passwordOption = new("--password") {
      Description = "Subsonic password (omit to prompt interactively; passing via argv may expose secrets in shell history)."
    };
    Option<bool> useTokenOption = new("--use-token") {
      Description = "Whether to use MD5 token/salt authentication for Subsonic (default: true).",
      DefaultValueFactory = _ => true
    };
    Option<bool> noVerifyOption = new("--no-verify") {
      Description = "Skip verifying the Subsonic connection before saving."
    };

    Option<string?> clientIdOption = new("--client-id") {
      Description = "SoundCloud Client ID."
    };
    Option<string?> tokenOption = new("--token", "-t") {
      Description = "SoundCloud OAuth authentication token (omit to prompt interactively; passing via argv may expose secrets in shell history)."
    };

    Command loginCmd = new("login", "Authenticate with a streaming service and save credentials securely.");
    loginCmd.Arguments.Add(serviceArg);
    loginCmd.Options.Add(hostOption);
    loginCmd.Options.Add(portOption);
    loginCmd.Options.Add(schemeOption);
    loginCmd.Options.Add(usernameOption);
    loginCmd.Options.Add(passwordOption);
    loginCmd.Options.Add(useTokenOption);
    loginCmd.Options.Add(noVerifyOption);
    loginCmd.Options.Add(clientIdOption);
    loginCmd.Options.Add(tokenOption);

    loginCmd.SetAction(async (parseResult, cancellationToken) => {
      string service = parseResult.GetValue(serviceArg)?.Trim().ToLowerInvariant() ?? string.Empty;

      switch (service) {
        case "subsonic": {
          SubsonicLoginArgs args = new(
              parseResult.GetValue(hostOption),
              parseResult.GetValue(portOption),
              parseResult.GetValue(schemeOption),
              parseResult.GetValue(usernameOption),
              parseResult.GetValue(passwordOption),
              parseResult.GetValue(useTokenOption),
              parseResult.GetValue(noVerifyOption));
          return await ExecuteSubsonicLoginAsync(getService(), io, args, cancellationToken);
        }

        case "soundcloud":
          return await ExecuteSoundCloudLoginAsync(getService(), io, parseResult.GetValue(clientIdOption), parseResult.GetValue(tokenOption), cancellationToken);

        default:
          io.WriteLine($"Unknown service '{service}'. Supported services: subsonic, soundcloud.");
          return 1;
      }
    });

    return loginCmd;
  }

  private sealed record SubsonicLoginArgs(
    string? Host,
    int? Port,
    string? Scheme,
    string? Username,
    string? Password,
    bool UseToken,
    bool NoVerify
  );

  private static async Task<int> ExecuteSubsonicLoginAsync(
    IAuthService authService,
    IConsoleIO io,
    SubsonicLoginArgs args,
    CancellationToken cancellationToken
  ) {
    if (!TryGetOrPromptRequired(io, args.Host, "Server Host: ", "Error: Server host is required.", out string host)) {
      return 1;
    }

    string? scheme = args.Scheme?.Trim().ToLowerInvariant();
    int defaultPort = scheme == "https" ? 443 : SubsonicConfig.Defaults.ServerPort;
    if (!TryGetOrPromptPort(io, args.Port, defaultPort, out int port)) {
      return 1;
    }

    if (string.IsNullOrWhiteSpace(scheme)) {
      scheme = port == 443 ? "https" : "http";
    }

    if (!TryGetOrPromptRequired(io, args.Username, "Username: ", "Error: Username is required.", out string username)) {
      return 1;
    }

    if (!TryGetOrPromptRequired(io, args.Password, "Password: ", "Error: Password is required.", out string password, isSecret: true)) {
      return 1;
    }

    if (!args.NoVerify) {
      io.WriteLine("Connecting and verifying credentials...");
    }

    try {
      await authService.LoginSubsonicAsync(host, port, scheme, username, password, args.UseToken, verifyConnection: !args.NoVerify, cancellationToken);
      io.WriteLine("Success! Saved server settings to config.json and password to secure credential store.");
      return 0;
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      io.WriteLine($"Error: {ex.Message}");
      return 1;
    }
  }

  private static async Task<int> ExecuteSoundCloudLoginAsync(
    IAuthService authService,
    IConsoleIO io,
    string? clientIdInput,
    string? tokenInput,
    CancellationToken cancellationToken
  ) {
    string? clientId = GetOrPromptOptional(io, clientIdInput, "Client ID: ");

    if (!TryGetOrPromptRequired(io, tokenInput, "OAuth Auth Token: ", "Error: Authentication token is required.", out string token, isSecret: true)) {
      return 1;
    }

    try {
      await authService.LoginSoundCloudAsync(clientId ?? string.Empty, token, cancellationToken);
      io.WriteLine("Success! Saved SoundCloud credentials to secure credential store.");
      return 0;
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      io.WriteLine($"Error: {ex.Message}");
      return 1;
    }
  }

  private static bool TryGetOrPromptRequired(
    IConsoleIO io,
    string? currentValue,
    string prompt,
    string errorMessage,
    out string result,
    bool isSecret = false
  ) {
    if (!string.IsNullOrWhiteSpace(currentValue)) {
      result = currentValue.Trim();
      return true;
    }

    string? input;
    if (isSecret) {
      input = io.ReadPassword(prompt);
    } else {
      io.Write(prompt);
      input = io.ReadLine();
    }

    if (string.IsNullOrWhiteSpace(input)) {
      io.WriteLine(errorMessage);
      result = string.Empty;
      return false;
    }

    result = input.Trim();
    return true;
  }

  private static string? GetOrPromptOptional(IConsoleIO io, string? currentValue, string prompt) {
    if (!string.IsNullOrWhiteSpace(currentValue)) {
      return currentValue.Trim();
    }

    io.Write(prompt);
    string? input = io.ReadLine()?.Trim();
    return string.IsNullOrWhiteSpace(input) ? null : input;
  }

  private static bool TryGetOrPromptPort(IConsoleIO io, int? currentPort, int defaultPort, out int port) {
    if (currentPort.HasValue) {
      if (currentPort.Value is < 1 or > 65535) {
        io.WriteLine($"Error: Port must be between 1 and 65535 (got {currentPort.Value}).");
        port = 0;
        return false;
      }

      port = currentPort.Value;
      return true;
    }

    io.Write($"Server Port [{defaultPort}]: ");
    string? portStr = io.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(portStr)) {
      port = defaultPort;
      return true;
    }

    if (!int.TryParse(portStr, out port) || port is < 1 or > 65535) {
      io.WriteLine($"Error: Invalid port '{portStr}'. Port must be an integer between 1 and 65535.");
      port = 0;
      return false;
    }

    return true;
  }

  private static Command CreateImportCookiesCommand(Func<IAuthService> getService, IConsoleIO io) {
    Argument<string?> pathArg = new("path") {
      Description = "Path to cookies.txt (defaults to ~/.config/smoc/cookies.txt).",
      Arity = ArgumentArity.ZeroOrOne
    };

    Option<bool> deleteOption = new("--delete-source") {
      Description = "Delete the plaintext cookies file after successful import."
    };

    Command importCmd = new("import-cookies", "Import YouTube Music cookies into secure credential store and generate session tokens.");
    importCmd.Arguments.Add(pathArg);
    importCmd.Options.Add(deleteOption);

    importCmd.SetAction(async (parseResult, cancellationToken) => {
      string? path = parseResult.GetValue(pathArg);
      string displayPath = string.IsNullOrWhiteSpace(path) ? SmocPaths.DefaultCookiesPath : path;

      bool deleteSource = parseResult.GetValue(deleteOption);
      if (!deleteSource && !io.IsInputRedirected) {
        deleteSource = io.Confirm("Remove plaintext cookies file after import?", defaultValue: true);
      }

      io.WriteLine($"Importing cookies from {displayPath}...");
      ImportCookiesResult result = await getService().ImportCookiesAsync(path, deleteSource, cancellationToken);

      if (result.Success) {
        io.WriteLine($"Success: {result.Message}");
        io.WriteLine($"Imported {result.CookieCount} cookies. Tokens generated: {result.TokensGenerated}.");
        if (result.SourceDeleted) {
          io.WriteLine("Plaintext cookies file deleted.");
        }
        return 0;
      }

      io.WriteLine($"Import failed: {result.Message}");
      return 1;
    });

    return importCmd;
  }

  private static Command CreateGenTokensCommand(Func<IAuthService> getService, IConsoleIO io) {
    Command genTokensCmd = new("gentokens", "Regenerate YouTube Music visitor and PO session tokens from stored cookies.");

    genTokensCmd.SetAction(async (parseResult, cancellationToken) => {
      io.WriteLine("Generating YouTube Music session tokens...");
      try {
        await getService().GenerateTokensAsync(cancellationToken);
        io.WriteLine("Success! Generated and saved YouTube Music session tokens.");
        return 0;
      } catch (OperationCanceledException) {
        throw;
      } catch (Exception ex) {
        io.WriteLine($"Error: {ex.Message}");
        return 1;
      }
    });

    return genTokensCmd;
  }

  private static Command CreateStatusCommand(Func<IAuthService> getService, IConsoleIO io) {
    Command statusCmd = new("status", "View authentication and storage status across all services.");

    statusCmd.SetAction(async (parseResult, cancellationToken) => {
      await ExecuteStatusAsync(getService(), io, cancellationToken);
      return 0;
    });

    return statusCmd;
  }

  private static async Task ExecuteStatusAsync(IAuthService authService, IConsoleIO io, CancellationToken cancellationToken) {
    IReadOnlyList<ServiceAuthStatus> statuses = await authService.GetStatusAsync(cancellationToken);

    io.WriteLine(string.Format("{0,-16} {1,-22} {2,-18} {3}", "SERVICE", "STATUS", "STORAGE", "ACCOUNT / DETAILS"));
    io.WriteLine(new string('-', 84));

    foreach (ServiceAuthStatus status in statuses) {
      io.WriteLine(string.Format(
          "{0,-16} {1,-22} {2,-18} {3}",
          status.Service,
          FormatStatus(status.Status),
          FormatStorage(status.Storage),
          status.Details));
    }
  }

  private static Command CreateLogoutCommand(Func<IAuthService> getService, IConsoleIO io) {
    Argument<string> serviceArg = new("service") {
      Description = "Service to log out of ('subsonic', 'soundcloud', 'youtube'/'ytm', or 'all').",
      Arity = ArgumentArity.ExactlyOne
    };
    serviceArg.AcceptOnlyFromAmong("subsonic", "soundcloud", "youtube", "youtubemusic", "ytm", "all");

    Command logoutCmd = new("logout", "Remove credentials from secure storage for a streaming service.");
    logoutCmd.Arguments.Add(serviceArg);

    logoutCmd.SetAction(async (parseResult, cancellationToken) => {
      string service = parseResult.GetValue(serviceArg)?.Trim() ?? string.Empty;
      IAuthService authService = getService();

      try {
        bool removed = await authService.LogoutAsync(service, cancellationToken);
        if (removed) {
          io.WriteLine($"Logged out of {service}. Credentials removed from credential store.");
        } else {
          io.WriteLine($"No credentials found to remove for {service}.");
        }

        IReadOnlyList<ServiceAuthStatus> statuses = await authService.GetStatusAsync(cancellationToken);
        if (AuthServiceTargetParser.TryParse(service, out AuthServiceTarget target)) {
          foreach (ServiceAuthStatus status in statuses) {
            if (MatchesTarget(status.Service, target) && status.Storage == CredentialStorageKind.Environment) {
              io.WriteLine($"Note: {status.Service} credentials are still active via environment variables.");
            }
          }
        }

        return 0;
      } catch (OperationCanceledException) {
        throw;
      } catch (Exception ex) {
        io.WriteLine($"Error: {ex.Message}");
        return 1;
      }
    });

    return logoutCmd;
  }

  private static bool MatchesTarget(string serviceDisplayName, AuthServiceTarget target) => target switch {
    AuthServiceTarget.All => true,
    AuthServiceTarget.Subsonic => string.Equals(serviceDisplayName, "Subsonic", StringComparison.OrdinalIgnoreCase),
    AuthServiceTarget.SoundCloud => string.Equals(serviceDisplayName, "SoundCloud", StringComparison.OrdinalIgnoreCase),
    AuthServiceTarget.YouTubeMusic => string.Equals(serviceDisplayName, "YouTube Music", StringComparison.OrdinalIgnoreCase),
    _ => false
  };

  private static Command CreateMigrateCommand(Func<IAuthService> getService, IConsoleIO io) {
    Option<bool> keepBackupOption = new("--keep-backup") {
      Description = "Keep plaintext backup files (.bak) on disk instead of deleting them."
    };

    Command migrateCmd = new("migrate", "Migrate plaintext passwords, tokens, and cookies to secure credential store.");
    migrateCmd.Options.Add(keepBackupOption);

    migrateCmd.SetAction(async (parseResult, cancellationToken) => {
      bool keepBackup = parseResult.GetValue(keepBackupOption);

      io.WriteLine("Scanning for plaintext credentials to migrate...");
      MigrationResult result = await getService().MigrateAsync(backupPlaintextFiles: keepBackup, cancellationToken);

      if (result.MigratedItems.Count == 0 && result.CleanedFiles.Count == 0) {
        if (result.Errors.Count > 0) {
          WriteSection(io, "Warnings/Errors during migration:", "!", result.Errors);
          return 1;
        }
        io.WriteLine("No plaintext credentials found. All services are already using secure storage or unconfigured.");
        return 0;
      }

      WriteSection(io, "Successfully migrated credentials to secure credential store:", "-", result.MigratedItems);
      WriteSection(io, "Sanitized plaintext files:", "-", result.CleanedFiles);
      WriteSection(io, "Warnings/Errors during migration:", "!", result.Errors);

      if (keepBackup && result.CleanedFiles.Count > 0) {
        io.WriteLine("Warning: Plaintext backup files (.bak) were retained on disk. Remove them once you have verified authentication.");
      }

      return result.Errors.Count == 0 ? 0 : 1;
    });

    return migrateCmd;
  }

  private static void WriteSection(IConsoleIO io, string header, string prefix, IReadOnlyList<string> items) {
    if (items.Count > 0) {
      io.WriteLine(header);
      foreach (string item in items) {
        io.WriteLine($"  {prefix} {item}");
      }
    }
  }
}
