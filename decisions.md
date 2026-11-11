# Design decisions

The main choices behind Tally, why we made them, and what we gave up.

---

**Why:** Money needs to be exact and explainable. An LLM can misread or "round" a number; deterministic code checks
every class combination and can show *why* it chose a match. The owner stays in control of their books. And a
screenshot isn't proof the money arrived: only the owner can check the bank, so the owner gives the final yes.

---

## 1. AI reads, code decides, a human approves

**Decision:** The AI only reads payment screenshots (and drafts reminder text). Matching, money math and every
payment change are plain C# — and nothing counts until the owner approves.

**Why:** Money needs to be exact and explainable. An LLM can misread or "round" a number; deterministic code checks
every class combination and can show *why* it chose a match. The owner stays in control of their books.

**Trade-off:** One click of human approval per batch, instead of fully automatic recording. For a small business,
trust is worth that click.

---

## 2. PayPal stays the source of truth

**Decision:** When the owner approves an outside payment (Zelle, check, cash), Tally records it on the family's
real PayPal invoice and reads PayPal's status back. Undo removes it from PayPal first.

**Why:** Owners and parents already look at PayPal. If Tally and PayPal disagree, people stop trusting both. Writing
to PayPal (not just to Tally's database) keeps one consistent picture.

**Trade-off:** Every approval and undo makes a PayPal API call, so PayPal errors must be handled (retry button,
"not noted yet" state). Undo checks PayPal first: if PayPal says no, nothing changes in Tally.

---

## 3. The AI sees as little as possible

**Decision:** The receipt reader gets only the image; it never sees the families list. Finding the family happens
in code, using known payer names (aliases) from past payments. The reminder writer sees only the one family it is
writing to.

**Why:** Less personal data sent to the AI, smaller and cheaper prompts, and no chance of the AI "inventing" a
family match.

**Trade-off:** A brand-new sender with no history can't be matched automatically; the owner picks the family.

---

## 4. Webhooks through a queue, processed once

**Decision:** PayPal webhooks are verified (signature), stored, and queued through Azure Service Bus (with
dead-lettering after 5 failed tries). Each PayPal event ID is processed only once. An in-memory queue is used when
Service Bus isn't configured.

**Why:** PayPal may resend events, and the app may be busy or restarting. The queue means no event is lost, and the
"once only" check means no payment is counted twice.

**Trade-off:** One more moving part (Service Bus). The in-memory fallback keeps local development simple.

---

## 5. Money is exact, and concurrent edits are safe

**Decision:** All money is `decimal(18,2)`; payments fill the oldest open charges first; extra becomes family
credit. Charges use row versions (optimistic concurrency), and PayPal calls use idempotency keys.

**Why:** Two people (or a webhook and the owner) can change the same bill at the same moment. Row versions turn that
into a clear "someone changed this, refresh" instead of silently losing a payment.

**Trade-off:** Occasionally the owner sees a "refresh and try again" message.

---

## 6. Never cascade-delete financial history

**Decision:** Every foreign key is `Restrict`. Deleting is explicit and ordered (children first).

**Why:** Payment history should never disappear as a side effect of deleting something else.

**Trade-off:** Deletes (like the demo reset) must list every table in the right order.

---

## 7. Duplicates are caught before the AI

**Decision:** Each image gets a SHA-256 fingerprint. An identical screenshot reuses the earlier reading instead of
calling the AI again, and is marked as a duplicate. Same payment reference = duplicate too.

**Why:** Parents often send the same screenshot twice. This prevents double-counting and saves AI calls.

**Trade-off:** A re-cropped or re-saved screenshot has a different fingerprint; the reference check covers many of
those, the owner covers the rest.

---

## 8. A shared demo that keeps itself clean

**Decision:** The live demo is one shared sample academy. It resets itself when someone opens it after 30 idle
minutes (checked lazily on the first visit — no background timer), waits for a reset already in progress, and
rebuilds itself if it's ever empty.

**Why:** Judges should always start from step 1, without a background job that keeps the free database awake.

**Trade-off:** Two visitors at the very same moment can see each other's actions. Documented in the README.

---

## 9. Free-tier hosting, honestly

**Decision:** Render (Docker) for the app, Azure SQL free offer (serverless, auto-pause, overage billing disabled),
an uptime monitor on `/health` (which never touches the database).

**Why:** $0 to run, and the database can never produce a surprise bill.

**Trade-off:** The first visit after a quiet hour waits up to a minute while the database wakes. The app shows a
clear message instead of an empty screen.

---

## 10. Secrets never in code

**Decision:** User secrets locally, environment variables on Render, `.env.example` with names only. The history
was scanned with gitleaks before going public.

**Why:** The repo is public and open source.

**Trade-off:** Running it locally takes a few extra setup steps (user secrets instead of a settings file).

---

## 11. Never guess, never leave the owner stuck

**Decision:** When the code can't be sure (two families fit the same amount, an unknown sender), Tally shows the
options and asks the owner to pick. Every screen ends with a next step: a duplicate says "this is a copy", a
family with nothing open offers "Dismiss · nothing to record", and the demo guide offers "Start the guide fresh"
if something was done out of order.

**Why:** A wrong guess on money costs more than one extra click. And a screen with no next step makes people
stop trusting the tool.

**Trade-off:** Unclear payments need a human look instead of being recorded automatically.