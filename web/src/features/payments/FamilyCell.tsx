import type { CustomCellRendererProps } from "ag-grid-react"
import type { PaymentRow } from "./types"

export function FamilyCell(props: CustomCellRendererProps<PaymentRow>) {
      if (props.node.footer) return <strong>November total</strong>
  if (!props.node.group) return null
  const first = props.node.allLeafChildren?.[0]?.data
  const count = props.node.allLeafChildren?.length ?? 0
  return (
    <span className="flex items-center gap-2">
      <strong>{props.value}</strong>
      <span className="text-neutral-500">({count})</span>
      {first?.payPalInvoiceId && (
        <span className="whitespace-nowrap rounded border border-[#CFE0EC] bg-[#F2F7FB] px-2 py-0.5 font-mono text-xs text-[#16476A]">
          PayPal {first.payPalStatus}
        </span>
      )}
    </span>
  )
}