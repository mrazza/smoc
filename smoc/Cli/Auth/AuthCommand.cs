using System.CommandLine;
using Smoc.Configuration;

namespace Smoc.Cli.Auth;

/// <summary>
/// Configures and creates the <c>auth</c> subcommand group for authentication management.
/// </summary>
public static class AuthCommand {
  /// <summary>
  /// Creates the root <c>auth</c> command with all nested subcommands.
  /// </summary>
  /// <param name="authService">Optional auth service override (defaults to standard <see cref="AuthService"/>).</param>
  /// <param name="console">Optional console IO override (defaults to <see cref="SystemConsole"/>).</param>
  /// <returns>A configured <see cref="Command"/> instance.</returns>
  public static Command Create(IAuthService? authService = null, IConsoleIO? console = null) {
    IConsoleIO io = console ?? new SystemConsole();
    IAuthService service = authService ?? new AuthService();

    Command authRoot = new("auth", "Manage streaming service authentication and credentials.");

    authRoot.Subcommands.Add(CreateLoginCommand(service, io));
    authRoot.Subcommands.Add(CreateImportCookiesCommand(service, io));
    authRoot.Subcommands.Add(CreateStatusCommand(service, io));
    authRoot.Subcommands.Add(CreateLogoutCommand(service, io));
    authRoot.Subcommands.Add(CreateMigrateCommand(service, io));

    authRoot.SetAction(async (parseResult, cancellationToken) => {
      await ExecuteStatusAsync(service, io, cancellationToken);
      return 0;
    });

    return authRoot;
  }

  private static Command CreateLoginCommand(IAuthService authService, IConsoleIO io) {
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
          return await ExecuteSubsonicLoginAsync(authService, io, args, cancellationToken);
        }

        case "soundcloud":
          return await ExecuteSoundCloudLoginAsync(authService, io, parseResult.GetValue(clientIdOption), parseResult.GetValue(tokenOption), cancellationToken);

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
    int port = GetOrPromptPort(io, args.Port, defaultPort);

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

  private static int GetOrPromptPort(IConsoleIO io, int? currentPort, int defaultPort = 80) {
    if (currentPort.HasValue) {
      return currentPort.Value;
    }

    io.Write($"Server Port [{defaultPort}]: ");
    string? portStr = io.ReadLine()?.Trim();
    return int.TryParse(portStr, out int p) ? p : defaultPort;
  }

  private static Command CreateImportCookiesCommand(IAuthService authService, IConsoleIO io) {
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
      ImportCookiesResult result = await authService.ImportCookiesAsync(path, deleteSource, cancellationToken);

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

  private static Command CreateStatusCommand(IAuthService authService, IConsoleIO io) {
    Command statusCmd = new("status", "View authentication and storage status across all services.");

    statusCmd.SetAction(async (parseResult, cancellationToken) => {
      await ExecuteStatusAsync(authService, io, cancellationToken);
      return 0;
    });

    return statusCmd;
  }

  private static async Task ExecuteStatusAsync(IAuthService authService, IConsoleIO io, CancellationToken cancellationToken) {
    IReadOnlyList<ServiceAuthStatus> statuses = await authService.GetStatusAsync(cancellationToken);

    io.WriteLine(string.Format("{0,-16} {1,-16} {2,-18} {3}", "SERVICE", "STATUS", "STORAGE", "ACCOUNT / DETAILS"));
    io.WriteLine(new string('-', 78));

    foreach (ServiceAuthStatus status in statuses) {
      io.WriteLine(string.Format("{0,-16} {1,-16} {2,-18} {3}", status.Service, status.Status, status.Storage, status.Details));
    }
  }

  private static Command CreateLogoutCommand(IAuthService authService, IConsoleIO io) {
    Argument<string> serviceArg = new("service") {
      Description = "Service to log out of ('subsonic', 'soundcloud', 'youtube'/'ytm', or 'all').",
      Arity = ArgumentArity.ZeroOrOne
    };
    serviceArg.AcceptOnlyFromAmong("subsonic", "soundcloud", "youtube", "youtubemusic", "ytm", "all");

    Command logoutCmd = new("logout", "Remove credentials from secure storage for a streaming service.");
    logoutCmd.Arguments.Add(serviceArg);

    logoutCmd.SetAction(async (parseResult, cancellationToken) => {
      string service = parseResult.GetValue(serviceArg)?.Trim() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(service)) {
        io.WriteLine("Error: Service argument is required. Supported services: subsonic, soundcloud, youtube, all.");
        return 1;
      }

      try {
        bool removed = await authService.LogoutAsync(service, cancellationToken);
        if (removed) {
          io.WriteLine($"Logged out of {service}. Credentials removed from credential store.");
        } else {
          io.WriteLine($"No credentials found to remove for {service}.");
        }
        return 0;
      } catch (ArgumentException ex) {
        io.WriteLine($"Error: {ex.Message}");
        return 1;
      }
    });

    return logoutCmd;
  }

  private static Command CreateMigrateCommand(IAuthService authService, IConsoleIO io) {
    Option<bool> keepBackupOption = new("--keep-backup") {
      Description = "Keep plaintext backup files (.bak) on disk instead of deleting them."
    };

    Command migrateCmd = new("migrate", "Migrate plaintext passwords, tokens, and cookies to secure credential store.");
    migrateCmd.Options.Add(keepBackupOption);

    migrateCmd.SetAction(async (parseResult, cancellationToken) => {
      bool keepBackup = parseResult.GetValue(keepBackupOption);

      io.WriteLine("Scanning for plaintext credentials to migrate...");
      MigrationResult result = await authService.MigrateAsync(backupPlaintextFiles: keepBackup, cancellationToken);

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
