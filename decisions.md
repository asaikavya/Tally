# Decisions

- Split into Domain / Infrastructure / Ai / Api so business rules can be tested without a database or PayPal.
- Webhooks can be delayed or missed, so a Sync job reconciles unpaid invoices directly with PayPal, reusing the same idempotent recording logic.”