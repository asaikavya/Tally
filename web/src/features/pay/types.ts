export type PayLine = { student: string; offering: string; amountDue: number; remaining: number }

export type PayPage = {
  academy: string
  payerName: string
  period: string
  lines: PayLine[]
  total: number
  remaining: number
  dueDate: string | null
  payUrl: string | null
  payPalStatus: string | null
  parentSaysPaid: boolean
}