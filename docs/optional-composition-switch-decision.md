# Decision: Optional Boolean Composition Switch

## Purpose

Resolve the contradiction between two versions of the startup-refactor plan:

- one version makes the boolean mode switch required;
- another version treats an absent switch as an intentional live-mode default.

The second interpretation is the required behaviour. Update the implementation plan accordingly.

This document uses generic names intentionally. Keep the revised plan free of organization, product, service, environment, and infrastructure-specific terminology.

## Final decision

Keep the boolean composition switch optional.

Its semantics are:

| Configuration state | Result |
|---|---|
| Key absent | Live mode |
| `false` | Live mode |
| `true` | Emulated mode |
| Empty, whitespace, or malformed value | Startup failure |

Absence is not a configuration error. It represents the documented default.

An invalid supplied value is a configuration error and must never silently become live mode.

## Why it should not be made required for consistency

The switch is composition-driving configuration. The host reads it before selecting which implementations to register.

Ordinary runtime options follow a different lifecycle:

- bind after the configuration pipeline is complete;
- register through `AddOptions<T>()`;
- validate through data annotations or `IValidateOptions<T>`;
- use `ValidateOnStart()`;
- consume through `IOptions<T>`.

The mode switch does not naturally fit that lifecycle because the dependency graph must already be selected before host-start validation runs.

Do not make it required merely to make every configuration value look alike. Configuration rules should follow semantics and ownership, not visual consistency.

Making the key required would be an intentional behaviour change. It would require every existing deployment, test, launch profile, and local configuration to add an explicit `false` value despite live mode already being the established default. No such migration is required for the current refactor.

## Required parsing behaviour

Read the raw scalar once so the implementation can distinguish absence from invalid input.

Use behaviour equivalent to:

```csharp
internal static bool ReadUseEmulation(IConfiguration configuration)
{
    ArgumentNullException.ThrowIfNull(configuration);

    string? configuredValue = configuration["UseEmulation"];
    if (configuredValue is null)
    {
        return false;
    }

    if (!bool.TryParse(configuredValue, out bool useEmulation))
    {
        throw new InvalidOperationException(
            "Configuration value 'UseEmulation' must be 'true' or 'false' when supplied.");
    }

    return useEmulation;
}
```

The example key is generic. Retain the application's established key in the implementation.

This parsing produces the intended distinction:

- a missing provider value returns `null` and selects live mode;
- `false` selects live mode;
- `true` selects emulated mode;
- `""` fails;
- whitespace fails;
- arbitrary text fails;
- boolean text may be parsed case-insensitively.

Do not use an API that silently converts missing, empty, or malformed input into the default value without preserving this distinction.

## Configuration classification

Classify the switch as:

```text
Optional composition-driving scalar with an explicit live-mode default
```

It is not:

- a normal runtime options object;
- an optional feature whose registration can be deferred until host start;
- a reloadable option;
- a value that should be rebound through a second options path.

## Registration flow

The composition flow should be:

1. Build the final configuration-provider pipeline.
2. Read the raw boolean switch once.
3. Treat absence as `false`.
4. Reject an invalid supplied value.
5. Register live or emulated implementations explicitly.
6. Expose the resolved mode to runtime consumers only when they need it.

If consumers require a structured value, create one from the already resolved switch:

```csharp
var composition = new HostComposition(
    UseEmulation: ReadUseEmulation(configuration));

services.AddSingleton(Options.Create(composition));
```

Do not bind the same switch again through:

```csharp
services.AddOptions<HostComposition>()
    .Bind(...)
    .ValidateOnStart();
```

That would create two materializations and would validate the second instance after the first had already selected registrations.

## Required plan correction

Remove wording equivalent to:

```text
Make the boolean switch explicit and required so missing configuration no longer
silently behaves like live mode.
```

Replace it with:

```text
Keep the boolean composition switch optional. Missing or false intentionally selects
live mode; true selects emulated mode. Read the raw scalar once before composition.
A supplied blank or malformed value fails startup instead of becoming the default.
```

Do not reintroduce a named-mode enum unless the supported domain grows beyond two states or the live default is intentionally removed in a separately approved change.

## Documentation requirements

State the default anywhere operators or developers configure the switch:

- base configuration documentation;
- deployment instructions;
- launch-profile documentation;
- local-development instructions;
- the composition parser;
- composition tests.

Use clear wording:

> When the mode-switch key is absent or set to `false`, the application uses live dependencies. Set it to `true` to use emulated dependencies. If the key is supplied with a blank or non-boolean value, startup fails.

Avoid describing missing configuration as "silently" selecting live mode. The behaviour is not accidental when it is documented, implemented explicitly, and covered by tests.

## Required tests

### Missing key

- Omit the key from all providers.
- Assert live registrations are selected.
- Assert no emulated registration is selected.
- Assert host construction succeeds when all active live configuration is valid.

### Explicit false

- Set the key to `false`.
- Assert the same live registrations as the missing-key case.

### Explicit true

- Set the key to `true`.
- Assert emulated registrations are selected.
- Assert live-only options, validators, consumers, health checks, and hosted services remain dormant.
- Prove the host starts with live-only configuration sections absent.

### Empty value

- Set the key to an empty scalar through a high-precedence provider.
- Assert startup fails with the mode-switch validation message.
- Prove the empty override does not fall back to a lower-precedence `true` or `false` value.

### Whitespace value

- Set the key to whitespace.
- Assert startup fails.

### Malformed value

- Set the key to arbitrary non-boolean text.
- Assert startup fails before affected registrations complete.
- Assert the fatal event reaches bootstrap logging.

### Provider precedence

- Supply different switch values from multiple configuration providers.
- Assert the highest-precedence supplied value controls composition.
- Include a test where the highest-precedence provider supplies an empty value and confirm that the empty value fails rather than revealing a lower-precedence value.

## Interaction with dormant settings

The optional default does not weaken validation of the active mode.

When live mode is selected by either absence or explicit `false`:

- all required live configuration is active;
- live options must be present and valid;
- live validation failures stop startup.

When emulated mode is selected by `true`:

- live-only options are not registered;
- live-only validators are not registered;
- no consumer may force live-only options to materialize;
- live-only configuration may be entirely absent.

The mode switch selects which configuration becomes active; it does not make active configuration optional.

## When making the switch required would be appropriate

Reconsider required configuration only if one of these conditions becomes true:

- live mode is no longer the safe or established default;
- the system gains more than two operating modes;
- deployment policy requires every environment to declare its mode explicitly;
- an audit requirement prohibits default composition;
- a separately approved breaking configuration migration is planned.

If that happens, treat it as a distinct configuration-contract change. Update every deployment and test deliberately. Do not hide that migration inside the current startup refactor.

## Final instruction to the agent

Revise the plan to preserve the optional boolean and its explicit live-mode default.

Do not make the key required merely because other runtime options use required sections or `ValidateOnStart()`. Continue with the remaining startup-refactor decisions after correcting this one contradiction.
