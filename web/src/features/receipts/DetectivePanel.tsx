import { useEffect, useState } from "react"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import type { DetectiveResult, Evidence, Finding } from "./detectiveTypes"

const chip: Record<string, string> = {
  Strong: "bg-[#E3F1E7] text-[#1E6B3A]",
  Medium: "bg-[#FBF0D9] text-[#7A4F00]",
  Weak: "bg-neutral-100 text-neutral-600",
}

function EvidenceList({ items }: { items: Evidence[] }) {
  if (items.length === 0) return null
  return (
    <ul className="flex flex-col gap-2">
      {items.map((e, i) => (
        <li key={i} className="flex items-start gap-2 text-[15px]">
          <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-bold ${chip[e.strength]}`}>{e.strength}</span>
          <span>{e.text}</span>
        </li>
      ))}
    </ul>
  )
}

function Step({ n, title, children }: { n: number; title: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="flex gap-3">
      <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-[#16476A] text-sm font-bold text-white">{n}</span>
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <div className="font-bold">{title}</div>
        {children}
      </div>
    </div>
  )
}

function FamilyDetail({ f }: { f: Finding }) {
  const option = f.coverage.options[0]
  const picked = new Map(option?.lines.map((l) => [l.chargeId, l.amount]) ?? [])
  return (
    <>
      <Step n={1} title={<>Who sent it? <span className="text-[#1E6B3A]">{f.name}</span>
        {f.sender && <span className="ml-2 text-sm font-medium text-neutral-500">({f.sender.confidence.toLowerCase()})</span>}</>}>
        <EvidenceList items={f.sender?.evidence ?? []} />
      </Step>

      <Step n={2} title={`What it covers: ${f.name.split(" ")[0]} has ${f.openCharges.length} open ${f.openCharges.length === 1 ? "charge" : "charges"}`}>
        <div className="flex flex-col gap-1.5">
          {f.openCharges.map((c) => {
            const amt = picked.get(c.chargeId)
            const on = amt !== undefined
            return (
              <div key={c.chargeId} className={`flex items-center justify-between rounded-lg border px-3 py-2 ${on ? "border-[#9FD1AF] bg-[#EEF8F1]" : "border-neutral-200 text-neutral-500"}`}>
                <span className="flex items-center gap-2">
                  <span className={`flex size-5 items-center justify-center rounded text-xs font-bold ${on ? "bg-[#1E6B3A] text-white" : "border border-neutral-300"}`}>{on ? "✓" : ""}</span>
                  {c.student.split(" ")[0]} · {c.offering}
                </span>
                <span className="font-semibold">{on && amt! < c.remaining ? `${money(amt!)} of ${money(c.remaining)}` : money(c.remaining)}</span>
              </div>
            )
          })}
        </div>
        {f.coverage.tried.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {f.coverage.tried.map((t, i) => (
              <span key={i} className={`rounded-full px-2.5 py-1 text-sm ${t.fits ? "bg-[#1E6B3A] font-bold text-white" : "bg-neutral-100 text-neutral-500 line-through"}`}>
                {t.label} = {money(t.sum)}{t.fits ? " ✓" : ""}
              </span>
            ))}
          </div>
        )}
        <EvidenceList items={[...f.coverage.evidence, ...f.extra]} />
        {f.coverage.options.length > 1 && (
          <p className="text-sm text-neutral-600">Other ways it fits: {f.coverage.options.slice(1).map((o) => o.lines.map((l) => l.label).join(" + ")).join(" · ")}</p>
        )}
      </Step>

      {option && (
        <Step n={3} title="What Tally will do after you approve">
          <ul className="list-disc pl-5 text-[15px]">
            {option.lines.map((l) => <li key={l.chargeId}>{money(l.amount)} to {l.label}</li>)}
            {option.leftover > 0 && <li>{money(option.leftover)} kept as family credit</li>}
            <li>{option.stillDueAfter > 0 ? `${money(option.stillDueAfter)} still due, and a receipt goes out with a PayPal link for the rest` : "Nothing left to pay. A thank-you receipt goes out"}</li>
          </ul>
          {option.note && <p className="text-sm text-[#7A4F00]">{option.note}</p>}
          <Button disabled title="Comes in Step 10" className="min-h-12 self-start">Approve &amp; note on PayPal bill</Button>
        </Step>
      )}
    </>
  )
}

export function DetectivePanel({ receiptId, status }: { receiptId: string; status: string }) {
  const [result, setResult] = useState<DetectiveResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (status === "Duplicate") return
    getJson<DetectiveResult>(`/api/receipts/${receiptId}/detective`).then(setResult).catch((e: Error) => setError(e.message))
  }, [receiptId, status])

  if (status === "Duplicate") return null
  if (error) return <p className="text-red-700">Detective couldn't run: {error}</p>
  if (!result) return <p className="text-neutral-500">Working it out…</p>

  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-neutral-200 bg-white p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-xl font-extrabold">{result.headline}</h2>
        {result.verdict === "Suggested" && <span className="rounded-full bg-[#FBF0D9] px-3 py-1 text-sm font-bold text-[#7A4F00]">Needs your approval</span>}
      </div>

      {result.best && <FamilyDetail f={result.best} />}

      {result.verdict === "ChooseFamily" && (
        <div className="flex flex-col gap-3">
          <p className="text-neutral-600">The receipt doesn't say clearly who paid, so Tally won't guess. Which family is it?</p>
          {result.candidates.map((c) => (
            <div key={c.payerId} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-neutral-200 p-3">
              <div>
                <div className="font-semibold">{c.name}</div>
                <div className="text-sm text-neutral-600">{c.coverage.evidence[0]?.text}</div>
              </div>
              <Button variant="outline" disabled title="Comes in Step 10">It's this family</Button>
            </div>
          ))}
        </div>
      )}

      {result.ruledOut.length > 0 && (
        <div className="rounded-lg bg-[#F6F5F1] p-3 text-sm">
          <strong>Also checked:</strong> {result.ruledOut.join(" · ")}
        </div>
      )}
      <p className="text-xs text-neutral-500">AI only read the receipt. Finding the family and every combination is Tally's own code. Nothing is recorded until you approve.</p>
    </section>
  )
}