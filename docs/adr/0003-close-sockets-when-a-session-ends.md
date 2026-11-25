# 0003. Close a session's sockets when it ends, with close code 4000

- Status: Accepted
- Date: 2026-11-16

## Context

A gateway connection is authenticated once, when it opens. A test of the
old code showed what happened after a logout or `DELETE /sessions/{id}`:

- the sockets stayed open until the next pass of
  `RealtimeCredentialMonitor`, up to 30 s;
- the monitor then closed them with 1008, which the webapp treats as "try
  again", so it reconnected;
- logout and the revoke of one session changed the user's security stamp,
  so the sockets of every other device closed too.

## Decision

- Session access tokens carry the session id in the `sid` claim.
  `JwtTokenProvider` accepts a token with `sid` only while that session
  exists.
- Logout and the revoke of one session no longer change the security
  stamp; only "sign out everywhere" and refresh-token reuse do.
- When a session ends, `ISessionEvents.EndedAsync` publishes a close order
  on the Redis channel `gateway:closes`, and every replica closes that
  session's connections at once.
- The close code is 4000, "Session ended": the client must not reconnect.
  The monitor uses 4000 for an ended session and keeps 1008 for other
  failures.

## Alternatives considered

- **Only the monitor checks the session**: simpler, but a socket would
  still live up to 30 s after a logout.
- **Shorter monitor interval**: more load on every connection, and the
  delay only shrinks.
- **Keep changing the security stamp**: correct for "everywhere", wrong for
  one device: it signed out the user's other sessions.
- **Close only on the replica that served the logout**: the socket may be
  on another node; the backplane is needed.

## Consequences

- A logout closes that session's sockets immediately on every replica, and
  the other devices stay connected.
- Clients must treat 4000 as signed out. The webapp does it through the
  `/auth/me` call after its reconnect backoff; ending the session on 4000
  at once is a small open improvement.
- Tokens without `sid` (API clients, impersonation, tokens from before the
  change) keep the old checks.
- During a rolling deploy an old replica does not listen on
  `gateway:closes`; its monitor still closes the sockets within 30 s.
