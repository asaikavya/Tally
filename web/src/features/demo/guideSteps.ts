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
}

export type StepAction = "try-zelle" | "parent-page" | "paypal-bill"

export type Step = {
  key: string
  say: (p: Progress) => string
  done: (p: Progress) => boolean
  action?: (p: Progress) => { kind: StepAction; label: string } | null
}

// The 5-step demo. Short, everyday words: one instruction per step.
export const STEPS: Step[] = [
  {
    key: "zelle",
    say: () => "A parent sent $170 on Zelle, no note. See which kids it's for.",
    done: (p) => p.zelleRead,
    action: () => ({ kind: "try-zelle", label: "Send $170 Zelle screenshot" }),
  },
  {
    key: "approve",
    say: () => 'Tally found the match. On the receipt, click "Approve & note on PayPal".',
    done: (p) => p.notedOnPayPal,
  },
  {
    key: "parent",
    say: (p) => p.parentSaidPaid
      ? "Ravi says he paid his $100 balance. Confirm it under \"Needs you\"."
      : "Be the parent: on Ravi's page, tap \"I already paid\" for his $100 balance.",
    done: (p) => p.parentConfirmed,
    action: (p) => p.parentSaidPaid ? null : { kind: "parent-page", label: "Open Ravi's page ↗" },
  },
  {
    key: "remind",
    say: () => 'Lucia is late. Click "Send reminder" on Lucia. The AI writes it in Spanish.',
    done: (p) => p.reminderSent,
  },
  {
    key: "paypal",
    say: () => "Be Lucia: pay her bill with the PayPal sandbox buyer. Home updates by itself.",
    done: (p) => p.paidWithPayPal,
    action: () => ({ kind: "paypal-bill", label: "Open Lucia's PayPal bill ↗" }),
  },
]