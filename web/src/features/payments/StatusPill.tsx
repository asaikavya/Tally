import type { CustomCellRendererProps } from "ag-grid-react"

const styles: Record<string, string> = {
  Paid: "bg-[#E3F1E7] text-[#1E6B3A]",
  Partial: "bg-[#FBF0D9] text-[#7A4F00]",
  Late: "bg-[#FBE4DF] text-[#A8321F]",
  Due: "bg-neutral-100 text-neutral-700",
}

// Shows a colored pill. Group rows (the family line) have no status, so they show nothing.
export function StatusPill(props: CustomCellRendererProps) {
  const value = props.value as string | undefined
  if (!value) return null
  return (
    <span className={`inline-block min-w-20 rounded-full px-2.5 py-0.5 text-center text-xs font-semibold ${styles[value] ?? ""}`}>
      {value}
    </span>
  )
}