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

    authRoot.SetAction(async (parseResult) => {
      await ExecuteStatusAsync(service, io, CancellationToken.None);
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

    loginCmd.SetAction(async (parseResult) => {
      string service = parseResult.GetValue(serviceArg)?.Trim().ToLowerInvariant() ?? string.Empty;

      switch (service) {
        case "subsonic": {
          string? host = parseResult.GetValue(hostOption);
          if (string.IsNullOrWhiteSpace(host)) {
            io.Write("Server Host: ");
            host = io.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(host)) {
              io.WriteLine("Error: Server host is required.");
              return;
            }
          }

          int? port = parseResult.GetValue(portOption);
          if (port == null) {
            io.Write("Server Port [443]: ");
            string? portStr = io.ReadLine()?.Trim();
            port = int.TryParse(portStr, out int p) ? p : 443;
          }

          string? scheme = parseResult.GetValue(schemeOption);
          if (string.IsNullOrWhiteSpace(scheme)) {
            scheme = port == 443 ? "https" : "http";
          }

          string? username = parseResult.GetValue(usernameOption);
          if (string.IsNullOrWhiteSpace(username)) {
            io.Write("Username: ");
            username = io.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(username)) {
              io.WriteLine("Error: Username is required.");
              return;
            }
          }

          string? password = parseResult.GetValue(passwordOption);
          if (string.IsNullOrWhiteSpace(password)) {
            password = io.ReadPassword("Password: ");
            if (string.IsNullOrWhiteSpace(password)) {
              io.WriteLine("Error: Password is required.");
              return;
            }
          }

          bool noVerify = parseResult.GetValue(noVerifyOption);
          if (!noVerify) {
            io.WriteLine("Connecting and verifying credentials...");
          }

          try {
            await authService.LoginSubsonicAsync(host, port.Value, scheme, username, password, useToken: true, verifyConnection: !noVerify, CancellationToken.None);
            io.WriteLine("Success! Saved server settings to config.json and password to OS Keyring.");
          } catch (Exception ex) {
            io.WriteLine($"Error: {ex.Message}");
          }
          break;
        }

        case "soundcloud": {
          string? clientId = parseResult.GetValue(clientIdOption);
          if (string.IsNullOrWhiteSpace(clientId)) {
            io.Write("Client ID: ");
            clientId = io.ReadLine()?.Trim();
          }

          string? token = parseResult.GetValue(tokenOption);
          if (string.IsNullOrWhiteSpace(token)) {
            token = io.ReadPassword("OAuth Auth Token: ");
            if (string.IsNullOrWhiteSpace(token)) {
              io.WriteLine("Error: Authentication token is required.");
              return;
            }
          }

          try {
            await authService.LoginSoundCloudAsync(clientId ?? string.Empty, token, CancellationToken.None);
            io.WriteLine("Success! Saved SoundCloud credentials to OS Keyring.");
          } catch (Exception ex) {
            io.WriteLine($"Error: {ex.Message}");
          }
          break;
        }

        default:
          io.WriteLine($"Unknown service '{service}'. Supported services: subsonic, soundcloud.");
          break;
      }
    });

    return loginCmd;
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

    importCmd.SetAction(async (parseResult) => {
      string? path = parseResult.GetValue(pathArg);
      if (string.IsNullOrWhiteSpace(path)) {
        path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc", "cookies.txt");
      }

      bool deleteSource = parseResult.GetValue(deleteOption);
      if (!deleteSource && !io.IsInputRedirected) {
        deleteSource = io.Confirm("Remove plaintext cookies file after import?", defaultValue: true);
      }

      io.WriteLine($"Importing cookies from {path}...");
      ImportCookiesResult result = await authService.ImportCookiesAsync(path, deleteSource, CancellationToken.None);

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

    statusCmd.SetAction(async (_) => {
      await ExecuteStatusAsync(authService, io, CancellationToken.None);
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

    logoutCmd.SetAction(async (parseResult) => {
      string service = parseResult.GetValue(serviceArg)?.Trim() ?? string.Empty;
      if (string.IsNullOrWhiteSpace(service)) {
        io.WriteLine("Error: Service argument is required.");
        return;
      }

      bool removed = await authService.LogoutAsync(service, CancellationToken.None);
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

    migrateCmd.SetAction(async (parseResult) => {
      bool backup = !parseResult.GetValue(noBackupOption);

      io.WriteLine("Scanning for plaintext credentials to migrate...");
      MigrationResult result = await authService.MigrateAsync(backup, CancellationToken.None);

      if (result.MigratedItems.Count == 0 && result.CleanedFiles.Count == 0) {
        io.WriteLine("No plaintext credentials found. All services are already using secure storage or unconfigured.");
        return;
      }

      if (result.MigratedItems.Count > 0) {
        io.WriteLine("Successfully migrated credentials to OS Keyring:");
        foreach (string item in result.MigratedItems) {
          io.WriteLine($"  - {item}");
        }
      }

      if (result.CleanedFiles.Count > 0) {
        io.WriteLine("Sanitized plaintext files:");
        foreach (string file in result.CleanedFiles) {
          io.WriteLine($"  - {file}");
        }
      }

      if (result.Errors.Count > 0) {
        io.WriteLine("Warnings/Errors during migration:");
        foreach (string err in result.Errors) {
          io.WriteLine($"  ! {err}");
        }
      }
    });

    return migrateCmd;
  }
}
