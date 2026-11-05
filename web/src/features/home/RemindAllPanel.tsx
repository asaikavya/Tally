import { useEffect, useRef, useState } from "react"
import { Button } from "@/components/ui/button"
import { postFor } from "@/lib/api"
import { q } from "@/lib/demo"
import { money } from "@/lib/money"

type Draft = {
  subject: string; note: string; language: string; aiWritten: boolean
  canSend: boolean; whyNot: string | null; lastSentAtUtc: string | null
}
type Family = { payerId: string; payerName: string; amount: number }
type Row = Family & {
  draft?: Draft; error?: string; picked: boolean
  status?: "sending" | "sent" | "failed"; sendError?: string
}

const MAX = 5                                           // kind to the AI rate limit: one batch at a time
const LANG_NAME: Record<string, string> = { en: "English", es: "Spanish" }

// "Remind all late": AI drafts each family's reminder (one at a time), the owner ticks, PayPal sends.
export function RemindAllPanel({ families, onClose, onDone }: {
  families: Family[]; onClose: () => void; onDone: () => void
}) {
  const [rows, setRows] = useState<Row[]>(() => families.slice(0, MAX).map((f) => ({ ...f, picked: false })))
  const [drafted, setDrafted] = useState(0)
  const [phase, setPhase] = useState<"drafting" | "ready" | "sending" | "done">("drafting")
  const started = useRef(false)

  // Draft one family at a time, in each family's own language.
  useEffect(() => {
    if (started.current) return                         // React dev mode runs effects twice: draft only once
    started.current = true
    const list = families.slice(0, MAX)
    ;(async () => {
      for (const [i, f] of list.entries()) {
        try {
          const d = await postFor<Draft>(`/api/reminders/draft?${q}`, { payerId: f.payerId, language: null })
          setRows((prev) => prev.map((r, j) => (j === i ? { ...r, draft: d, picked: d.canSend } : r)))
        } catch (e) {
          setRows((prev) => prev.map((r, j) => (j === i ? { ...r, error: (e as Error).message } : r)))
        }
        setDrafted(i + 1)
      }
      setPhase("ready")
    })()
  }, [families])

  function toggle(i: number) {
    setRows((prev) => prev.map((r, j) => (j === i ? { ...r, picked: !r.picked } : r)))
  }

  async function sendPicked() {
    setPhase("sending")
    const picked = rows.map((r, i) => ({ r, i })).filter(({ r }) => r.picked && r.draft)
    for (const { r, i } of picked) {
      setRows((prev) => prev.map((x, j) => (j === i ? { ...x, status: "sending" } : x)))
      try {
        await postFor("/api/reminders/send", {
          payerId: r.payerId, subject: r.draft!.subject, note: r.draft!.note, language: r.draft!.language,
        })
        setRows((prev) => prev.map((x, j) => (j === i ? { ...x, status: "sent" } : x)))
      } catch (e) {
        setRows((prev) => prev.map((x, j) => (j === i ? { ...x, status: "failed", sendError: (e as Error).message } : x)))
      }
    }
    setPhase("done")
    onDone()
  }

  const pickedCount = rows.filter((r) => r.picked && r.draft).length
  const sentCount = rows.filter((r) => r.status === "sent").length

  return (
    <div className="flex flex-col gap-3 border-b border-neutral-200 bg-[#F6F5F1] px-5 py-4">
      <div className="flex items-center justify-between gap-2">
        <span className="font-semibold">Remind late families</span>
        <Button variant="ghost" onClick={onClose} disabled={phase === "sending"}>Close</Button>
      </div>

      {phase === "drafting" && (
        <p className="text-sm text-neutral-500">Drafting {Math.min(drafted + 1, rows.length)} of {rows.length}… each in the family's own language.</p>
      )}
      {families.length > MAX && (
        <p className="text-xs text-neutral-500">Showing the first {MAX}. Send these, then open it again for the rest.</p>
      )}

      <ul className="flex flex-col gap-2">
        {rows.map((r, i) => (
          <li key={r.payerId} className="flex flex-col gap-1 rounded-lg border border-neutral-200 bg-white px-3 py-2">
            <div className="flex flex-wrap items-center gap-2">
              <input type="checkbox" className="size-4 accent-[#1E6B3A]"
                checked={r.picked} disabled={!r.draft?.canSend || phase !== "ready"}
                onChange={() => toggle(i)} aria-label={`Send to ${r.payerName}`} />
              <span className="font-semibold">{r.payerName} · {money(r.amount)}</span>
              {r.draft && (
                <span className="rounded bg-[#E8F0F7] px-1.5 py-0.5 text-xs font-semibold text-[#16476A]">
                  {LANG_NAME[r.draft.language] ?? r.draft.language}
                </span>
              )}
              <span className="ml-auto text-sm">
                {r.status === "sending" && <span className="text-neutral-500">Sending…</span>}
                {r.status === "sent" && <span className="font-semibold text-[#1E6B3A]">✓ Sent</span>}
                {r.status === "failed" && <span className="text-red-700">Not sent</span>}
                {!r.draft && !r.error && phase === "drafting" && <span className="text-neutral-400">Waiting…</span>}
              </span>
            </div>
            {r.draft && (
              <>
                <span className="text-sm">{r.draft.subject}</span>
                <details className="text-sm text-neutral-600">
                  <summary className="cursor-pointer text-[#16476A]">Preview message</summary>
                  <p className="mt-1 whitespace-pre-line">{r.draft.note}</p>
                </details>
              </>
            )}
            {r.draft && !r.draft.canSend && r.draft.whyNot && <span className="text-sm text-[#7A4F00]">{r.draft.whyNot}</span>}
            {r.error && <span className="text-sm text-red-700">{r.error}</span>}
            {r.sendError && <span className="text-sm text-red-700">{r.sendError}</span>}
          </li>
        ))}
      </ul>

      {phase === "ready" && (
        <div className="flex flex-wrap items-center gap-3">
          <Button disabled={pickedCount === 0} onClick={sendPicked}>
            Send {pickedCount} {pickedCount === 1 ? "reminder" : "reminders"} via PayPal
          </Button>
          <span className="text-xs text-neutral-500">
            Drafted by AI from each family's own open classes. To edit one first, use Send reminder on its row.
          </span>
        </div>
      )}
      {phase === "done" && (
        <p className="text-sm font-semibold text-[#1E6B3A]">
          ✓ {sentCount} {sentCount === 1 ? "reminder" : "reminders"} sent through PayPal. Each family got their bill with a Pay now button.
        </p>
      )}
    </div>
  )
}