import { useEffect, useRef, useState } from "react"
import { KeyRound } from "lucide-react"

// PayPal SANDBOX test buyer for judges: fake money, works only on sandbox.paypal.com.
const BUYER_EMAIL = "sb-agnwz53636700@personal.example.com"   // ← the judge account you create
const BUYER_PASSWORD = "TallyJudge2026!"             // ← its sandbox password

export function TestBuyerLogin() {
  const [open, setOpen] = useState(false)
  const box = useRef<HTMLDivElement>(null)

  // Close when clicking anywhere outside the popup, or on Esc.
  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => { if (box.current && !box.current.contains(e.target as Node)) setOpen(false) }
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false) }
    document.addEventListener("mousedown", onDown)
    document.addEventListener("keydown", onKey)
    return () => {
      document.removeEventListener("mousedown", onDown)
      document.removeEventListener("keydown", onKey)
    }
  }, [open])

  return (
    <div ref={box} className="relative hidden sm:block">
      <button onClick={() => setOpen((o) => !o)} aria-expanded={open}
        className="flex min-h-10 items-center gap-1.5 rounded-lg px-3 font-semibold hover:bg-[#F7E7BD]">
        <KeyRound className="size-4" aria-hidden="true" /> Test buyer login
      </button>
      {open && (
        <div className="absolute right-0 top-11 z-50 w-72 rounded-xl border border-neutral-200 bg-white p-3 text-xs text-neutral-700 shadow-xl">
          <div className="mb-1 font-semibold text-neutral-900">PayPal Sandbox test buyer</div>
          <div className="mb-2 text-neutral-500">Use it to pay any family's PayPal bill. Fake money, no real payments.</div>
          <div>Email: <span className="select-all font-mono">{BUYER_EMAIL}</span></div>
          <div>Password: <span className="select-all font-mono">{BUYER_PASSWORD}</span></div>
        </div>
      )}
    </div>
  )
}