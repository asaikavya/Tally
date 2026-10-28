import { useCallback, useEffect, useState } from "react"
import { Link } from "react-router"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"

type Cover = { label: string; amount: number }
type ReviewItem = {
  receiptId: string; amount: number | null; senderName: string | null; method: string | null; paidOn: string | null
  sure: boolean; headline: string; payerId: string | null; family: string | null; covers: Cover[]
}
type ReviewQueue = { sure: ReviewItem[]; needsLook: ReviewItem[]; sureTotal: number }
type BulkItem = { receiptId: string; approved: boolean; message: string }
type BulkResult = { approved: number; skipped: number; items: BulkItem[] }

export function ReviewInbox({ version = 0, onApproved }: { version?: number; onApproved?: () => void }) {
  const [queue, setQueue] = useState<ReviewQueue | null>(null)
  const [picked, setPicked] = useState<Set<string>>(new Set())
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<BulkResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(() => {
    getJson<ReviewQueue>("/api/receipts/review")
      .then((q) => { setQueue(q); setPicked(new Set(q.sure.map((i) => i.receiptId))) })   // all sure ones pre-ticked
      .catch((e: Error) => setError(e.message))
  }, [])

  useEffect(() => { load() }, [load,version])

  function toggle(id: string) {
    setPicked((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id); else next.add(id)
      return next
    })
  }

  async function approveAll() {
    setBusy(true); setError(null); setResult(null)
    try {
      const res = await fetch("/api/receipts/approve-all", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ receiptIds: [...picked] }),
      })
      const data = await res.json().catch(() => null)
      if (!res.ok) throw new Error(data?.problem ?? `Something went wrong (${res.status})`)
      setResult(data as BulkResult)
      load()
            if ((data as BulkResult).approved > 0) onApproved?.()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
    }
  }

  if (error && !queue) return <p className="text-red-700">Couldn't load receipts: {error}</p>
  if (!queue) return null
  const total = queue.sure.length + queue.needsLook.length
  if (total === 0 && !result) return null

  const pickedTotal = queue.sure.filter((i) => picked.has(i.receiptId)).reduce((s, i) => s + (i.amount ?? 0), 0)

  return (
    <section className="flex flex-col gap-4 rounded-2xl border border-neutral-200 bg-white p-5">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-lg font-extrabold">Receipts to check · {total}</h2>
        <span className="text-sm text-neutral-500">Nothing is recorded until you approve.</span>
      </div>

      {result && (
        <div className="flex flex-col gap-1 rounded-xl bg-[#F1F6FA] p-3 text-sm">
          <div className="font-bold">{result.approved} approved{result.skipped > 0 ? `, ${result.skipped} left for you to check` : ""}</div>
          {result.items.map((i) => (
            <div key={i.receiptId} className={i.approved ? "text-[#1E6B3A]" : "text-[#7A4F00]"}>
              {i.approved ? "✓" : "!"} {i.message}
            </div>
          ))}
        </div>
      )}

      {queue.sure.length > 0 && (
        <div className="flex flex-col gap-3 rounded-xl border border-[#9FD1AF] bg-[#EEF8F1] p-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <div className="font-bold">
                ✓ {queue.sure.length} sure {queue.sure.length === 1 ? "match" : "matches"} ready ({money(queue.sureTotal)})
              </div>
              <div className="text-sm text-neutral-600">Known family, and only one way the amount fits. Untick any you want to look at first.</div>
            </div>
            <Button className="min-h-11" disabled={busy || picked.size === 0} onClick={approveAll}>
              {busy ? `Approving ${picked.size}…` : `Approve & note on PayPal · ${picked.size} (${money(pickedTotal)})`}
            </Button>
          </div>
          <ul className="flex flex-col gap-1.5">
            {queue.sure.map((i) => (
              <li key={i.receiptId} className="flex items-center gap-3 rounded-lg bg-white px-3 py-2">
                <input type="checkbox" className="size-4 accent-[#1E6B3A]" disabled={busy}
                  checked={picked.has(i.receiptId)} onChange={() => toggle(i.receiptId)}
                  aria-label={`Include ${i.family}`} />
                <div className="min-w-0 flex-1">
                  <span className="font-semibold">{i.family}</span>
                  <span className="text-neutral-600"> · {i.covers.map((c) => c.label).join(" + ")}</span>
                  <div className="text-xs text-neutral-500">{i.method ?? "Payment"}{i.senderName ? ` from ${i.senderName}` : ""}</div>
                </div>
                <span className="font-semibold">{money(i.amount ?? 0)}</span>
                <Link to={`/receipts/${i.receiptId}`} className="text-sm text-[#16476A] underline">View</Link>
              </li>
            ))}
          </ul>
        </div>
      )}

      {queue.needsLook.length > 0 && (
        <div className="flex flex-col gap-2">
          <div className="font-bold">Needs a look · {queue.needsLook.length}</div>
          <ul className="flex flex-col gap-1.5">
            {queue.needsLook.map((i) => (
              <li key={i.receiptId} className="flex items-center justify-between gap-3 rounded-lg border border-neutral-200 px-3 py-2">
                <div className="min-w-0">
                  <div className="font-semibold">{i.headline}</div>
                  <div className="text-xs text-neutral-500">{i.method ?? "Payment"}{i.senderName ? ` from ${i.senderName}` : ""}</div>
                </div>
                <Button variant="outline" render={<Link to={`/receipts/${i.receiptId}`} />}>Open</Button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {error && <p className="text-sm text-red-700">{error}</p>}
    </section>
  )
}