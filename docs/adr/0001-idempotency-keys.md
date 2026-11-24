# 0001. Idempotency keys for incoming webhooks and for creating requests

- Status: Proposed
- Date: 2026-11-24

## Context

No request of the API is idempotent at the HTTP level today. A repeated
request is a new request.

That is harmless for most of the API:

- Updates (PUT) carry the entity's `expectedStamp`: a repeat either changes
  nothing or gets a version conflict.
- A repeated DELETE gets 404.
- Register is guarded by the unique user name; marking notifications read
  is idempotent by nature.
- The webapp does not retry mutations (`retry: false`), and `createForm`
  does not send a form while it is busy.

It is not harmless in two places:

1. **Incoming webhooks** (`POST /api/v1/hooks/{workflowId}/{blockId}`).
   The senders (GitHub, payment services, chat platforms) deliver at least
   once: on a timeout or a 5xx they send again. A webhook whose block waits
   for the program's reply holds the request for up to the block timeout
   plus 30 s, while senders give up much sooner (GitHub after 10 s). The
   sender retries, and the workflow runs twice: a message is sent twice, a
   record is made twice.
2. **Requests of API clients that create or start something**: a manual
   run, a workflow import, a package install, a note, a file upload. A
   script retries after a network error without knowing whether the first
   request arrived; if the server did the work and the answer was lost, the
   retry does it again.

## Decision

- **One mechanism**: an `[Idempotent]` attribute on the actions that need
  it, and a filter that implements it.
  - The client sends `Idempotency-Key` (the de-facto standard: Stripe, the
    IETF draft `draft-ietf-httpapi-idempotency-key-header`). The header is
    optional: without it a request works as today.
  - The filter keeps in Valkey, for about 24 hours: the key scoped by user
    (or webhook) and route, a hash of the body, and the stored response
    (status and body).
  - A repeat with the same key and body gets the stored response; the
    action does not run again.
  - The same key with a different body gets 422.
  - A repeat while the first request still runs gets 409, so that two
    concurrent requests never both run.
- **Webhooks**: senders rarely send `Idempotency-Key`; they send their own
  delivery id (`X-GitHub-Delivery`, `webhook-id` of Standard Webhooks /
  Svix, Stripe's event id). The `WebhookTrigger` block gets a parameter,
  the header that carries the delivery id (default `Idempotency-Key`). A
  repeated delivery gets the same `runId` and starts no run.
- **Order**: webhooks first (duplicates happen there in normal operation,
  not only on network faults), then the manual run, then install and
  import, then notes and file uploads.
- The header goes into the OpenAPI contract of these actions; an optional
  header is a compatible change.

## Alternatives considered

- **Nothing**: duplicates of webhook runs stay a normal event, and scripts
  must check for duplicates themselves.
- **Dedupe only by the body hash**: two different events with the same
  body (a ping, an identical form) would be dropped as one.
- **Natural keys per entity** (a client-chosen id for each created item):
  works for some entities, not for runs, and needs a contract change for
  each.
- **A database table instead of Valkey**: durable, but the keys live only a
  day and are read on every protected request; Valkey with a TTL fits, and
  it is already shared between server instances.

## Consequences

- The protected actions need their responses to be storable (JSON, a size
  limit; a file download is never one of them).
- A key is scoped by user and route, so two users or two webhooks never
  share keys.
- Valkey loss loses the keys: a retry after that runs again, as today.
- Tests: a repeat returns the stored response and runs nothing; a
  different body gets 422; a concurrent repeat gets 409; a webhook repeat
  with the same delivery id returns the same run.
- Later the webapp's `createSubmit` can send a key for each submit attempt
  and keep it for its retries, which covers a lost response in the UI.
