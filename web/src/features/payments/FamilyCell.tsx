import type { CustomCellRendererProps } from "ag-grid-react"
import type { PaymentRow } from "./types"
import { paypalStatusLabel } from "@/lib/paypalStatus"
import { money } from "@/lib/money"

export function FamilyCell(props: CustomCellRendererProps<PaymentRow>) {
      if (props.node.footer) return <strong>November total</strong>
  if (!props.node.group) return null
  const first = props.node.allLeafChildren?.[0]?.data
  const count = props.node.allLeafChildren?.length ?? 0
  return (
    <span className="flex items-center gap-2">
      <strong>{props.value}</strong>
      <span className="text-neutral-500">({count})</span>
      {first?.payPalInvoiceId && first.payPalStatus && (
        <span
          title={`PayPal status: ${first.payPalStatus}`}
          className="whitespace-nowrap rounded border border-[#CFE0EC] bg-[#F2F7FB] px-2 py-0.5 text-xs font-semibold text-[#16476A]">
          PayPal · {paypalStatusLabel[first.payPalStatus] ?? first.payPalStatus.toLowerCase()}
        </span>
      )}
            {(first?.familyCredit ?? 0) > 0 && (
        <span title="Paid more than was billed. Kept for the next bill."
          className="whitespace-nowrap rounded border border-[#B7DCC3] bg-[#EEF8F1] px-2 py-0.5 text-xs font-semibold text-[#1E6B3A]">
          {money(first!.familyCredit)} credit
        </span>
      )}
      
    </span>
    
    
  )
}