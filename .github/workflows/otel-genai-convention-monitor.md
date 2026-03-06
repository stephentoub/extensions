---
description: >
  Monitors the OpenTelemetry GenAI semantic conventions for changes and evaluates
  whether they impact the Microsoft.Extensions.AI OpenTelemetry implementations.
  If changes are detected, creates a PR to implement the updates.

on:
  schedule: weekly
  workflow_dispatch:

permissions:
  contents: read
  issues: read
  pull-requests: read

tools:
  web-fetch:
  github:
    toolsets: [repos, issues, pull_requests]
  cache-memory:

network:
  allowed:
    - defaults
    - dotnet
    - github

safe-outputs:
  create-pull-request:
    max: 1
  create-issue:
    max: 1
---

# OTel GenAI Semantic Convention Monitor

You are tasked with monitoring the [OpenTelemetry GenAI Semantic Conventions](https://github.com/open-telemetry/semantic-conventions/tree/main/docs/gen-ai) for changes and evaluating their impact on the Microsoft.Extensions.AI library in this repository.

## Context

This repository (`stephentoub/extensions`) contains implementations of the OpenTelemetry GenAI semantic conventions in the `Microsoft.Extensions.AI` library. The key implementation files are:

### Source Files
- `src/Libraries/Microsoft.Extensions.AI/OpenTelemetryConsts.cs` — Defines all semantic convention constants (attribute names, metric names, bucket boundaries)
- `src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryChatClient.cs` — Chat completion telemetry client
- `src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryImageGenerator.cs` — Image generation telemetry client
- `src/Libraries/Microsoft.Extensions.AI/Embeddings/OpenTelemetryEmbeddingGenerator.cs` — Embedding generation telemetry
- `src/Libraries/Microsoft.Extensions.AI/SpeechToText/OpenTelemetrySpeechToTextClient.cs` — Speech-to-text telemetry client

### Builder Extensions
- `src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryChatClientBuilderExtensions.cs`
- `src/Libraries/Microsoft.Extensions.AI/ChatCompletion/OpenTelemetryImageGeneratorBuilderExtensions.cs`
- `src/Libraries/Microsoft.Extensions.AI/Embeddings/OpenTelemetryEmbeddingGeneratorBuilderExtensions.cs`
- `src/Libraries/Microsoft.Extensions.AI/SpeechToText/OpenTelemetrySpeechToTextClientBuilderExtensions.cs`

### Test Files
- `test/Libraries/Microsoft.Extensions.AI.Tests/ChatCompletion/OpenTelemetryChatClientTests.cs`
- `test/Libraries/Microsoft.Extensions.AI.Tests/Image/OpenTelemetryImageGeneratorTests.cs`
- `test/Libraries/Microsoft.Extensions.AI.Tests/Embeddings/OpenTelemetryEmbeddingGeneratorTests.cs`
- `test/Libraries/Microsoft.Extensions.AI.Tests/SpeechToText/OpenTelemetrySpeechToTextClientTests.cs`

## Your Task

### Step 1: Fetch the Latest Semantic Conventions

Use `web-fetch` to retrieve the current OpenTelemetry GenAI semantic convention documents from:

- https://raw.githubusercontent.com/open-telemetry/semantic-conventions/main/docs/gen-ai/gen-ai-spans.md
- https://raw.githubusercontent.com/open-telemetry/semantic-conventions/main/docs/gen-ai/gen-ai-metrics.md
- https://raw.githubusercontent.com/open-telemetry/semantic-conventions/main/docs/gen-ai/gen-ai-events.md

Also check any additional files in the `docs/gen-ai/` directory by fetching the directory listing from:

- https://github.com/open-telemetry/semantic-conventions/tree/main/docs/gen-ai

### Step 2: Check for Changes Since Last Run

Read from `cache-memory` to find the previous run's state. Look for a file named `last-check-state.json` that tracks:
- The last commit SHA checked from the `open-telemetry/semantic-conventions` repo
- A summary of the conventions as they were understood at last check
- Any previously identified pending changes

If no cache exists, this is the first run — proceed with a full comparison.

Use the GitHub tool to check the latest commits on the `open-telemetry/semantic-conventions` repository's `main` branch for changes in the `docs/gen-ai/` path. Compare against the cached SHA to determine if there are new changes.

### Step 3: Analyze Impact on This Repository

If changes are detected in the semantic conventions, read the current implementation files in this repository and compare them against the latest conventions. Look for:

1. **New attributes** — Attributes defined in the conventions that are not present in `OpenTelemetryConsts.cs`
2. **Renamed attributes** — Attributes that have been renamed in the conventions
3. **Deprecated attributes** — Attributes marked as deprecated in the conventions that are still used
4. **New metrics** — New metric definitions not yet implemented
5. **Changed metric specifications** — Changes to bucket boundaries, units, or descriptions
6. **New semantic conventions for operations** — New operation types (e.g., new GenAI operation names)
7. **Behavioral changes** — Changes to how attributes should be recorded (e.g., conditional recording rules)

### Step 4: Take Action Based on Findings

#### If No Changes Detected
Update the cache-memory with the current state and exit. Write to `last-check-state.json` with:
- Current commit SHA from `open-telemetry/semantic-conventions`
- Timestamp of check (use filesystem-safe format `YYYY-MM-DD-HH-MM-SS`)
- Status: "no-changes"

#### If Changes Detected but No Impact
Update the cache-memory noting the convention changes but that they don't affect the current implementation. Write to `last-check-state.json` with:
- Current commit SHA
- Timestamp
- Status: "changes-no-impact"
- Brief summary of what changed

#### If Changes Detected with Impact
1. **Create a detailed issue** describing the convention changes and their impact on the implementation using `create-issue` safe output. Include:
   - Which convention documents changed
   - What specific attributes/metrics/behaviors are affected
   - Which source files need to be updated
   - A proposed implementation plan

2. **Create a PR with the necessary code changes** using `create-pull-request` safe output:
   - Update `OpenTelemetryConsts.cs` with new/changed constants
   - Update the relevant `OpenTelemetryXxClient` files to use new attributes/metrics
   - Update corresponding test files
   - Include a clear description of what changed in the conventions and how the code was updated

3. Update cache-memory with:
   - Current commit SHA
   - Timestamp
   - Status: "changes-pr-created"
   - PR number and issue number created

## Important Guidelines

- **Preserve backward compatibility** — When attributes are renamed, consider supporting both old and new names during a transition period.
- **Follow existing code patterns** — Match the coding style and patterns used in the existing OpenTelemetry implementation files.
- **Be conservative** — Only implement changes that are clearly specified in the stable (non-experimental) parts of the semantic conventions, unless the existing code already implements experimental conventions.
- **Test changes** — Ensure any code modifications include corresponding test updates.
- **Use filesystem-safe timestamps** — Format as `YYYY-MM-DD-HH-MM-SS` (no colons, no `T`, no `Z`) when writing to cache-memory.
