import { useCallback } from "react"
import { useLocation, useNavigate } from "react-router"

// "Show me": go to Home, scroll to the part of the page a step is about, and outline it for 2 seconds.
export function useShowMe() {
  const navigate = useNavigate()
  const { pathname } = useLocation()

  return useCallback((tour: string) => {
    if (pathname !== "/") navigate("/")
    const tryFind = (triesLeft: number) => {
      const el = document.querySelector<HTMLElement>(`[data-tour="${tour}"]`)
      if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "center" })
        el.classList.add("tour-highlight")
        window.setTimeout(() => el.classList.remove("tour-highlight"), 2000)
      } else if (triesLeft > 0) {
        window.setTimeout(() => tryFind(triesLeft - 1), 300)     // Home may still be loading
      }
    }
    tryFind(10)
  }, [navigate, pathname])
}