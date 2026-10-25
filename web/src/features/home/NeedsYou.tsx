import { useEffect, useState } from "react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import { q } from "@/lib/demo"
import type { NeedsYouItem } from "./types"

export function NeedsYou() {
  const [items, setItems] = useState<NeedsYouItem[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getJson<NeedsYouItem[]>(`/api/home/needs-you?${q}`).then(setItems).catch((e: Error) => setError(e.message))
  }, [])

  return (
    <section className="overflow-hidden rounded-2xl border border-neutral-200 bg-white">
      <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-neutral-200 px-5 py-4">
        <h2 className="text-lg font-bold">
          Needs you {items && <span className="font-medium text-neutral-500">· {items.length}</span>}
        </h2>
        <span className="text-sm text-neutral-500">Everything else is done.</span>
      </div>

      {error && <p className="px-5 py-4 text-red-700">Couldn't load: {error}</p>}
      {!items && !error && <p className="px-5 py-4 text-neutral-500">Loading…</p>}
      {items?.length === 0 && <p className="px-5 py-4 text-neutral-500">Nothing needs you today.</p>}

      {items?.map((item) => (
        <div key={item.payerId} className="flex flex-wrap items-center gap-3 border-b border-neutral-100 px-5 py-4 last:border-b-0">
          <div className="min-w-0 flex-[1_1_320px]">
            <div className="flex flex-wrap items-center gap-2 text-base font-semibold">
              <Badge variant="destructive">Late</Badge>
              {item.payerName} · {money(item.amount)}
            </div>
            <p className="mt-1 text-sm text-neutral-600">
              {item.lines.join(" · ")} — {item.daysLate} {item.daysLate === 1 ? "day" : "days"} late
              {item.country && item.country !== "US" ? ` · pays from ${item.country}` : ""}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" disabled title="Comes in step 10">Paid another way</Button>
            <Button variant="outline" disabled title="Comes in step 12">Resend PayPal link</Button>
          </div>
        </div>
      ))}
    </section>
  )
}