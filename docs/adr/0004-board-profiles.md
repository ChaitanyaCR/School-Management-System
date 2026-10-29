# ADR-0004: Board-specific rules as versioned data packs

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

CBSE is supported first, but customers may follow CISCE, state boards, IB or Cambridge. Boards differ in classes, subjects, assessment patterns, grading, pass rules and report-card formats.

## Decision

- A **board profile** is a versioned folder of YAML and HTML templates under `Modules/Boards/Profiles/{board}/`, embedded in the assembly and validated at startup.
- Applying a profile **copies** its data into the organisation as editable records tagged with the profile version. Later profile updates never overwrite an organisation's customisations silently.
- Rules that configuration can't express are implemented as an `IResultRuleStrategy`, registered as a .NET keyed service under the board code.
- Report-card and certificate templates use Liquid (Fluid), which is sandboxed and safe for future user-edited templates.

## Consequences

- Adding a board means adding a folder and tests, with no change to core code.
- The result engine must be written against configuration, not CBSE assumptions.
- Profile schema changes need a migration path for organisations that already applied an older version.
