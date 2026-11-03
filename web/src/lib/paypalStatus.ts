// PayPal's invoice status → words an academy owner understands. Used on Home and Payments.
export const paypalStatusLabel: Record<string, string> = {
  DRAFT: "Not sent yet",
  SENT: "Unpaid",
  UNPAID: "Unpaid",
  PARTIALLY_PAID: "Partly paid",
  MARKED_AS_PAID: "Paid outside PayPal",
  PAID: "Paid",
  CANCELLED: "Cancelled",
  REFUNDED: "Refunded",
}