import { NavLink, Outlet } from "react-router"
import { useEffect, useState } from "react"
import { SidebarSamples } from "@/features/home/SidebarSamples"
import { GuideCard } from "@/features/demo/GuideCard"
import { ParentViewButton } from "@/features/demo/ParentViewButton"
import { TestBuyerLogin } from "@/features/demo/TestBuyerLogin"
import { checkPayPalIfReturning } from "@/lib/paypalReturn"

type ResetInfo = { families: number; payPalBillsSent: number; warnings: string[] }

const link = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center rounded-lg px-3 text-[15px] ${
    isActive ? "bg-[#EEF3F7] font-semibold text-[#16476A]" : "text-neutral-700 hover:bg-neutral-100"
  }`

export function AppShell() {
  const [resetting, setResetting] = useState(false)
  const [toast, setToast] = useState<string | null>(null)
  const [ready, setReady] = useState(false)   // true once we know the demo isn't being reset under us

  // After a reset, show one short message.
  useEffect(() => {
    try {
      const saved = sessionStorage.getItem("tally-reset")
      if (saved) {
        sessionStorage.removeItem("tally-reset")
        const r = JSON.parse(saved) as ResetInfo
        setToast(`Fresh start · ${r.payPalBillsSent} PayPal bills sent. Open Guide to begin.`
          + (r.warnings.length > 0 ? ` Note: ${r.warnings.join(" · ")}` : ""))
      }
    } catch { /* storage blocked: skip the message */ }
  }, [])

  // The toast disappears on its own after 6 seconds.
  useEffect(() => {
    if (!toast) return
    const t = window.setTimeout(() => setToast(null), 6000)
    return () => window.clearTimeout(t)
  }, [toast])

    // Opening the app: if the shared demo was used and then left alone, the server starts it fresh.
  // Opening the app: if the shared demo was used and then left alone, the server starts it fresh FIRST,
  // so nobody sees a half-empty demo.
  useEffect(() => {
    fetch("/api/demo/fresh-if-idle", { method: "POST" })
      .then((r) => (r.ok ? r.json() : null))
      .then((d) => {
        if (d?.reset) {
          try { sessionStorage.setItem("tally-reset", JSON.stringify(d.result)) } catch { /* skip the message */ }
          window.location.assign("/")
          return
        }
        setReady(true)
      })
      .catch(() => setReady(true))            // never block the app because of this check
  }, [])

    // Coming back from a PayPal tab: ask PayPal for news (covers local, and a slow webhook on live).
  useEffect(() => {
    const onFocus = () => { void checkPayPalIfReturning() }
    window.addEventListener("focus", onFocus)
    return () => window.removeEventListener("focus", onFocus)
  }, [])

  const resetDemo = async () => {
  if (!window.confirm("Start over? This resets the shared demo for everyone using it.")) return
    setResetting(true)
    try {
      const res = await fetch("/api/demo/reset", { method: "POST" })
      if (!res.ok) throw new Error(String(res.status))
      const result = await res.json()
      try { sessionStorage.setItem("tally-reset", JSON.stringify(result)) } catch { /* skip the message */ }
      window.location.assign("/")   // the page you were on may not exist after a reset
    } catch {
      setResetting(false)
      setToast("Reset didn't work. Try again in a moment.")
    }
  }

  return (
    <div className="flex min-h-svh flex-col bg-[#F6F5F1] text-neutral-900 md:flex-row">
      {/* Phone: slim top bar. Laptop: left sidebar. */}
      <aside className="flex flex-col gap-3 border-b border-neutral-200 bg-white p-3 md:min-h-svh md:w-56 md:gap-5 md:border-r md:border-b-0 md:p-4">
          <NavLink to="/" className="flex items-center gap-2.5 rounded-lg px-1.5 hover:opacity-80" aria-label="Tally home">
          <div className="flex size-8 items-center justify-center rounded-lg bg-[#1F5F8B] font-bold text-white">T</div>
          <div>
            <div className="text-lg font-bold leading-tight">Tally</div>
            <div className="text-xs text-neutral-500">for Melody Music Academy</div>
          </div>
        </NavLink>
        <nav className="flex flex-row gap-1 md:flex-col md:gap-0.5">
          <NavLink to="/" end className={link}>Home</NavLink>
          <NavLink to="/payments" className={link}>Payments</NavLink>
        </nav>
        <SidebarSamples />
        <div className="mt-auto hidden rounded-xl border border-[#CFE0EC] bg-[#F2F7FB] p-3 md:block">
          <div className="text-sm font-bold text-[#16476A]">✓ Connected to PayPal Sandbox</div>
          <div className="mt-1 text-xs leading-snug text-neutral-700">
            PayPal payments update here automatically. Payments made outside PayPal are noted on the bill.
          </div>
        </div>
      </aside>

      <div className="min-w-0 flex-1 pb-24">
        {/* One thin bar: what this is + reset */}
        <div className="flex items-center justify-between gap-3 border-b border-[#F0D9A0] bg-[#FFF6DF] px-4 py-2 text-sm text-[#5C4300] md:px-6">
        <span><strong>Demo</strong> · Sandbox · shared · starts fresh after 30 min idle</span>
        <div className="flex items-center gap-2">
            <TestBuyerLogin />
            <ParentViewButton />
            <button
              title="Reloads the sample academy and sends fresh PayPal sandbox bills. Takes about 10 seconds."
              onClick={resetDemo}
              disabled={resetting}
              className="min-h-10 rounded-lg bg-[#7A4F00] px-4 font-semibold text-white hover:bg-[#5C3B00] disabled:opacity-60"
            >
              {resetting ? "Resetting…" : "↺ Reset"}
            </button>
          </div>
        </div>
         {resetting ? (
          <p className="p-8 text-neutral-500">Starting the demo fresh… about 10–20 seconds.</p>
        ) : ready ? <Outlet /> : (
          <p className="p-8 text-neutral-500">Getting the demo ready… The first visit can take up to a minute.</p>
        )}
      </div>

      {toast && (
        <div role="status" className="fixed bottom-4 left-4 z-40 max-w-sm rounded-xl bg-[#1E4D2E] px-4 py-3 text-sm text-white shadow-lg">
          {toast}
        </div>
      )}
      <GuideCard />
    </div>
  )
}