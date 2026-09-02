# AGENTS.md — AI-Collaboration Principles & Debugging Checklist

## Principles

1. **Hard constraints go in code, not prompts.** Validate against a known-good
   set (enum values, FKs, output schema) and hard-fail on mismatch — never try
   to salvage or guess.
2. **Split retries by layer.** Transport failures (timeout/5xx) → standard
   retry/backoff. Content failures (200 OK, bad payload) → a separate retry
   step, decoupled from final-failure handling. Keep "fetch+validate" and
   "persist" in separate try blocks — only the former should retry.
3. **Concurrency needs DB-native locking, not in-memory checks.** A
   single-process state check only stops sequential misuse, not races.
   Prefer a DB optimistic-concurrency token; translate its exception to a
   business error at the infra boundary.
4. **Isolation should be structural.** If a module shouldn't touch certain
   data, don't inject that dependency at all — a comment or convention will
   eventually be bypassed.
5. **Never trust derivable input.** Recompute anything the server can derive
   (counts, checksums, aggregates) instead of accepting it from the caller.
6. **State units explicitly, and clamp on both ends.** For any numeric
   contract (0–100 vs 0–1, etc.), migrate old data when the unit changes AND
   add a defensive clamp downstream.
7. **Empty string ≠ NULL for JSON/JSONB.** Normalize blank → NULL before
   writing, even if validation allows `""`.
8. **Migrations are additive by default.** Dropping a column/table is an
   exception that must be justified in writing, and destructive scripts must
   run in the correct order (migrate data, then drop).
9. **Version config/prompt templates instead of overwriting.** Add a new
   version + deactivate the old one; update seed scripts to match, or fresh
   environments will reproduce old bugs.
10. **Test fakes need boundary values.** A too-short/simple fake payload
    hides overflow and size-limit bugs — add at least one edge-case fixture.
11. **Don't re-`new` non-value-equal config objects per request.** Framework
    DI/caching may treat it as "config changed" and cause bizarre intermittent
    failures. Use `static readonly` for stateless config.
12. **Port/service collisions fail silently.** Local vs. containerized
    services on the "same" port can silently connect to the wrong instance —
    symptoms look unrelated (e.g. auth errors).
13. **Use an unlinked cancellation token in failure-handling paths**, since
    the original request being cancelled is often *why* the failure happened.

## Reusable Component Map

| Symptom | Look for | Key trait |
|---|---|---|
| AI response fails to parse | Content-validation retry executor | Only decides retry-or-not; doesn't own failure policy |
| Concurrent write conflict | Optimistic concurrency token + exception translator | DB-native token; translation at infra boundary |
| Rule/copy needs hot updates | Versioned config table | New version row, deactivate old — never in-place UPDATE |
| Value must be consistent across write paths | Single shared calculator util | All paths call it; never trusts external input |
| Audit trail must not be edited | Append-only status transition | Add terminal state (e.g. "deprecated"), never delete |
| Mixed rule + AI classification | Rule-first, AI-assisted classifier | Classifier must never throw; falls back to rule silently |
| Backfilling old data | Idempotent one-shot script | `ON CONFLICT DO NOTHING/UPDATE`, safely re-runnable |

## Debugging Checklist

1. Transport failure or content failure — are they handled in the same catch?
2. Is the failure-path cancellation token tied to the original request?
3. Is this field caller-supplied when it should be server-computed?
4. Does this number have an explicit, matching unit on both ends?
5. Is a JSON/JSONB write failing because `''` was sent instead of `NULL`?
6. Does the test fake even reach the size/length that triggers this bug?
7. Could two requests read the same stale state before either commits?
8. Did the seed/bootstrap script get updated along with this template change?
9. Could this be a local-vs-container port collision?
10. Is a stateless object being re-`new`'d inside a per-request callback?
11. Does this destructive change touch real data — should it be versioned
    instead, and is the script order safe?
12. Is this isolation guarantee structural, or just convention that a future
    change could silently break?

---
Keep this file general. Project-specific names, tables, and SQL belong in
that project's own docs, not here.