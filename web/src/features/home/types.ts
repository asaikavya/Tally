export type HomeSummary = {
  period: string
  billed: number
  paidThroughPayPal: number
  recordedOutsidePayPal: number
  stillDue: number
  late: number
  lateCharges: number
  payPalBills: number
}
export type NeedsYouItem = {
  kind: "late" | "parent-says-paid"
  payerId: string
  payerName: string
  country: string | null
  amount: number
  daysLate: number
  lines: string[]
  note: string | null
  payPalInvoiceId: string | null
  payPalStatus: string | null
}