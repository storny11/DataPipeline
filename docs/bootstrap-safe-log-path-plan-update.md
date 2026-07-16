# Bootstrap-Safe Configuration-Owned Logging Pattern

## Purpose

Use one simple Serilog flow that keeps normal sink settings visible in `appsettings.json`, captures failures before host configuration is available, and writes bootstrap and runtime events to the same resolved file.

This reference intentionally uses generic application terminology.

## Ownership

Configuration owns normal logging behavior:

- sink selection;
- minimum levels and category overrides;
- enrichers;
- output formatting;
- rolling, retention, size, and shared-file settings.

Code owns only:

- a console emergency bootstrap logger;
- a temporary bootstrap file sink at the already resolved normal application path;
- the application-instance log directory;
- the resolved File sink path override;
- the bootstrap-to-final logger lifecycle.

The short-lived bootstrap phase defines its minimal sinks in code. The final phase defines its normal sinks in configuration. Never add the same sink twice within either phase.

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

Resolve a safe path first. If the instance value is missing or unsafe, use the fixed root path. Validate the same launch arguments separately after file logging is active, then reject an invalid launch. No result/context object is needed.

## Startup order

Use this order:

1. Create a console-only reloadable bootstrap logger.
2. Resolve the generic instance path from primitive launch arguments and enable the same application file.
3. Let file initialization throw if the directory or sink cannot be opened. The process must not continue with console-only logging when console output is not preserved.
4. Resolve and validate other fallible bootstrap inputs, including the host environment and instance argument.
5. Create the application builder once and add custom configuration providers.
6. Add the already resolved File sink path as an in-memory override.
7. Validate that the completed configuration still contains the named File sink.
8. Register services and configure final Serilog from the completed configuration.
9. Build and run the host.
10. Log fatal failures once, rethrow, and flush in `finally`.

## Final logger

The final logger should only add host-aware capabilities:

```csharp
loggerConfiguration
    .ReadFrom.Configuration(configuration)
    .ReadFrom.Services(services);
```

It must not add normal sinks again in code. The final logger reads the named configuration and the injected path; its file sink continues in the same rolling file used by the bootstrap phase.

## Verification

Tests should prove:

- Development without `instance` uses the root log path;
- deployed startup without `instance` selects the root fallback and fails validation;
- common command-line forms are accepted by the built-in provider;
- blank and unsafe values select the fallback and fail;
- duplicate values use the provider's last-value-wins behavior;
- the named File sink receives the resolved path override;
- failures before the full configuration pipeline exists reach the bootstrap-safe application file;
- bootstrap and final events appear in the same file;
- configuration-owned sinks are not duplicated in code;
- failure to initialize durable file logging fails startup instead of silently continuing.
