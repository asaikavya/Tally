// $90 stays "$90"; $88.2 becomes "$88.20". Never round real money away.
const whole = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 })
const cents = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function money(n: number): string {
  return Math.round(n * 100) % 100 === 0 ? whole.format(n) : cents.format(n)
}