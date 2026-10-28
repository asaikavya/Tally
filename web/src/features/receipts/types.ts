export type Receipt = {
  id: string
  status: "Uploaded" | "Read" | "NeedsReview" | "Duplicate" | "Matched" | "Rejected"
  uploadedBy: "owner" | "parent"
  uploadedAtUtc: string
  amount: number | null
  currency: string | null
  paidOn: string | null
  senderName: string | null
  paidTo: string | null
  memo: string | null
  reference: string | null
  method: string | null
  problem: string | null
  duplicateOfId: string | null
  edited: string[]
 aiRead: boolean
 corrections: Correction[]      // add inside the Receipt type
}

export type Correction = { field: string; aiValue: string | null; nowValue: string | null }