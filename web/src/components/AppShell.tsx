import { NavLink, Outlet } from "react-router"
import { useEffect, useState } from "react"

type ResetInfo = { families: number; payPalBillsSent: number; warnings: string[] }

const link = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center rounded-lg px-3 text-[15px] ${
    isActive ? "bg-[#EEF3F7] font-semibold text-[#16476A]" : "text-neutral-700 hover:bg-neutral-100"
  }`

export function AppShell() {
  // --- state (like fields on the page) ---
  const [resetting, setResetting] = useState(false)
  const [resetInfo, setResetInfo] = useState<ResetInfo | null>(null)

  // --- runs once when the page loads: show the "Demo reset" message if a reset just happened ---
  useEffect(() => {
    try {
      const saved = sessionStorage.getItem("tally-reset")
      if (saved) {
        setResetInfo(JSON.parse(saved))
        sessionStorage.removeItem("tally-reset")
      }
    } catch { /* storage blocked: no message */ }
  }, [])

  // --- the Reset button's action ---
  const resetDemo = async () => {
    setResetting(true)
    try {
      const res = await fetch("/api/demo/reset", { method: "POST" })
      if (!res.ok) throw new Error(String(res.status))
      const result = await res.json()
      try { sessionStorage.setItem("tally-reset", JSON.stringify(result)) } catch { /* storage blocked: skip the message */ }
      window.location.reload()
    } catch {
      setResetting(false)
      alert("Reset failed. Check that the API is running.")
    }
  }

  return (
    <div className="flex min-h-svh flex-wrap bg-[#F6F5F1] text-neutral-900">
      <aside className="flex w-full flex-col gap-5 border-r border-neutral-200 bg-white p-4 md:min-h-svh md:w-56">
        <div className="flex items-center gap-2.5 px-1.5">
          <div className="flex size-8 items-center justify-center rounded-lg bg-[#1F5F8B] font-bold text-white">T</div>
          <div>
            <div className="text-lg font-bold leading-tight">Tally</div>
            <div className="text-xs text-neutral-500">Melody Music Academy</div>
          </div>
        </div>
        <nav className="flex flex-col gap-0.5">
          <NavLink to="/" end className={link}>Home</NavLink>
          <NavLink to="/payments" className={link}>Payments</NavLink>
        </nav>
        <div className="mt-auto rounded-xl border border-[#CFE0EC] bg-[#F2F7FB] p-3">
          <div className="text-sm font-bold text-[#16476A]">✓ Connected to PayPal</div>
          <div className="mt-1 text-xs leading-snug text-neutral-700">
            PayPal bills and payments sync automatically. Payments made outside PayPal are noted on the bill.
          </div>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        {/* Demo bar */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#F0D9A0] bg-[#FFF6DF] px-6 py-2.5 text-sm text-[#5C4300]">
          <span>
            <strong>Demo mode</strong> · sample academy, PayPal sandbox. Try anything, then reset.
          </span>
          <button
            title="Reloads the sample academy and sends fresh PayPal sandbox bills. Takes about 10 seconds."
            onClick={resetDemo}
            disabled={resetting}
            className="min-h-10 rounded-lg bg-[#7A4F00] px-4 font-semibold text-white hover:bg-[#5C3B00] disabled:opacity-60"
          >
            {resetting ? "Resetting… (talking to PayPal)" : "↺ Reset demo"}
          </button>
        </div>

        {/* "Demo reset" message, shown once after a reset */}
        {resetInfo && (
          <div role="status" className="flex flex-wrap items-start justify-between gap-3 border-b border-[#BFE3CB] bg-[#EEF8F1] px-6 py-3 text-sm text-[#1E4D2E]">
            <div>
              <strong>Demo reset.</strong> {resetInfo.families} families loaded, {resetInfo.payPalBillsSent} fresh PayPal sandbox bills sent.
              <span className="mt-0.5 block">Try: open Payments to see each family's PayPal bill, or open a family's pay link and tap “I already paid”.</span>
              {resetInfo.warnings.length > 0 && (
                <span className="mt-1 block text-[#7A4F00]">Note: {resetInfo.warnings.join(" · ")}</span>
              )}
            </div>
            <button onClick={() => setResetInfo(null)} className="min-h-9 rounded-md px-3 font-semibold hover:bg-[#DDF0E3]" aria-label="Dismiss">✕</button>
          </div>
        )}

        <Outlet />
      </div>
    </div>
  )
}