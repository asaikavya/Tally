import { useEffect } from "react"
import { Button } from "@/components/ui/button"

// First visit only: what Tally is, that this is a sandbox, and where to start.
export function WelcomeCard({ onClose }: { onClose: (startStory: boolean) => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(false) }
    window.addEventListener("keydown", onKey)
    return () => window.removeEventListener("keydown", onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
         role="dialog" aria-modal="true" aria-labelledby="welcome-title">
      <div className="flex w-full max-w-md flex-col gap-4 rounded-2xl bg-white p-6 shadow-xl">
        <h2 id="welcome-title" className="text-2xl font-extrabold tracking-tight">Welcome to Tally</h2>
        <p className="text-[15px] leading-relaxed">
          Tally keeps <strong>every payment on the family's PayPal bill</strong>, including the ones
          paid by Zelle, UPI, cash or check. AI reads the screenshots; Tally works out who paid for what;
          you approve.
        </p>
        <p className="text-sm text-neutral-600">
          This is a sample music academy. The families are made up, and PayPal runs in sandbox mode with test money.
        </p>
        <div className="flex flex-wrap gap-2">
          <Button autoFocus className="min-h-11" onClick={() => onClose(true)}>Start the 5-step story</Button>
          <Button variant="outline" className="min-h-11" onClick={() => onClose(false)}>Look around first</Button>
        </div>
      </div>
    </div>
  )
}