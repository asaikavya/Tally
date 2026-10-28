import { useState } from "react"
import { useNavigate } from "react-router"
import { postFile } from "@/lib/api"
import type { Receipt } from "@/features/receipts/types"

type Sample = { key: string; label: string; hint: string; file: string; twice?: boolean }

// Each sample shows one rule of the Payment Detective.
const SAMPLES: Sample[] = [
  { key: "exact", label: "$170 Zelle, no memo", hint: "Finds the family, and the only 2 classes that add up to $170", file: "zelle-r-kumar-170.png" },
  { key: "close", label: "$88.20 from Mexico", hint: "Within 2% of a $90 class, so probably a transfer fee", file: "transfer-lucia-lopez-88-20.png" },
  { key: "over",  label: "$300 check", hint: "Pays all 3 classes, and $30 becomes family credit", file: "check-ravi-kumar-300.png" },
  { key: "dup",   label: "Same receipt twice", hint: "The second upload is caught as a duplicate", file: "zelle-r-kumar-150.png", twice: true },
]

// Load the demo image and send it through the normal upload, exactly like "Upload a receipt".
async function uploadSample(file: string): Promise<string> {
  const img = await fetch(`/samples/${file}`)
  if (!img.ok) throw new Error(`Sample image ${file} is missing from web/public/samples`)
  const r = await postFile<Receipt>("/api/receipts", new File([await img.blob()], file, { type: "image/png" }))
  if (!r) throw new Error("Upload didn't return a receipt")
  return r.id
}

export function SampleReceipts() {
  const navigate = useNavigate()
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function run(s: Sample) {
    setBusy(s.key); setError(null)
    try {
      let id = await uploadSample(s.file)
      if (s.twice) id = await uploadSample(s.file)     // open the second copy: the duplicate
      navigate(`/receipts/${id}`)
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