// PayPal's invoice status → words an academy owner understands.
export const paypalStatusLabel: Record<string, string> = {
  DRAFT: "draft",
  SENT: "sent",
  PARTIALLY_PAID: "partly paid",
  MARKED_AS_PAID: "paid outside PayPal",
  PAID: "paid",
  CANCELLED: "cancelled",
  REFUNDED: "refunded",
}