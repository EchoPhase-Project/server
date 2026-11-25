# Architecture decision records

Each file here records one decision about the server: the situation, what
we chose, what we gave up, and what follows from it. The rules themselves
are in [docs/contributing/](../contributing/); a record explains *why* a
rule is the way it is, so that the reasoning is not lost and the decision is
not argued again from scratch.

Records are numbered in order and never renumbered. A decision that is
replaced is not deleted: its status changes to "Superseded by NNNN", and the
new record links back to it.

To add one, copy [template.md](template.md) to the next number, for example
`NNNN-short-title.md`, and add it to the list below. Write in plain, clear
English.

| No. | Decision | Status |
| --- | --- | --- |
| [0001](0001-idempotency-keys.md) | Idempotency keys for incoming webhooks and for creating requests | Proposed |
| [0002](0002-responses-without-a-mapping-library.md) | Responses without a mapping library | Accepted |
| [0003](0003-close-sockets-when-a-session-ends.md) | Close a session's sockets when it ends, with close code 4000 | Accepted |
