export type PaymentRow = {
  chargeId: string
  payerId: string
  family: string
  student: string
  offering: string
  plan: string
  amountDue: number
  amountPaid: number
  remaining: number
  dueDate: string
  status: "Paid" | "Partial" | "Says paid" | "Late" | "Due"
  paidVia: string
  payPalInvoiceId: string | null
  payPalStatus: string | null
familyCredit: number
}