import { useEffect, useState } from "react"
import { useParams } from "react-router"
import { Button } from "@/components/ui/button"
import { getJson, postJson } from "@/lib/api"
import { money } from "@/lib/money"
import { PERIOD } from "@/lib/demo"
import type { PayPage as PayPageData } from "./types"
import { postFile } from "@/lib/api"

const methods = ["Zelle", "UPI", "Cash", "Check", "Other"] as const

export function PayPage() {
  const { token } = useParams()
  const [data, setData] = useState<PayPageData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [method, setMethod] = useState<(typeof methods)[number]>("Zelle")
  const [note, setNote] = useState("")
  const [sending, setSending] = useState(false)

  const load = () =>
    getJson<PayPageData>(`/api/pay/${token}?period=${PERIOD}`).then(setData).catch((e: Error) => setError(e.message))

  useEffect(() => { load() }, [token]) // eslint-disable-line react-hooks/exhaustive-deps

  const tellUs = async () => {
    setSending(true)
    try {
      await postJson(`/api/pay/${token}/already-paid?period=${PERIOD}`, { method, note: note || null })
      setShowForm(false)
      await load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSending(false)
    }
  }

  if (error?.startsWith("404")) return <Centered>This link isn't valid. Please ask your academy for a new one.</Centered>
  if (error) return <Centered>Something went wrong. Please try again in a minute.</Centered>
  if (!data) return <Centered>Loading…</Centered>

  const month = new Date(data.period + "T00:00").toLocaleDateString("en-US", { month: "long" })
  const due = data.dueDate ? new Date(data.dueDate + "T00:00").toLocaleDateString("en-US", { month: "short", day: "numeric" }) : null
  const settled = data.remaining <= 0

  return (
    <div className="min-h-svh bg-[#F6F5F1] px-4 py-6 text-neutral-900">
      <div className="mx-auto flex max-w-md flex-col gap-4">
        <div className="flex items-center gap-3">
          <div className="flex size-10 items-center justify-center rounded-xl bg-neutral-900 text-sm font-bold text-white">
            {data.academy.split(" ").map((w) => w[0]).slice(0, 2).join("")}
          </div>
          <div className="font-bold">{data.academy}</div>
        </div>

        <section className="flex flex-col gap-4 rounded-2xl border border-neutral-200 bg-white p-5">
          <p className="text-neutral-600">Hi {data.payerName.split(" ")[0]}, here's {month}</p>
          <div>
            <div className="text-4xl font-extrabold">{money(data.remaining)}</div>
            <div className="mt-1 text-sm text-neutral-600">
              {settled ? "All paid — thank you!" : due ? `still due · was due ${due}` : "still due"}
            </div>
          </div>

          <details open className="border-t border-neutral-100 pt-3">
            <summary className="min-h-8 cursor-pointer font-semibold">
              What's included · {data.lines.length} {data.lines.length === 1 ? "class" : "classes"}
            </summary>
            <ul className="mt-2 flex flex-col gap-2">
              {data.lines.map((l) => (
                <li key={l.student + l.offering} className="flex justify-between gap-3">
                  <span>{l.student} · {l.offering}</span>
                  <span className="font-semibold">{l.remaining <= 0 ? "Paid" : money(l.remaining)}</span>
                </li>
              ))}
            </ul>
          </details>

          {!settled && data.payUrl && (
            <a
              href={data.payUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="flex min-h-13 items-center justify-center rounded-xl bg-[#1F5F8B] text-lg font-bold text-white"
            >
              Pay with PayPal
            </a>
          )}
          {!settled && !data.payUrl && (
            <p className="text-sm text-neutral-600">Your PayPal bill isn't ready yet. Please check back soon.</p>
          )}
        </section>

        {!settled && (
          <section className="flex flex-col gap-3 rounded-2xl border border-neutral-200 bg-white p-5">
            {data.parentSaysPaid ? (
              <p>Thanks! We'll confirm once your payment shows up. We won't send reminders meanwhile.</p>
            ) : !showForm ? (
              <>
                <h2 className="font-bold">Already paid another way?</h2>
                <Button variant="outline" className="min-h-11" onClick={() => setShowForm(true)}>I already paid</Button>
                                <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-md border border-dashed border-neutral-400 font-medium hover:bg-neutral-50">
                  Upload a receipt
                  <input type="file" accept="image/png,image/jpeg,image/webp" className="hidden"
                    onChange={async (e) => {
                      const file = e.target.files?.[0]
                      if (!file) return
                      try { await postFile(`/api/pay/${token}/receipt?period=${PERIOD}`, file); await load() }
                      catch (err) { setError((err as Error).message) }
                    }} />
                </label>            
                  </>
            ) : (
              <>
                <h2 className="font-bold">How did you pay?</h2>
                <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Payment method">
                  {methods.map((m) => (
                    <Button key={m} variant={method === m ? "default" : "outline"} role="radio" aria-checked={method === m} onClick={() => setMethod(m)}>
                      {m}
                    </Button>
                  ))}
                </div>
                <label className="flex flex-col gap-1 text-sm">
                  Anything we should know? (optional)
                  <input
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    maxLength={200}
                    placeholder="e.g. sent on Nov 9 from my husband's account"
                    className="min-h-11 rounded-lg border border-neutral-300 px-3"
                  />
                </label>
                <Button className="min-h-11" disabled={sending} onClick={tellUs}>{sending ? "Sending…" : "Tell the academy"}</Button>
              </>
            )}
          </section>
        )}

        <p className="text-center text-xs text-neutral-500">Only you and {data.academy} can see this page.</p>
      </div>
    </div>
  )
}

function Centered({ children }: { children: React.ReactNode }) {
  return <div className="flex min-h-svh items-center justify-center bg-[#F6F5F1] p-6 text-center text-neutral-700">{children}</div>
}