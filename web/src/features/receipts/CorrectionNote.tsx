import { money } from "@/lib/money"
import type { Correction } from "./types"

const label: Record<string, string> = {
  Amount: "Amount", Currency: "Currency", PaidOn: "Date", SenderName: "Sender",
  PaidTo: "Paid to", Memo: "Memo", Reference: "Reference", Method: "Method",
}

function show(field: string, v: string | null): string {
  if (v === null || v === "") return "nothing"
  if (field === "Amount") {
    const n = Number(v)
    return Number.isNaN(n) ? v : money(n)
  }
  if (field === "PaidOn") {
    const d = new Date(`${v}T00:00:00`)
    return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })
  }
  return `"${v}"`
}

// "AI read $107 → $170": the owner's fix, with the AI's original kept visible.
export function CorrectionNote({ corrections }: { corrections: Correction[] | undefined }) {
  if (!corrections || corrections.length === 0) return null
  return (
    <div className="flex flex-col gap-1.5 rounded-lg border border-[#E9D9B5] bg-[#FBF6EA] p-3 text-sm">
      <div className="font-semibold text-[#7A4F00]">Corrected by the owner · both versions kept</div>
      {corrections.map((c) => (
        <div key={c.field}>
          {label[c.field] ?? c.field}: AI read{" "}
          <span className="text-neutral-500 line-through">{show(c.field, c.aiValue)}</span>
          {" → "}
          <strong>{show(c.field, c.nowValue)}</strong>
        </div>
      ))}
    </div>
  )
}