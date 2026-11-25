# 0002. Responses without a mapping library

- Status: Accepted
- Date: 2026-11-24

## Context

The server referenced AutoMapper 16.2.0, but no code used it. The package
added two high-severity advisories (GHSA-6vhm-j9h7-f9gh,
GHSA-gpwp-5797-mjff, warning NU1903) to every restore and a library to the
image. Entities already left the server in two hand-written ways:

- typed response records with a static `From` method, next to their
  controller (`NoteResponse.From`, `SessionResponse`, `WorkflowResponses`,
  and five more);
- the project's own `Projector` (`EchoPhase.Projection`), which copies only
  `[Expose]` properties, with includes chosen per call and cycle detection
  (users, API clients, impersonation).

## Decision

- No mapping library. AutoMapper was removed (server `7556466`).
- What goes out is written explicitly: a response record with `From`, or
  `Projector` with `[Expose]`. Both are allow-lists: a new entity property
  does not leave the server until someone adds it.
- Response records carry the `/// <param>` docs and the nullability that
  become the OpenAPI contract.

## Alternatives considered

- **Keep AutoMapper**: unused, with open advisories, and a commercial
  licence from version 15. Convention-based mapping also sends out a new
  property silently, which is the opposite of an allow-list.
- **A source-generated mapper** (Mapperly): no runtime reflection and
  compile-time errors, but for a few dozen small records a `From` method is
  as short and needs no tool.
- **Only `Projector`**: its output is untyped, so the contract needs the
  response type declared beside it; typed records give the contract and
  the code from one place.

## Consequences

- A response change is a code change in one record, visible in review and
  in `openapi:check`.
- Two ways remain. `AGENTS.md` and `CONTRIBUTING.md` still say "return
  entities through the Projector", while most controllers use records;
  the rule should be brought in line with the code (which way for new
  endpoints) in a separate change.
