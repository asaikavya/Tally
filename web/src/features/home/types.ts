export type HomeSummary = {
  period: string
  billed: number
  paidThroughPayPal: number
  recordedOutsidePayPal: number
  stillDue: number
  late: number
  lateCharges: number
}