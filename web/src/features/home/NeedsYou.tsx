import { useEffect, useState } from "react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import { q } from "@/lib/demo"
import type { NeedsYouItem } from "./types"
import { ReminderPanel } from "./ReminderPanel"

const METHODS = ["Zelle", "UPI", "Cash", "Check", "Other"]

type Recorded = {
  paymentId: string; amount: number; credit: number
  lines: { student: string; offering: string; applied: number }[]
  payPal: { noted: boolean; statusAfter: string | null }[]
}

async function post<T>(url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method: "POST",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  })
  const data = await res.json().catch(() => null)
  if (!res.ok) throw new Error(data?.problem ?? `Something went wrong (${res.status})`)
  return data as T
}

export function NeedsYou({ version = 0, onChanged }: { version?: number; onChanged?: () => void }) {
  const [items, setItems] = useState<NeedsYouItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [open, setOpen] = useState<string | null>(null)        // which family's form is open
  const [remindFor, setRemindFor] = useState<string | null>(null)   // which family's reminder is open
  const [method, setMethod] = useState("Zelle")
  const [amount, setAmount] = useState("")
  const [busy, setBusy] = useState(false)
  const [problem, setProblem] = useState<string | null>(null)
  const [recorded, setRecorded] = useState<{ name: string; method: string; r: Recorded } | null>(null)

  useEffect(() => {
    getJson<NeedsYouItem[]>(`/api/home/needs-you?${q}`).then(setItems).catch((e: Error) => setError(e.message))
  }, [version])

  function openForm(item: NeedsYouItem) {
    setOpen(item.payerId)
    setProblem(null)
    setAmount(String(item.amount))
    setRemindFor(null)
    // "Parent says paid" → start with what the parent said (Zelle, UPI…).
    setMethod(METHODS.find((m) => item.note?.toLowerCase().includes(m.toLowerCase())) ?? "Zelle")
  }

  async function record(item: NeedsYouItem) {
    const n = Number(amount)
    if (!(n > 0)) { setProblem("Enter the amount you received."); return }
    setBusy(true); setProblem(null)
    try {
      const r = await post<Recorded>("/api/payments/manual", { payerId: item.payerId, amount: n, method })
      setRecorded({ name: item.payerName, method, r })
      setOpen(null)
      onChanged?.()
    } catch (e) {
      setProblem((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  async function undo() {
    if (!recorded) return
    setBusy(true); setProblem(null)
    try {
      await post(`/api/payments/${recorded.r.paymentId}/undo`)
      setRecorded(null)
      onChanged?.()
    } catch (e) {
      setProblem((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="overflow-hidden rounded-2xl border border-neutral-200 bg-white">
      <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-neutral-200 px-5 py-4">
        <h2 className="text-lg font-bold">
          Needs you {items && <span className="font-medium text-neutral-500">· {items.length}</span>}
        </h2>
        <span className="text-sm text-neutral-500">Everything else is done.</span>
      </div>

      {recorded && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-neutral-200 bg-[#EEF8F1] px-5 py-3 text-sm">
          <div className="min-w-0">
            <div className="font-semibold text-[#1E6B3A]">✓ Recorded {money(recorded.r.amount)} {recorded.method} from {recorded.name}</div>
            <div className="text-neutral-700">
              {recorded.r.lines.map((l) => `${money(l.applied)} to ${l.student.split(" ")[0]} · ${l.offering}`).join(", ")}
              {recorded.r.credit > 0 ? ` · ${money(recorded.r.credit)} family credit` : ""}
              {" · "}
              {recorded.r.payPal.length === 0 ? "no PayPal bill"
                : recorded.r.payPal.every((p) => p.noted) ? `noted on PayPal (${recorded.r.payPal.map((p) => p.statusAfter).join(", ")})`
                : "PayPal note pending"}
            </div>
          </div>
          <Button variant="outline" disabled={busy} onClick={undo}>{busy ? "Working…" : "Undo"}</Button>
        </div>
      )}

      {error && <p className="px-5 py-4 text-red-700">Couldn't load: {error}</p>}
      {!items && !error && <p className="px-5 py-4 text-neutral-500">Loading…</p>}
      {items?.length === 0 && <p className="px-5 py-4 text-neutral-500">Nothing needs you today.</p>}

      {items?.map((item) => (
        <div key={item.payerId} className="flex flex-wrap items-center gap-3 border-b border-neutral-100 px-5 py-4 last:border-b-0">
          <div className="min-w-0 flex-[1_1_320px]">
            <div className="flex flex-wrap items-center gap-2 text-base font-semibold">
              {item.kind === "parent-says-paid"
                ? <Badge className="bg-[#ECE9F8] text-[#4A3F8F]">Parent says paid</Badge>
                : <Badge variant="destructive">Late</Badge>}
              {item.payerName} · {money(item.amount)}
            </div>
            <p className="mt-1 text-sm text-neutral-600">
              {item.kind === "parent-says-paid"
                ? `Says: ${item.note} — check your bank. Reminders are paused.`
                : `${item.lines.join(" · ")} — ${item.daysLate} ${item.daysLate === 1 ? "day" : "days"} late${item.country && item.country !== "US" ? ` · pays from ${item.country}` : ""}`}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" disabled={busy}
              onClick={() => (open === item.payerId ? setOpen(null) : openForm(item))}>
              {item.kind === "parent-says-paid" ? "Confirm received" : "Paid another way"}
            </Button>
                      {item.kind === "late"
              ? <Button variant="outline" disabled={busy}
                  onClick={() => { setOpen(null); setRemindFor(remindFor === item.payerId ? null : item.payerId) }}>
                  Send reminder
                </Button>
              : <Button variant="outline" disabled title="The parent said they already paid">Reminders paused</Button>}
          </div>

          {open === item.payerId && (
            <div className="flex w-full flex-wrap items-end gap-3 rounded-lg bg-[#F6F5F1] p-3">
              <label className="flex flex-col gap-1 text-sm">
                <span className="font-semibold">How they paid</span>
                <select value={method} onChange={(e) => setMethod(e.target.value)}
                  className="h-10 rounded-md border border-neutral-300 bg-white px-2">
                  {METHODS.map((m) => <option key={m}>{m}</option>)}
                </select>
              </label>
              <label className="flex flex-col gap-1 text-sm">
                <span className="font-semibold">Amount received</span>
                <input type="number" min="0" step="0.01" inputMode="decimal" value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  className="h-10 w-32 rounded-md border border-neutral-300 bg-white px-2" />
              </label>
              <Button disabled={busy} onClick={() => record(item)} className="h-10">
                {busy ? "Recording…" : "Record & note on PayPal"}
              </Button>
              <span className="w-full text-xs text-neutral-500">Fills the oldest charges first. Anything extra becomes family credit.</span>
              {problem && <span className="w-full text-sm text-red-700">{problem}</span>}
            </div>
          )}
          {remindFor === item.payerId && (
            <ReminderPanel payerId={item.payerId} payerName={item.payerName} onClose={() => setRemindFor(null)} />
          )}
        </div>
      ))}
    </section>
  )
}