# Cloud Servers and Stable Server Identity — Implementation Plan

## Architectural target

Every server has two distinct concepts:

- **Server ID:** Immutable internal identity, preferably a strongly typed `ServerId` backed by a `Guid`.
- **Hostname:** Mutable, unique, player-facing name used by terminal commands and scripting APIs.

The ID is used for internal relationships and must remain stable when the hostname changes. Hostnames remain the normal public interface; players and scripts should not need to know about internal IDs.

Cloud servers will be ordinary `Server` instances with an explicit `IsCloudServer` marker, not a separate server entity type.

## Implementation sequence

### Step 1 — Introduce stable server identity

- Add an immutable `ServerId` value object.
- Give every server an immutable ID.
- Keep hostname-based lookup as a convenient player-facing operation.
- Add ID-based lookup and use IDs for new internal references.
- Preserve hostname uniqueness and reject duplicate registrations.
- Keep the change scoped to the domain foundation; avoid combining it with cloud-server behavior.

**Acceptance criteria**
- Every server has a unique, immutable ID.
- Existing network operations using hostnames continue to work.
- ID-based lookup is available.
- Duplicate hostnames are rejected.
- Existing tests remain green.

### Step 2 — Refactor internal server references

Move internal relationships away from mutable hostnames, including:

- Network connections.
- Virtual filesystem ownership/resolution.
- Script process host identity.
- Script execution context and process management.
- Internal service relationships that currently retain hostnames.

Public-facing commands and scripting APIs continue accepting hostnames where appropriate. A running script remains associated with the same server if its hostname changes; querying its current hostname should resolve the latest name from its stable ID.

**Acceptance criteria**
- No internal server relationship depends on a mutable hostname.
- Processes remain associated with their original server across renames.
- Files, RAM allocations and network relationships remain intact.
- Existing terminal and scripting behavior remains compatible.

### Step 3 — Persistence and save migration

Persist server IDs and convert persisted internal references to IDs where needed. Introduce a migration from the current save format (v5), subject to verification of the current version when implementation begins.

Migration should assign IDs to existing servers, preserve hostnames and game state, and mark all existing servers as non-cloud servers.

**Acceptance criteria**
- Existing v5 saves load correctly.
- Saving and reloading preserves server IDs.
- Network topology, files and running processes survive migration and round trips.
- No existing game state is lost.

### Step 4 — Implement server renaming

Add a hostname-change operation with existing hostname validation and uniqueness rules. Update any hostname indexes atomically while preserving the server's identity and state.

Expose renaming initially for cloud servers only. Stable IDs mean running scripts need not block a rename.

**Acceptance criteria**
- A cloud server can be renamed while scripts are running.
- Processes, files, RAM usage, connections and server state remain unchanged.
- New terminal and scripting lookups use the new hostname.

### Step 5 — Implement cloud-server domain functionality

Add `CloudServerService`, `CloudServerFormulas` and configuration in `BalanceConfig`.

Initial configuration:
- Maximum cloud servers: 5.
- RAM tiers: 8, 16, 32, 64, 128 and 256 GB.
- Starting tier: 8 GB.
- CPU cores are not implemented in this iteration, but the design should not preclude them.

Cloud servers are regular servers with an explicit marker. They have zero money, minimum security, immediate root access, are discoverable, and connect directly to `home`. They support normal server operations and script execution.

Purchases and upgrades use configurable pricing formulas. RAM upgrades are domain operations that validate the new maximum and ensure existing RAM allocation remains valid.

**Acceptance criteria**
- Purchasing creates a fully functional server.
- Configured limits and prices are enforced.
- Upgrades preserve server state.
- Failed operations do not charge the player.

### Step 6 — Add terminal commands

Add a `cloud` command group:

```text
cloud
cloud list
cloud buy <hostname> <ram>
cloud upgrade <hostname> <ram>
cloud rename <hostname> <newHostname>
```

Commands remain thin interfaces over `CloudServerService`; business logic is not duplicated in the console layer.

### Step 7 — Add the cloud scripting sub-API

Follow existing botnet API conventions and expose cloud functionality through `nb.Cloud`, including server listing/lookup, purchase and upgrade cost queries, purchase, upgrade and rename.

Exact DTOs, result types, validation and RAM-cost behavior must follow the existing scripting API conventions. The API delegates to `CloudServerService`.

### Step 8 — Integration testing, documentation and verification

Cover stable identity, hostname changes, network relationships, running processes, persistence and migration, cloud purchases/upgrades, terminal commands, scripting API registration and RAM accounting.

Update architecture, codebase, game design and roadmap documentation as appropriate. Run the full test suite, formatting and static checks, and verify CI.

## Working strategy and constraints

- Work on feature branch `feature/stable-server-identity`.
- Use a CI/test checkpoint after each meaningful step.
- Keep the identity refactoring separate from cloud-server behavior.
- Design with future CPU cores in mind, but do not implement them now.
- Do not merge into `master` without explicit user approval.
