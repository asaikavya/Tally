export type Progress = {
  zelleRead: boolean; notedOnPayPal: boolean; parentConfirmed: boolean
  parentSaidPaid: boolean; reminderSent: boolean; paidWithPayPal: boolean
}

export type Step = { key: string; title: string; todo: string; tour: string; done: (p: Progress) => boolean }

// The 5-step story, "Tonight at the academy". Each step ticks from real data on the server.
export const STEPS: Step[] = [
  { key: "zelle", title: "A mystery Zelle", tour: "samples", done: (p) => p.zelleRead,
    todo: "A parent sent $170 by Zelle with no name or memo. Try the $170 Zelle sample." },
  { key: "approve", title: "Note it on PayPal", tour: "review", done: (p) => p.notedOnPayPal,
    todo: "Approve the sure matches. Tally notes each one on the family's PayPal bill." },
  { key: "parent", title: "Parent says paid", tour: "needs-you", done: (p) => p.parentConfirmed,
    todo: "Open a family's pay page, tap “I already paid”, then confirm it here." },
  { key: "remind", title: "Reminder in Spanish", tour: "needs-you", done: (p) => p.reminderSent,
    todo: "Lucia still owes $90. Send her a reminder; it opens in Spanish." },
  { key: "paypal", title: "Pay with PayPal", tour: "needs-you", done: (p) => p.paidWithPayPal,
    todo: "Open Lucia's PayPal bill and pay as the test buyer. Tally updates by itself." },
]