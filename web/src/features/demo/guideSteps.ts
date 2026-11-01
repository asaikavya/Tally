// Matches GET /api/demo/progress (camelCase from ASP.NET).
export type Progress = {
  zelleRead: boolean
  notedOnPayPal: boolean
  parentSaidPaid: boolean
  parentConfirmed: boolean
  reminderSent: boolean
  paidWithPayPal: boolean
  raviPayerId: string | null
  luciaPayerId: string | null
  outsideTotal: number
  payPalTotal: number
  payPalNotes: number
}

export type StepAction = "try-zelle" | "parent-page" | "paypal-bill"

export type Step = {
  key: string
  say: (p: Progress) => string
  doneText: string                       // shown once the step is finished
  done: (p: Progress) => boolean
  action?: (p: Progress) => { kind: StepAction; label: string } | null
}

// The 5-step demo. Short, everyday words: one instruction per step.
export const STEPS: Step[] = [
  {
    key: "zelle",
    say: () => "A parent sent $170 on Zelle, no note. See which kids it's for.",
    doneText: "Tally read the screenshot and found Ravi's family.",
    done: (p) => p.zelleRead,
    action: () => ({ kind: "try-zelle", label: "Send $170 Zelle screenshot" }),
  },
  {
    key: "approve",
    say: () => 'Tally found the match. Click "Approve & note on PayPal".',
    doneText: "Approved. $170 is noted on Ravi's PayPal bill.",
    done: (p) => p.notedOnPayPal,
  },
  {
    key: "parent",
    say: (p) => p.parentSaidPaid
      ? "Ravi says he paid his $100 balance. Confirm it under \"Needs you\"."
      : "Be the parent: on Ravi's page, tap \"I already paid\" for his $100 balance.",
    doneText: "Ravi's $100 is confirmed and noted on PayPal.",
    done: (p) => p.parentConfirmed,
    action: (p) => p.parentSaidPaid ? null : { kind: "parent-page", label: "Open Ravi's page ↗" },
  },
  {
    key: "remind",
    say: () => 'Lucia is late. Click "Send reminder" on Lucia. The AI writes it in Spanish.',
    doneText: "Reminder sent to Lucia, written in Spanish.",
    done: (p) => p.reminderSent,
  },
  {
    key: "paypal",
    say: () => "Be Lucia: pay her bill as the PayPal sandbox buyer (login is in the README). Home updates by itself.",
    doneText: "Lucia paid on PayPal. Home updated by itself.",
    done: (p) => p.paidWithPayPal,
    action: () => ({ kind: "paypal-bill", label: "Open Lucia's PayPal bill ↗" }),
  },
]