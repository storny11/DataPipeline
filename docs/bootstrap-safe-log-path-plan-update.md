# Bootstrap-Safe Configuration-Owned Logging Pattern

## Purpose

Use one simple Serilog flow that keeps normal sink settings visible in `appsettings.json`, captures startup failures after host configuration is available, and writes bootstrap and runtime events to the same resolved file.

This reference intentionally uses generic application terminology.

## Ownership

Configuration owns normal logging behavior:

- sink selection;
- minimum levels and category overrides;
- enrichers;
- output formatting;
- rolling, retention, size, and shared-file settings.

Code owns only:

- a console-only emergency bootstrap logger;
- the application-instance log directory;
- the resolved File sink path override;
- the bootstrap-to-final logger lifecycle.

Do not configure normal console or file sinks in both code and configuration.

## Serilog configuration shape

Give each sink a stable name instead of using array indexes:

```json
"Serilog": {
  "Using": [
    "Serilog.Sinks.Console",
    "Serilog.Sinks.File"
  ],
  "MinimumLevel": {
    "Default": "Information"
  },
  "Enrich": [
    "FromLogContext"
  ],
  "WriteTo": {
    "ConsoleSink": {
      "Name": "Console"
    },
    "FileSink": {
      "Name": "File",
      "Args": {
        "path": "logs/application-.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 14,
        "fileSizeLimitBytes": 10485760,
        "rollOnFileSizeLimit": true,
        "shared": true
      }
    }
  }
}
```

The stable runtime override key is:

```text
Serilog:WriteTo:FileSink:Args:path
```

Add the resolved path through a final in-memory configuration provider. Do not scan `WriteTo`, depend on array positions, replace arbitrary tokens, or mutate JSON files.

## Generic launch argument

Use an optional `instance` command-line argument to isolate deployed application logs:

| Launch shape | `instance` value | Log directory | Result |
|---|---|---|---|
| Development | Absent | Fixed log root | Continue |
| Development | Valid | Fixed log root plus instance | Continue |
| Deployed | Valid | Fixed log root plus instance | Continue |
| Deployed | Absent | Fixed log root fallback | Log fatally and fail |
| Any | Blank or unsafe | Fixed log root fallback | Log fatally and fail |

Read command-line pairs with .NET's `AddCommandLine` provider. Do not maintain a custom argument parser.

Treat the instance as one directory segment. Reject:

- empty or whitespace-only values;
- surrounding whitespace;
- `.` and `..`;
- rooted paths;
- either directory separator;
- invalid filename characters.

Return the fallback root path together with any validation error. This lets the configured bootstrap logger start before the application rejects the launch.

## Startup order

Use this order:

1. Create a console-only reloadable bootstrap logger.
2. Create the application builder once.
3. Resolve the generic instance path from command-line configuration and the builder environment.
4. Add the resolved File sink path as an in-memory override.
5. Reload the bootstrap logger from `builder.Configuration`.
6. Throw a delayed launch-validation error inside the top-level `try` block.
7. Register services and configure final Serilog using the same configuration instance.
8. Build and run the host.
9. Log fatal failures once, rethrow, and flush in `finally`.

If configuration-based logging cannot initialize, retain the console bootstrap logger and configure the final logger with the same console-only fallback. Do not create a second startup-log family.

## Final logger

The final logger should only add host-aware capabilities:

```csharp
loggerConfiguration
    .ReadFrom.Configuration(configuration)
    .ReadFrom.Services(services);
```

It must not add normal sinks again. The bootstrap reload and final logger read the same named configuration and therefore share the same concrete file path.

## Verification

Tests should prove:

- Development without `instance` uses the root log path;
- deployed startup without `instance` selects the root fallback and fails validation;
- common command-line forms are accepted by the built-in provider;
- blank and unsafe values select the fallback and fail;
- duplicate values use the provider's last-value-wins behavior;
- the named File sink receives the resolved path override;
- bootstrap and final events appear in the same file;
- configuration-owned sinks are not duplicated in code;
- configuration logging failure leaves a usable console fallback.
