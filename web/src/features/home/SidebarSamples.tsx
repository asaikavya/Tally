import { useState } from "react"
import { useNavigate } from "react-router"
import { Smartphone } from "lucide-react"
import { SAMPLES, runSample, type Sample } from "./samples"

// Laptop only: demo controls in the sidebar. Tan = pretend; white = the real app.
export function SidebarSamples() {
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
    <section className="mx-3 mt-4 hidden flex-col gap-2 rounded-xl border border-[#E8D5B0] bg-[#FBF3E4] p-3 md:flex">
      <span className="flex items-center gap-1.5 text-xs font-semibold text-[#6B4E16]">
        <Smartphone className="size-3.5" aria-hidden="true" /> More cases to try
      </span>
      {SAMPLES.filter((s) => s.key !== "exact").map((s) => (
        <button key={s.key} type="button" disabled={busy !== null} onClick={() => run(s)}
          className="flex flex-col items-start rounded-lg border border-[#E8D5B0] bg-white px-2.5 py-1.5 text-left hover:border-[#16476A] disabled:cursor-wait disabled:opacity-60">
          <span className="text-sm font-semibold">{busy === s.key ? "Reading…" : s.label}</span>
          <span className="text-xs text-neutral-600">{s.hint}</span>
        </button>
      ))}
      {error && <p className="text-xs text-red-700">{error}</p>}
    </section>
  )
}