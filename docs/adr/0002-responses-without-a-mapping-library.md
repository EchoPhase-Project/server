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
- The rule (2026-11-25): every response body is a `sealed record` with
  `From`, declared with `[ProducesResponseType<T>]`. `ResponseTypeTests`
  enforces it for new actions; 38 older actions are listed there and may
  only leave the list (the 30 bodies without a type, and actions that do
  not declare their success status at all). `Projector` and `[Expose]`
  are on their way out.

## Growth of the API (2026-11-25)

The rule was checked against how the API grew. Until September 2026 the
server had 10 controllers and 27 actions, no response records, and
`Projector` in two places. In October it grew to 28 controllers and 107
actions in one month; the first response records arrived on 2026-10-25,
the day after the contract check (`openapi:check`) went into CI, and by
then there were 30 records and about 20 `From` methods. The plans ahead
(chats, package environments and secrets, an MCP server, split workers)
will add more endpoints at a similar pace.

The failure seen so far is not the cost of writing `From`: it is responses
that never got a type. 30 of the 107 operations have a success body that
is not in the contract (Projector output, anonymous objects, raw values),
and the web client writes types for some of them by hand; one of those
(`CreatedApiClient`) already disagrees with the server. A mapping library
would not have prevented that; a test that requires a typed response does.

So hand-written `From` stays the way. Revisit a source-generated mapper
(Mapperly) only if the records reach the hundreds or many lists need
projections into SQL: about a third of the current `From` methods hold
access rules or computed fields that a generator would not write anyway.
