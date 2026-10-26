import { useEffect, useState } from "react"
import { Link, useParams } from "react-router"
import { Button } from "@/components/ui/button"
import { getJson, putJson } from "@/lib/api"
import { money } from "@/lib/money"
import type { Receipt } from "./types"

type Form = { amount: string; paidOn: string; senderName: string; memo: string; reference: string }
const toForm = (r: Receipt): Form => ({
  amount: r.amount?.toString() ?? "", paidOn: r.paidOn ?? "", senderName: r.senderName ?? "",
  memo: r.memo ?? "", reference: r.reference ?? "",
})

export function ReceiptPage() {
  const { id } = useParams()
  const [r, setR] = useState<Receipt | null>(null)
  const [form, setForm] = useState<Form | null>(null)
  const [editing, setEditing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getJson<Receipt>(`/api/receipts/${id}`).then((x) => { setR(x); setForm(toForm(x)) }).catch((e: Error) => setError(e.message))
  }, [id])

  const save = async () => {
    if (!form) return
    const updated = await putJson<Receipt>(`/api/receipts/${id}`, {
      amount: form.amount ? Number(form.amount) : null,
      paidOn: form.paidOn || null,
      senderName: form.senderName || null,
      memo: form.memo || null,
      reference: form.reference || null,
    })
    setR(updated); setForm(toForm(updated)); setEditing(false)
  }

  if (error) return <p className="p-8 text-red-700">Couldn't load receipt: {error}</p>
  if (!r || !form) return <p className="p-8 text-neutral-500">Loading…</p>

  const field = (label: string, key: keyof Form, shown: string, type = "text") => (
    <div className="flex flex-col gap-1">
      <label htmlFor={key} className="text-sm text-neutral-500">
        {label}{r.edited.includes(key.charAt(0).toUpperCase() + key.slice(1)) && <span className="ml-1 text-xs text-[#7A4F00]">(corrected)</span>}
      </label>
      {editing
        ? <input id={key} type={type} value={form[key]} onChange={(e) => setForm({ ...form, [key]: e.target.value })}
                 className="min-h-11 rounded-lg border border-neutral-300 px-3" />
        : <div className="min-h-8 font-medium">{shown || <span className="text-neutral-400">not on the receipt</span>}</div>}
    </div>
  )

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-5 p-8">
      <Link to="/" className="text-sm text-neutral-600">← Back to Home</Link>
      <h1 className="text-3xl font-extrabold tracking-tight">Receipt</h1>

      {r.status === "Duplicate" && <p className="rounded-lg bg-[#FBE4DF] p-3 text-[#A8321F]">{r.problem} Nothing new will be recorded.</p>}
      {r.status === "NeedsReview" && <p className="rounded-lg bg-[#FBF0D9] p-3 text-[#7A4F00]">{r.problem}</p>}

      <div className="flex flex-wrap items-start gap-6">
        <div className="flex-[1_1_300px] rounded-2xl bg-[#F1EFEA] p-5">
          <img src={`/api/receipts/${r.id}/image`} alt="Uploaded receipt"
               className="mx-auto max-h-140 -rotate-1 rounded-md bg-white shadow-md" />
        </div>



        <section className="flex flex-[1_1_380px] flex-col gap-4 rounded-2xl border border-neutral-200 bg-white p-6">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-bold">What the AI read</h2>
            {!editing
              ? <Button variant="outline" onClick={() => setEditing(true)}>Fix a detail</Button>
              : <div className="flex gap-2"><Button onClick={save}>Save</Button><Button variant="ghost" onClick={() => { setForm(toForm(r)); setEditing(false) }}>Cancel</Button></div>}
          </div>
          {field("Amount", "amount", r.amount != null ? money(r.amount) : "", "number")}
        {field("Date", "paidOn", r.paidOn ? new Date(r.paidOn + "T00:00").toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" }) : "", "date")}
          {field("Sender", "senderName", r.senderName ?? "")}
          {field("Memo", "memo", r.memo ?? "")}
          {field("Reference", "reference", r.reference ?? "")}
          <p className="text-sm text-neutral-500">
            {r.method ?? "Unknown method"} · paid to {r.paidTo ?? "unknown"} · uploaded by {r.uploadedBy}
          </p>
          <p className="text-xs text-neutral-500">
            AI read the receipt image only. It never sees your families list; matching is Tally's own code.
          </p>
          <div className="rounded-xl border border-dashed border-neutral-300 p-4 text-sm text-neutral-500">
            Payment Detective (who paid, which classes) arrives in the next step.
          </div>
        </section>
      </div>
    </main>
  )
}