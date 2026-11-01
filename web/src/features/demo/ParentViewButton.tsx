import { useEffect, useState } from "react"
import { Smartphone } from "lucide-react"
import { getJson, postFor } from "@/lib/api"
import type { Progress } from "./guideSteps"

// Demo bar shortcut: open what a parent sees (Ravi's private family page), in a new tab.
export function ParentViewButton() {
  const [raviId, setRaviId] = useState<string | null>(null)

  useEffect(() => {
    getJson<Progress>("/api/demo/progress").then((p) => setRaviId(p.raviPayerId)).catch(() => setRaviId(null))
  }, [])

  async function open() {
    if (!raviId) return
    const tab = window.open("about:blank", "_blank")           // open during the click so it isn't blocked
    try {
      const r = await postFor<{ url: string }>(`/api/payers/${raviId}/pay-link`)
      const url = r.url.startsWith("/") ? `${window.location.origin}${r.url}` : r.url
      if (tab) { tab.opener = null; tab.location.href = url }
    } catch { tab?.close() }
  }

  if (!raviId) return null                                     // demo off: show nothing
  return (
    <button onClick={open} title="What a parent sees on their phone"
      className="flex min-h-10 items-center gap-1.5 rounded-lg border border-[#16476A] bg-white px-3 font-semibold text-[#16476A] shadow-sm hover:bg-[#EEF3F7]">
      <Smartphone className="size-4" strokeWidth={2.5} aria-hidden="true" />
      <span className="hidden sm:inline">Parent's view ↗</span>
    </button>
  )
}