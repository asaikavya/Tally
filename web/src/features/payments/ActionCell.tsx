import type { CustomCellRendererProps } from "ag-grid-react"
import { Button } from "@/components/ui/button"
import type { PaymentRow } from "./types"

// One button per row, chosen by status. They come alive in later steps.
export function ActionCell(props: CustomCellRendererProps<PaymentRow>) {
  const row = props.data
  if (!row) return null // family and total rows have no actions

  if (row.status === "Paid")
    return <Button size="sm" variant="ghost" disabled title="Receipts come in step 12">Receipt</Button>
  
 if (row.status === "Says paid")
    return <Button size="sm" variant="outline" disabled title="Comes in step 10">Confirm</Button>

  if (row.status === "Late" || row.status === "Due" || row.status === "Partial")
    return <Button size="sm" variant="outline" disabled title="Comes in step 12">Resend link</Button>
  
  

  return null
}