// After someone opens a PayPal bill, check with PayPal whenever they come back to Tally (for 10 minutes).
// Works without webhooks (local) and covers a slow webhook on live.
let checkUntil = 0

export function markPayPalOpened() {
  checkUntil = Date.now() + 10 * 60 * 1000
}

export async function checkPayPalIfReturning() {
  if (Date.now() > checkUntil) return
  try { await fetch("/api/demo/sync", { method: "POST" }) } catch { /* fine: next return tries again */ }
  window.dispatchEvent(new Event("tally:refresh"))      // Home, Payments and the Guide update at once
}