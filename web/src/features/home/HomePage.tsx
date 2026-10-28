import { useEffect, useState } from "react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import type { HomeSummary } from "./types"
import { q } from "@/lib/demo"
import { NeedsYou } from "./NeedsYou"
import { UploadReceiptButton } from "@/features/receipts/UploadReceiptButton"
import { SampleReceipts } from "./SampleReceipts"
export function HomePage() {
  const [data, setData] = useState<HomeSummary | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getJson<HomeSummary>(`/api/home?${q}`)
      .then(setData)
      .catch((e: Error) => setError(e.message))
  }, [])

  if (error) return <p className="p-8 text-red-700">Couldn't load Home: {error}</p>
  if (!data) return <p className="p-8 text-neutral-500">Loading…</p>

  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-6 p-8">
      <div>
        <p className="text-sm text-neutral-500">November</p>
        <h1 className="text-3xl font-extrabold tracking-tight">
          Every payment, <span className="text-[#1F5F8B]">accounted for.</span>
           <UploadReceiptButton />
        </h1>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader><CardTitle className="text-sm text-neutral-500">Paid through PayPal</CardTitle></CardHeader>
          <CardContent>
            <p className="text-3xl font-bold">{money(data.paidThroughPayPal)}</p>
            <p className="text-sm text-neutral-500">of {money(data.billed)} billed</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm text-neutral-500">Recorded outside PayPal</CardTitle></CardHeader>
          <CardContent>
            <p className="text-3xl font-bold">{money(data.recordedOutsidePayPal)}</p>
            <p className="text-sm text-neutral-500">Zelle, UPI, cash, checks</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle className="text-sm text-neutral-500">Late</CardTitle></CardHeader>
          <CardContent>
            <p className="text-3xl font-bold text-red-700">{money(data.late)}</p>
            <p className="text-sm text-neutral-500">{data.lateCharges} charges past due</p>
          </CardContent>
        </Card>
      </div>
      <SampleReceipts />
      <NeedsYou />
    </main>
  )
}