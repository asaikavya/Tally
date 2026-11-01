import { useState } from "react"
import { useNavigate } from "react-router"
import { SAMPLES, runSample, type Sample } from "./samples"

// Phone only (the sidebar has these on laptops). Folded so Home stays short.
export function SampleReceipts() {
  const navigate = useNavigate()
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function run(s: Sample) {
    setBusy(s.key); setError(null)
    try { navigate(`/receipts/${await runSample(s)}`) }
    catch (e) { setError((e as Error).message) }
    finally { setBusy(null) }
  }

  return (
    <details className="rounded-xl border border-[#E8D5B0] bg-[#FBF3E4] p-3 md:hidden">
         <summary className="cursor-pointer text-sm font-semibold text-[#6B4E16]">More cases to try</summary>
      <div className="mt-3 grid grid-cols-2 gap-2">
        {SAMPLES.map((s) => (
          <button key={s.key} type="button" disabled={busy !== null} onClick={() => run(s)}
            className="flex flex-col items-start rounded-lg border border-[#E8D5B0] bg-white p-2.5 text-left disabled:opacity-60">
            <span className="text-sm font-semibold">{busy === s.key ? "Reading…" : s.label}</span>
            <span className="text-xs text-neutral-600">{s.hint}</span>
          </button>
        ))}
      </div>
      {error && <p className="mt-2 text-sm text-red-700">{error}</p>}
    </details>
  )
}