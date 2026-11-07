using System.CommandLine;

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
    });

    return authRoot;
  }

  private static Command CreateLoginCommand(IAuthService authService, IConsoleIO io) {
    Argument<string> serviceArg = new("service") {
      Description = "Streaming service to log in to (subsonic, soundcloud)."
    };

    Option<string?> hostOption = new("--host", "-h") {
      Description = "Subsonic server host."
    };
    Option<int?> portOption = new("--port", "-p") {
      Description = "Subsonic server port."
    };
    Option<string?> schemeOption = new("--scheme") {
      Description = "Subsonic server scheme (http or https)."
    };
    Option<string?> usernameOption = new("--username", "-u") {
      Description = "Subsonic username."
    };
    Option<string?> passwordOption = new("--password") {
      Description = "Subsonic password (if omitted, prompted interactively)."
    };
    Option<bool> noVerifyOption = new("--no-verify") {
      Description = "Skip verifying the Subsonic connection before saving."
    };

    Option<string?> clientIdOption = new("--client-id") {
      Description = "SoundCloud Client ID."
    };
    Option<string?> tokenOption = new("--token", "-t") {
      Description = "SoundCloud OAuth authentication token (if omitted, prompted interactively)."
    };

    Command loginCmd = new("login", "Authenticate with a streaming service and save credentials to OS keyring.");
    loginCmd.Arguments.Add(serviceArg);
    loginCmd.Options.Add(hostOption);
    loginCmd.Options.Add(portOption);
    loginCmd.Options.Add(schemeOption);
    loginCmd.Options.Add(usernameOption);
    loginCmd.Options.Add(passwordOption);
    loginCmd.Options.Add(noVerifyOption);
    loginCmd.Options.Add(clientIdOption);
    loginCmd.Options.Add(tokenOption);

    loginCmd.SetAction(async (parseResult, cancellationToken) => {
      string service = parseResult.GetValue(serviceArg)?.Trim().ToLowerInvariant() ?? string.Empty;

      switch (service) {
        case "subsonic":
          await ExecuteSubsonicLoginAsync(authService, io, parseResult, hostOption, portOption, schemeOption, usernameOption, passwordOption, noVerifyOption, cancellationToken);
          break;

        case "soundcloud":
          await ExecuteSoundCloudLoginAsync(authService, io, parseResult, clientIdOption, tokenOption, cancellationToken);
          break;

        default:
          io.WriteLine($"Unknown service '{service}'. Supported services: subsonic, soundcloud.");
          break;
      }
    });

    return loginCmd;
  }

  private static async Task ExecuteSubsonicLoginAsync(
    IAuthService authService,
    IConsoleIO io,
    ParseResult parseResult,
    Option<string?> hostOption,
    Option<int?> portOption,
    Option<string?> schemeOption,
    Option<string?> usernameOption,
    Option<string?> passwordOption,
    Option<bool> noVerifyOption,
    CancellationToken cancellationToken
  ) {
    if (!TryGetOrPromptRequired(io, parseResult.GetValue(hostOption), "Server Host: ", "Error: Server host is required.", out string host)) {
      return;
    }

    int port = GetOrPromptPort(io, parseResult.GetValue(portOption), 443);

    string? scheme = parseResult.GetValue(schemeOption);
    if (string.IsNullOrWhiteSpace(scheme)) {
      scheme = port == 443 ? "https" : "http";
    }

    if (!TryGetOrPromptRequired(io, parseResult.GetValue(usernameOption), "Username: ", "Error: Username is required.", out string username)) {
      return;
    }

    if (!TryGetOrPromptRequired(io, parseResult.GetValue(passwordOption), "Password: ", "Error: Password is required.", out string password, isSecret: true)) {
      return;
    }

    bool noVerify = parseResult.GetValue(noVerifyOption);
    if (!noVerify) {
      io.WriteLine("Connecting and verifying credentials...");
    }

    try {
      await authService.LoginSubsonicAsync(host, port, scheme, username, password, useToken: true, verifyConnection: !noVerify, cancellationToken);
      io.WriteLine("Success! Saved server settings to config.json and password to OS Keyring.");
    } catch (Exception ex) {
      io.WriteLine($"Error: {ex.Message}");
    }
  }

  private static async Task ExecuteSoundCloudLoginAsync(
    IAuthService authService,
    IConsoleIO io,
    ParseResult parseResult,
    Option<string?> clientIdOption,
    Option<string?> tokenOption,
    CancellationToken cancellationToken
  ) {
    string? clientId = GetOrPromptOptional(io, parseResult.GetValue(clientIdOption), "Client ID: ");

    if (!TryGetOrPromptRequired(io, parseResult.GetValue(tokenOption), "OAuth Auth Token: ", "Error: Authentication token is required.", out string token, isSecret: true)) {
      return;
    }

    try {
      await authService.LoginSoundCloudAsync(clientId ?? string.Empty, token, cancellationToken);
      io.WriteLine("Success! Saved SoundCloud credentials to OS Keyring.");
    } catch (Exception ex) {
      io.WriteLine($"Error: {ex.Message}");
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

  private static int GetOrPromptPort(IConsoleIO io, int? currentPort, int defaultPort = 443) {
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

    Command importCmd = new("import-cookies", "Import YouTube Music cookies into OS Keyring and generate session tokens.");
    importCmd.Arguments.Add(pathArg);
    importCmd.Options.Add(deleteOption);

    importCmd.SetAction(async (parseResult, cancellationToken) => {
      string? path = parseResult.GetValue(pathArg);
      string displayPath = string.IsNullOrWhiteSpace(path) ? AuthService.DefaultCookiesPath : path;

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
      } else {
        io.WriteLine($"Import failed: {result.Message}");
      }
    });

    return importCmd;
  }

  private static Command CreateStatusCommand(IAuthService authService, IConsoleIO io) {
    Command statusCmd = new("status", "View authentication and storage status across all services.");

    statusCmd.SetAction(async (parseResult, cancellationToken) => {
      await ExecuteStatusAsync(authService, io, cancellationToken);
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
      Description = "Service to log out of (subsonic, soundcloud, youtubemusic, or all)."
    };

    Command logoutCmd = new("logout", "Remove credentials from OS Keyring for a streaming service.");
    logoutCmd.Arguments.Add(serviceArg);

    logoutCmd.SetAction(async (parseResult, cancellationToken) => {
      string service = parseResult.GetValue(serviceArg)?.Trim() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(service)) {
        io.WriteLine("Error: Service argument is required.");
        return;
      }

      bool removed = await authService.LogoutAsync(service, cancellationToken);
      if (removed) {
        io.WriteLine($"Logged out of {service}. Credentials removed from OS Keyring.");
      } else {
        io.WriteLine($"No credentials found to remove for {service}.");
      }
    });

    return logoutCmd;
  }

  private static Command CreateMigrateCommand(IAuthService authService, IConsoleIO io) {
    Option<bool> noBackupOption = new("--no-backup") {
      Description = "Delete plaintext files directly instead of renaming to .bak."
    };

    Command migrateCmd = new("migrate", "Migrate plaintext passwords, tokens, and cookies to OS Keyring.");
    migrateCmd.Options.Add(noBackupOption);

    migrateCmd.SetAction(async (parseResult, cancellationToken) => {
      bool backup = !parseResult.GetValue(noBackupOption);

      io.WriteLine("Scanning for plaintext credentials to migrate...");
      MigrationResult result = await authService.MigrateAsync(backup, cancellationToken);

      if (result.MigratedItems.Count == 0 && result.CleanedFiles.Count == 0) {
        io.WriteLine("No plaintext credentials found. All services are already using secure storage or unconfigured.");
        return;
      }

      WriteSection(io, "Successfully migrated credentials to OS Keyring:", "-", result.MigratedItems);
      WriteSection(io, "Sanitized plaintext files:", "-", result.CleanedFiles);
      WriteSection(io, "Warnings/Errors during migration:", "!", result.Errors);
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