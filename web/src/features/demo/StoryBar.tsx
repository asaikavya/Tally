import { useCallback, useEffect, useState } from "react"
import { useLocation } from "react-router"
import { getJson } from "@/lib/api"
import { useAutoRefresh } from "@/lib/useAutoRefresh"
import { STEPS, type Progress } from "./story"

// The slim story line in the top bar: which step you're on, progress dots, and "Show me".
export function StoryBar({ onShowMe }: { onShowMe: (tour: string) => void }) {
  const [progress, setProgress] = useState<Progress | null>(null)
  const [unavailable, setUnavailable] = useState(false)
  const { pathname } = useLocation()

  const load = useCallback(() => {
    getJson<Progress>("/api/demo/progress").then(setProgress).catch(() => setUnavailable(true))
  }, [])
  useEffect(() => { load() }, [load, pathname])     // re-check when moving between pages
  useAutoRefresh(load)                              // ...and every 20s while visible

  if (unavailable) return <span><strong>Demo mode</strong> · sample academy, PayPal sandbox.</span>
  if (!progress) return <span><strong>Demo</strong> · loading the story…</span>

  const current = STEPS.findIndex((s) => !s.done(progress))
  if (current === -1) {
    return <span><strong>Demo · all 5 steps done.</strong> Every payment, accounted for.</span>
  }

  const step = STEPS[current]
  const text = step.key === "parent" && progress.parentSaidPaid
    ? "A parent says they paid. Confirm it under Needs you."
    : step.todo

  return (
    <div className="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1">
      <strong className="whitespace-nowrap">Demo · Step {current + 1} of {STEPS.length}</strong>
      <span className="min-w-0">{text}</span>
      <span className="flex items-center gap-1" aria-hidden="true">
        {STEPS.map((s, i) => (
          <span key={s.key} className={`size-2.5 rounded-full ${
            s.done(progress) ? "bg-[#1E6B3A]" : i === current ? "border-2 border-[#7A4F00]" : "bg-[#E2CF9E]"}`} />
        ))}
      </span>
      <button onClick={() => onShowMe(step.tour)} className="whitespace-nowrap rounded-md px-2 py-1 font-semibold underline underline-offset-2 hover:bg-[#F7E7BD]">
        Show me
      </button>
    </div>
  )
}