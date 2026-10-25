import type { IAggFuncParams } from "ag-grid-community"

// Family status from its children: all paid → Paid; nothing paid and something late → Late;
// a mix → Partial; otherwise Due.
export function familyStatus(p: IAggFuncParams): string {
    if (p.rowNode.level === -1) return ""
  const s = p.values as string[]
  if (s.length === 0) return ""
  if (s.every((v) => v === "Paid")) return "Paid"
  const anyPaid = s.some((v) => v === "Paid" || v === "Partial")
  if (anyPaid) return "Partial"
  if (s.some((v) => v === "Late")) return "Late"
  return "Due"
}

// "PayPal", "Zelle", "PayPal + Zelle" — every way this family paid, no repeats.
export function distinctJoin(p: IAggFuncParams): string {
  const parts = (p.values as string[])
    .flatMap((v) => (v ?? "").split(" + "))
    .filter(Boolean)
  return [...new Set(parts)].join(" + ")
}