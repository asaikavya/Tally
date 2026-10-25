export type HomeSummary = {
  period: string
  billed: number
  paidThroughPayPal: number
  recordedOutsidePayPal: number
  stillDue: number
  late: number
  lateCharges: number
}
export type NeedsYouItem = {
  kind: "late"
  payerId: string
  payerName: string
  country: string | null
  amount: number
  daysLate: number
  lines: string[]
}