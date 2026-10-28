import { useEffect, useState } from "react"
import { Button } from "@/components/ui/button"
import { postFor } from "@/lib/api"
import { q } from "@/lib/demo"

type Draft = {
  subject: string; note: string; language: string; aiWritten: boolean
  canSend: boolean; whyNot: string | null; lastSentAtUtc: string | null
}
type Sent = { sentAtUtc: string; payPalInvoiceIds: string[] }

const LANGS = [{ code: "en", label: "English" }, { code: "es", label: "Español" }]

// AI drafts → owner edits → PayPal sends (with the bill and a Pay now button).
export function ReminderPanel({ payerId, payerName, onClose }: { payerId: string; payerName: string; onClose: () => void }) {
  const [lang, setLang] = useState<string | null>(null)        // null = the family's own language
  const [draft, setDraft] = useState<Draft | null>(null)
  const [subject, setSubject] = useState("")
  const [note, setNote] = useState("")
  const [busy, setBusy] = useState<"draft" | "send" | null>("draft")
  const [problem, setProblem] = useState<string | null>(null)
  const [sent, setSent] = useState<Sent | null>(null)

  useEffect(() => {
    let cancelled = false
    postFor<Draft>(`/api/reminders/draft?${q}`, { payerId, language: lang })
      .then((d) => { if (!cancelled) { setDraft(d); setSubject(d.subject); setNote(d.note) } })
      .catch((e: Error) => { if (!cancelled) setProblem(e.message) })
      .finally(() => { if (!cancelled) setBusy(null) })
    return () => { cancelled = true }
  }, [payerId, lang])

  function switchTo(code: string) {
    if (busy || code === draft?.language) return
    setBusy("draft"); setProblem(null); setLang(code)
  }

  async function send() {
    if (!draft) return
    setBusy("send"); setProblem(null)
    try {
      setSent(await postFor<Sent>("/api/reminders/send", { payerId, subject, note, language: draft.language }))
    } catch (e) {
      setProblem((e as Error).message)
    } finally {
      setBusy(null)
    }
  }

  if (sent) {
    return (
      <div className="flex w-full flex-wrap items-center justify-between gap-3 rounded-lg bg-[#EEF8F1] p-3 text-sm">
        <span className="font-semibold text-[#1E6B3A]">
          ✓ Reminder sent through PayPal to {payerName}. PayPal emailed the bill with a Pay now button.
        </span>
        <Button variant="ghost" onClick={onClose}>Close</Button>
      </div>
    )
  }

  return (
    <div className="flex w-full flex-col gap-3 rounded-lg bg-[#F6F5F1] p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm font-semibold">Reminder to {payerName}</span>
        <div className="flex gap-1" role="group" aria-label="Language">
          {LANGS.map((l) => (
            <Button key={l.code} size="sm" variant={draft?.language === l.code ? "default" : "outline"}
              disabled={busy !== null} onClick={() => switchTo(l.code)}>
              {l.label}
            </Button>
          ))}
        </div>
      </div>

      {busy === "draft" && <p className="text-sm text-neutral-500">Drafting…</p>}

      {busy !== "draft" && draft && (
        <>
          <input aria-label="Subject" value={subject} onChange={(e) => setSubject(e.target.value)}
            className="h-10 rounded-md border border-neutral-300 bg-white px-2 text-sm" />
          <textarea aria-label="Message" rows={8} value={note} onChange={(e) => setNote(e.target.value)}
            className="rounded-md border border-neutral-300 bg-white p-2 text-sm leading-relaxed" />
          <p className="text-xs text-neutral-500">
            {draft.aiWritten
              ? "Drafted by AI from this family's open classes only. Edit anything before sending."
              : draft.canSend ? "Plain template (AI was unavailable). Edit anything before sending." : "Preview only."}
            {" "}PayPal adds the bill and a Pay now button.
          </p>
          {!draft.canSend && draft.whyNot && <p className="text-sm text-[#7A4F00]">{draft.whyNot}</p>}
          <div className="flex gap-2">
            <Button disabled={!draft.canSend || busy !== null} onClick={send}>
              {busy === "send" ? "Sending…" : "Send via PayPal"}
            </Button>
            <Button variant="ghost" onClick={onClose}>Cancel</Button>
          </div>
        </>
      )}

      {problem && <p className="text-sm text-red-700">{problem}</p>}
    </div>
  )
}