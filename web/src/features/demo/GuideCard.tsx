import { useCallback, useEffect, useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { Compass, X } from "lucide-react"
import { getJson, postFor } from "@/lib/api"
import { useAutoRefresh } from "@/lib/useAutoRefresh"
import { uploadSample } from "@/features/home/samples"
import { STEPS, type Progress, type StepAction } from "./guideSteps"

const SEEN_KEY = "tally-guide-seen"

function readSeen(): boolean {
  try { return localStorage.getItem(SEEN_KEY) === "1" } catch { return true }
}

// "Guide 1/5" pill, bottom-right. Click it for the current step; closed by default so it never blocks the page.
export function GuideCard() {
  const [progress, setProgress] = useState<Progress | null>(null)
  const [open, setOpen] = useState(false)
  const [seen, setSeen] = useState(readSeen)
  const [viewing, setViewing] = useState<number | null>(null)   // null = follow the first unfinished step
  const [busy, setBusy] = useState(false)
  const { pathname } = useLocation()
  const navigate = useNavigate()

  const load = useCallback(() => {
    getJson<Progress>("/api/demo/progress").then(setProgress).catch(() => setProgress(null))
  }, [])
  useEffect(() => { load() }, [load, pathname])
  useAutoRefresh(load)

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false) }
    window.addEventListener("keydown", onKey)
    return () => window.removeEventListener("keydown", onKey)
  }, [open])

  const firstOpen = progress ? STEPS.findIndex((s) => !s.done(progress)) : 0
  const allDone = progress !== null && firstOpen === -1
  const shown = viewing ?? (allDone ? STEPS.length - 1 : firstOpen)

  // When the step you're looking at gets done, follow along to the next one.
  useEffect(() => {
    if (viewing !== null && progress && STEPS[viewing].done(progress) && viewing < firstOpen) setViewing(null)
  }, [progress, viewing, firstOpen])

  function toggle() {
    setOpen((o) => !o)
    if (!seen) {
      setSeen(true)
      try { localStorage.setItem(SEEN_KEY, "1") } catch { /* fine */ }
    }
  }

  async function openFromServer(path: string) {
    const tab = window.open("about:blank", "_blank")           // open during the click so it isn't blocked
    try {
      const r = await postFor<{ url: string }>(path)
      const url = r.url.startsWith("/") ? `${window.location.origin}${r.url}` : r.url
      if (tab) { tab.opener = null; tab.location.href = url }
    } catch { tab?.close() }
  }

  async function act(kind: StepAction) {
    if (!progress) return
    setBusy(true)
    try {
      if (kind === "try-zelle") navigate(`/receipts/${await uploadSample("zelle-r-kumar-170.png")}`)
      if (kind === "parent-page" && progress.raviPayerId) await openFromServer(`/api/payers/${progress.raviPayerId}/pay-link`)
      if (kind === "paypal-bill" && progress.luciaPayerId) await openFromServer(`/api/payers/${progress.luciaPayerId}/paypal-bill`)
    } finally {
      setBusy(false)
      load()
    }
  }

  if (!progress) return null                          // demo off, or still loading: show nothing

  const step = STEPS[shown]
  const done = step.done(progress)
  const action = !done && shown === firstOpen ? step.action?.(progress) ?? null : null
  const badge = allDone ? "✓" : `${firstOpen + 1}/${STEPS.length}`

  return (
    <div className="fixed right-4 bottom-4 z-40 flex flex-col items-end gap-3">
      {open && (
        <aside id="guide-card" aria-label="Demo guide"
          className="flex w-[calc(100vw-2rem)] flex-col gap-3 rounded-2xl border border-neutral-200 bg-white p-4 shadow-xl sm:w-80">
          <div className="flex items-center justify-between text-xs font-semibold text-neutral-500">
            <span>{allDone ? "All done" : `Step ${shown + 1} of ${STEPS.length}`}</span>
            <button onClick={() => setOpen(false)} aria-label="Close guide" className="rounded p-1 hover:bg-neutral-100">
              <X className="size-4" aria-hidden="true" />
            </button>
          </div>

          <p className="text-[15px] font-semibold leading-snug">
            {allDone ? "✓ Every payment, accounted for." : done ? `✓ ${step.say(progress)}` : step.say(progress)}
          </p>

          {action && (
            <button disabled={busy} onClick={() => act(action.kind)}
              className="min-h-10 rounded-lg bg-[#16476A] px-3 text-sm font-semibold text-white hover:bg-[#123A57] disabled:opacity-60">
              {busy ? "Working…" : action.label}
            </button>
          )}

          <div className="flex items-center justify-between">
            <span className="flex gap-1" aria-hidden="true">
              {STEPS.map((s, i) => (
                <span key={s.key} className={`size-2.5 rounded-full ${
                  s.done(progress) ? "bg-[#1E6B3A]" : i === shown ? "bg-[#16476A]" : "bg-neutral-300"}`} />
              ))}
            </span>
            <span className="flex gap-1">
              <button disabled={shown === 0} onClick={() => setViewing(shown - 1)}
                className="rounded-md px-2 py-1 text-sm font-semibold hover:bg-neutral-100 disabled:opacity-30">‹ Back</button>
              <button disabled={shown === STEPS.length - 1} onClick={() => setViewing(shown + 1)}
                className="rounded-md px-2 py-1 text-sm font-semibold hover:bg-neutral-100 disabled:opacity-30">Next ›</button>
            </span>
          </div>
        </aside>
      )}

      <button onClick={toggle} aria-expanded={open} aria-controls="guide-card"
        aria-label={open ? "Close guide" : `Open guide, step ${badge}`}
        className="relative flex h-11 items-center gap-2 rounded-full bg-[#16476A] px-4 text-sm font-semibold text-white shadow-lg hover:bg-[#123A57]">
        {!seen && <span className="absolute inset-0 animate-ping rounded-full bg-[#16476A]/40" aria-hidden="true" />}
        <Compass className="relative size-4" aria-hidden="true" />
        <span className="relative">Guide {badge}</span>
      </button>
    </div>
  )
}