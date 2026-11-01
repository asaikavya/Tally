import { NavLink, Outlet } from "react-router"
import { useCallback, useEffect, useState } from "react"
import { StoryBar } from "@/features/demo/StoryBar"
import { WelcomeCard } from "@/features/demo/WelcomeCard"
import { useShowMe } from "@/features/demo/useShowMe"
import { STEPS } from "@/features/demo/story"
import { SidebarSamples } from "@/features/home/SidebarSamples"

type ResetInfo = { families: number; payPalBillsSent: number; warnings: string[] }

const link = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center rounded-lg px-3 text-[15px] ${
    isActive ? "bg-[#EEF3F7] font-semibold text-[#16476A]" : "text-neutral-700 hover:bg-neutral-100"
  }`

export function AppShell() {
  const [resetting, setResetting] = useState(false)
  const [toast, setToast] = useState<string | null>(null)
  const [welcome, setWelcome] = useState(false)
  const showMe = useShowMe()

  // On load: a short "Demo reset" message if a reset just happened, and the welcome card on a first visit.
  useEffect(() => {
    try {
      const saved = sessionStorage.getItem("tally-reset")
      if (saved) {
        sessionStorage.removeItem("tally-reset")
        const r = JSON.parse(saved) as ResetInfo
        setToast(`Demo reset · ${r.families} families, ${r.payPalBillsSent} fresh PayPal bills. Start with step 1 above.`
          + (r.warnings.length > 0 ? ` Note: ${r.warnings.join(" · ")}` : ""))
      }
      if (!localStorage.getItem("tally-welcome-seen")) setWelcome(true)
    } catch { /* storage blocked: skip both */ }
  }, [])

  // The toast disappears on its own after 6 seconds.
  useEffect(() => {
    if (!toast) return
    const t = window.setTimeout(() => setToast(null), 6000)
    return () => window.clearTimeout(t)
  }, [toast])

  const closeWelcome = useCallback((startStory: boolean) => {
    setWelcome(false)
    try { localStorage.setItem("tally-welcome-seen", "1") } catch { /* fine */ }
    if (startStory) showMe(STEPS[0].tour)
  }, [showMe])

  const resetDemo = async () => {
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
          <SidebarSamples />
        </nav>
        <div className="mt-auto rounded-xl border border-[#CFE0EC] bg-[#F2F7FB] p-3">
          <div className="text-sm font-bold text-[#16476A]">✓ Connected to PayPal</div>
          <div className="mt-1 text-xs leading-snug text-neutral-700">
            PayPal bills and payments sync automatically. Payments made outside PayPal are noted on the bill.
          </div>
        </div>
      </aside>

      <div className="min-w-0 flex-1">
        {/* ONE bar: the story step + what is this + reset */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#F0D9A0] bg-[#FFF6DF] px-6 py-2.5 text-sm text-[#5C4300]">
          <StoryBar onShowMe={showMe} />
          <div className="flex items-center gap-2">
            <button onClick={() => setWelcome(true)} className="min-h-10 rounded-lg px-3 font-semibold hover:bg-[#F7E7BD]">
              What is this?
            </button>
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

        <Outlet />
      </div>

      {toast && (
        <div role="status" className="fixed right-4 bottom-4 z-40 max-w-sm rounded-xl bg-[#1E4D2E] px-4 py-3 text-sm text-white shadow-lg">
          {toast}
        </div>
      )}
      {welcome && <WelcomeCard onClose={closeWelcome} />}
    </div>
  )
}