import {Fragment, useCallback, useEffect, useState } from "react"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import type { ApproveResult, DetectiveResult, Evidence, Finding, OpenCharge } from "./detectiveTypes"


const chip: Record<string, string> = {
  Strong: "bg-[#E3F1E7] text-[#1E6B3A]",
  Medium: "bg-[#FBF0D9] text-[#7A4F00]",
  Weak: "bg-neutral-100 text-neutral-600",
}

// POST that reads our friendly { problem } message on 409.
async function send<T>(url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method: "POST",
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  })
  const data = await res.json().catch(() => null)
  if (!res.ok) throw new Error(data?.problem ?? `Something went wrong (${res.status})`)
  return data as T
}

// Same order as the server: oldest bill, then student, then class.
const byDueStudentClass = (a: OpenCharge, b: OpenCharge) =>
  a.dueDate.localeCompare(b.dueDate) || a.student.localeCompare(b.student) || a.offering.localeCompare(b.offering)

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

function FamilyDetail({ f, busy, onApprove }: {
  f: Finding; busy: boolean; onApprove: (payerId: string, chargeIds: string[]) => void
}) {
  const options = f.coverage.options
  // One way to fit → pre-selected. Several → the owner must pick; Tally never guesses.
  const [pick, setPick] = useState(options.length === 1 ? 0 : -1)
  const option = pick >= 0 ? options[pick] : undefined
  const picked = new Map(option?.lines.map((l) => [l.chargeId, l.amount]) ?? [])
  const charges = [...f.openCharges].sort(byDueStudentClass)

  return (
    <>
      <Step n={1} title={<>Who sent it? <span className="text-[#1E6B3A]">{f.name}</span>
        {f.sender && <span className="ml-2 text-sm font-medium text-neutral-500">({f.sender.confidence.toLowerCase()})</span>}</>}>
        <EvidenceList items={f.sender?.evidence ?? []} />
      </Step>

      <Step n={2} title={`What it covers: ${f.name.split(" ")[0]} has ${charges.length} open ${charges.length === 1 ? "charge" : "charges"}`}>
        <div className="flex flex-col gap-1.5">
          {charges.map((c) => {
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
        {options.length > 1 && (
          <div className="flex flex-col gap-2">
            <p className="text-sm font-semibold">More than one combination fits. Which classes is it for?</p>
            <div className="flex flex-wrap gap-2">
              {options.map((o, i) => (
                <Button key={i} variant={i === pick ? "default" : "outline"} onClick={() => setPick(i)}>
                  {o.lines.map((l) => l.label).join(" + ")}
                </Button>
              ))}
            </div>
          </div>
        )}
      </Step>

      <Step n={3} title="What Tally will do after you approve">
        {option ? (
          <>
            <ul className="list-disc pl-5 text-[15px]">
              {option.lines.map((l) => <li key={l.chargeId}>{money(l.amount)} to {l.label}</li>)}
              {option.leftover > 0 && <li>{money(option.leftover)} kept as family credit</li>}
              <li>{option.stillDueAfter > 0 ? `${money(option.stillDueAfter)} still due` : "Nothing left to pay"}</li>
            </ul>
             {option.note && option.leftover === 0 && <p className="text-sm text-[#7A4F00]">{option.note}</p>}
            <Button disabled={busy} className="min-h-12 self-start"
              onClick={() => onApprove(f.payerId, option.lines.map((l) => l.chargeId))}>
               {busy ? "Noting it on PayPal…" : "Approve & note on PayPal bill"}
            </Button>
          </>
        ) : (
          <p className="text-neutral-600">Pick which classes above first.</p>
        )}
      </Step>
    </>
  )
}

// PayPal's own words, with a friendly hint on hover.
const friendly: Record<string, string> = {
  DRAFT: "Draft", SENT: "Sent, waiting for payment", PARTIALLY_PAID: "Partly paid",
  MARKED_AS_PAID: "Paid (outside PayPal)", PAID: "Paid",
}

function StatusChip({ s }: { s: string | null }) {
  if (!s) return null
  return <span title={friendly[s] ?? s} className="rounded bg-[#E8F0F7] px-1.5 py-0.5 font-mono text-xs font-semibold text-[#16476A]">{s}</span>
}

function CheckRow({ ok, children }: { ok: boolean; children: React.ReactNode }) {
  return (
    <li className="flex items-start gap-3">
      <span className={`mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-full text-sm font-bold ${ok ? "bg-[#1E6B3A] text-white" : "bg-[#FBE3E0] text-[#A33A2B]"}`}>{ok ? "✓" : "!"}</span>
      <div className="flex min-w-0 flex-col gap-1">{children}</div>
    </li>
  )
}

function ApprovedView({ a, busy, onUndo, onRetry }: {
  a: ApproveResult; busy: boolean; onUndo: () => void; onRetry: () => void
}) {
  const allNoted = a.payPal.every((p) => p.noted)
  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-[#9FD1AF] bg-[#EEF8F1] p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-xl font-extrabold">✓ Approved: {money(a.amount)}</h2>
        <Button variant="outline" disabled={busy} onClick={onUndo}>{busy ? "Working…" : "Undo"}</Button>
      </div>

      <ol className="flex flex-col gap-4">
        <CheckRow ok>
          <span className="font-semibold">Recorded in Tally</span>
          <ul className="text-sm text-neutral-700">
            {a.lines.map((l) => (
              <li key={l.chargeId}>
                {money(l.applied)} to {l.student.split(" ")[0]} · {l.offering}
                {l.stillDue > 0 ? ` (${money(l.stillDue)} still due)` : " (paid)"}
              </li>
            ))}
            {a.credit > 0 && <li className="font-semibold text-[#16476A]">{money(a.credit)} kept as family credit</li>}
          </ul>
        </CheckRow>

        {a.payPal.length === 0 && (
          <CheckRow ok>
            <span className="font-semibold">No PayPal bill for these classes</span>
            <span className="text-sm text-neutral-600">Recorded in Tally only.</span>
          </CheckRow>
        )}

        {a.payPal.map((p) => (
          <Fragment key={p.payPalInvoiceId}>
            <CheckRow ok={p.noted}>
              <span className="font-semibold">
                {p.noted ? `Noted on the PayPal bill: ${money(p.amount)} paid outside PayPal` : "Not on the PayPal bill yet"}
              </span>
              <span className="font-mono text-xs text-neutral-500">{p.payPalInvoiceId}</span>
            </CheckRow>
            {p.noted && (
              <CheckRow ok>
                <span className="font-semibold">PayPal's status, read back from PayPal</span>
                <span className="flex flex-wrap items-center gap-2 text-sm">
                  <StatusChip s={p.statusBefore} /> → <StatusChip s={p.statusAfter} />
                  <span className="text-neutral-600">{friendly[p.statusAfter ?? ""]}</span>
                </span>
              </CheckRow>
            )}
          </Fragment>
        ))}
      </ol>

      {!allNoted && (
        <Button disabled={busy} onClick={onRetry} className="self-start">Try PayPal again</Button>
      )}
    </section>
  )
}

export function DetectivePanel({ receiptId, status }: { receiptId: string; status: string }) {
  const [result, setResult] = useState<DetectiveResult | null>(null)
  const [approval, setApproval] = useState<ApproveResult | null>(null)
  const [chosen, setChosen] = useState<Finding | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [problem, setProblem] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const loadDetective = useCallback(() => {
    getJson<DetectiveResult>(`/api/receipts/${receiptId}/detective`).then(setResult).catch((e: Error) => setError(e.message))
  }, [receiptId])

  useEffect(() => {
    if (status === "Duplicate") return
    if (status === "Matched") {
      getJson<ApproveResult>(`/api/receipts/${receiptId}/approval`).then(setApproval).catch((e: Error) => setError(e.message))
    } else {
      loadDetective()
    }
  }, [receiptId, status, loadDetective])

  async function approve(payerId: string, chargeIds: string[]) {
    setBusy(true); setProblem(null)
    try { setApproval(await send<ApproveResult>(`/api/receipts/${receiptId}/approve`, { payerId, chargeIds })) }
    catch (e) { setProblem((e as Error).message) }
    finally { setBusy(false) }
  }

  async function undo() {
    setBusy(true); setProblem(null)
    try {
      await send(`/api/receipts/${receiptId}/undo`)
      setApproval(null); setResult(null); setChosen(null)
      loadDetective()
    }
    catch (e) { setProblem((e as Error).message) }
    finally { setBusy(false) }
  }
    async function retryPayPal() {
    setBusy(true); setProblem(null)
    try { setApproval(await send<ApproveResult>(`/api/receipts/${receiptId}/paypal-note`)) }
    catch (e) { setProblem((e as Error).message) }
    finally { setBusy(false) }
  }

  if (status === "Duplicate") return null
  if (error) return <p className="text-red-700">Detective couldn't run: {error}</p>

  const problemBox = problem && <p className="rounded-lg bg-red-50 p-3 text-sm text-red-800">{problem}</p>
  if (approval) return <div className="flex flex-col gap-3">{problemBox}<ApprovedView a={approval} busy={busy} onUndo={undo} onRetry={retryPayPal} /></div>
    if (!result) return <p className="text-neutral-500">Working it out…</p>

  const shown = result.best ?? chosen

  return (
    <section className="flex flex-col gap-5 rounded-2xl border border-neutral-200 bg-white p-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-xl font-extrabold">{result.headline}</h2>
        {result.verdict === "Suggested" && <span className="rounded-full bg-[#FBF0D9] px-3 py-1 text-sm font-bold text-[#7A4F00]">Needs your approval</span>}
      </div>

      {result.verdict === "ChooseFamily" && (
        <div className="flex flex-col gap-3">
          <p className="text-neutral-600">The receipt doesn't say clearly who paid, so Tally won't guess. Which family is it?</p>
          {result.candidates.map((c) => (
            <div key={c.payerId} className={`flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 ${chosen?.payerId === c.payerId ? "border-[#16476A] bg-[#F1F6FA]" : "border-neutral-200"}`}>
              <div>
                <div className="font-semibold">{c.name}</div>
                <div className="text-sm text-neutral-600">{c.coverage.evidence[0]?.text}</div>
              </div>
              <Button variant={chosen?.payerId === c.payerId ? "default" : "outline"} onClick={() => setChosen(c)}>
                {chosen?.payerId === c.payerId ? "Selected" : "It's this family"}
              </Button>
            </div>
          ))}
        </div>
      )}

      {shown && <FamilyDetail key={shown.payerId} f={shown} busy={busy} onApprove={approve} />}
      {problemBox}

      {result.ruledOut.length > 0 && (
        <div className="rounded-lg bg-[#F6F5F1] p-3 text-sm">
          <strong>Also checked:</strong> {result.ruledOut.join(" · ")}
        </div>
      )}
      <p className="text-xs text-neutral-500">AI read the receipt. Finding the family and checking every combination is Tally's own code. Nothing is recorded until you approve.</p>
    </section>
  )
}