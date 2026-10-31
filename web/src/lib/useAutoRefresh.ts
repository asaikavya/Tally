import { useEffect, useRef } from "react"

// Re-check every `ms` while this tab is visible, and right away when the owner comes back to the tab.
// Pauses while the tab is hidden, so a forgotten tab doesn't keep the free database awake.
export function useAutoRefresh(refresh: () => void, ms = 20000) {
  const latest = useRef(refresh)
  useEffect(() => { latest.current = refresh }, [refresh])

  useEffect(() => {
    const run = () => { if (document.visibilityState === "visible") latest.current() }
    const timer = window.setInterval(run, ms)
    document.addEventListener("visibilitychange", run)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener("visibilitychange", run)
    }
  }, [ms])
}