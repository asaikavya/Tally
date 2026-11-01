import { ArrowRight } from "lucide-react"

// The whole app in one line. Plain text on purpose: it explains, it isn't something to click.
const STEPS = ["Parent sends a screenshot", "AI reads it, Tally matches it", "Noted on the PayPal bill"]

export function HowItWorks() {
  return (
    <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-neutral-600" aria-label="How Tally works">
      {STEPS.map((s, i) => (
        <span key={s} className="flex items-center gap-2">
          <span className="flex items-center gap-1.5">
            <span className="flex size-5 items-center justify-center rounded-full bg-[#E8F0F7] text-xs font-bold text-[#16476A]">{i + 1}</span>
            {s}
          </span>
          {i < STEPS.length - 1 && <ArrowRight className="size-3.5 text-neutral-400" aria-hidden="true" />}
        </span>
      ))}
    </p>
  )
}