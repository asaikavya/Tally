
import { useState } from "react"
import { useNavigate } from "react-router"
import { SAMPLES, runSample, type Sample } from "./samples"
// Load the demo image and send it through the normal upload, exactly like "Upload a receipt".


export function SampleReceipts() {
  const navigate = useNavigate()
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

    async function run(s: Sample) {
    setBusy(s.key); setError(null)
    try {
      navigate(`/receipts/${await runSample(s)}`)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(null)
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl border border-dashed border-[#B9D0E2] bg-[#F7FAFC] p-4">
      <div>
        <span className="font-bold">Try a sample receipt</span>
        <span className="ml-2 text-sm text-neutral-600">Each one shows a different rule. Works best right after Reset demo.</span>
      </div>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        {SAMPLES.map((s) => (
          <button key={s.key} type="button" disabled={busy !== null} onClick={() => run(s)}
            className="flex flex-col items-start gap-1 rounded-xl border border-neutral-200 bg-white p-3 text-left transition hover:border-[#16476A] disabled:cursor-wait disabled:opacity-60">
            <span className="font-semibold">{busy === s.key ? "Reading the receipt…" : s.label}</span>
            <span className="text-sm text-neutral-600">{s.hint}</span>
          </button>
        ))}
      </div>
      {error && <p className="text-sm text-red-700">{error}</p>}
    </section>
  )
}