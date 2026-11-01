import { useCallback, useEffect, useRef, useState } from "react"
import { useNavigate } from "react-router"
import { Button } from "@/components/ui/button"
import { postFile } from "@/lib/api"
import type { Receipt } from "./types"

const MAX_FILES = 20
const IMAGE_TYPES = ["image/png", "image/jpeg", "image/webp"]

// One or many screenshots, picked or pasted (Ctrl+V). Read one at a time (kind to the AI rate limit), then:
// one file → its receipt page; several → Home, where sure matches wait pre-ticked.
export function UploadReceiptButton() {
  const input = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const [progress, setProgress] = useState<{ done: number; total: number } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const busy = progress !== null

  const upload = useCallback(async (picked: File[]) => {
    const files = picked.filter((f) => IMAGE_TYPES.includes(f.type)).slice(0, MAX_FILES)
    if (files.length === 0) { setError("Only PNG, JPG or WebP images."); return }
    setError(null)
    setProgress({ done: 0, total: files.length })

    let lastId: string | null = null
    const failed: string[] = []
    for (const [i, file] of files.entries()) {
      try {
        const r = await postFile<Receipt>("/api/receipts", file)
        if (r) lastId = r.id
      } catch {
        failed.push(file.name || `image ${i + 1}`)        // keep going: one bad file shouldn't stop the rest
      }
      setProgress({ done: i + 1, total: files.length })
    }

    setProgress(null)
    if (input.current) input.current.value = ""
    if (failed.length > 0) setError(`Couldn't read: ${failed.join(", ")}`)

    if (files.length === 1 && lastId) navigate(`/receipts/${lastId}`)
    else if (lastId) { navigate("/"); window.dispatchEvent(new Event("tally:refresh")) }
  }, [navigate])

  // Ctrl+V anywhere on the page: a copied screenshot goes straight in (but never while typing in a box).
  useEffect(() => {
    const onPaste = (e: ClipboardEvent) => {
      const t = e.target as HTMLElement | null
      if (busy || t?.closest("input, textarea, [contenteditable]")) return
      const files = Array.from(e.clipboardData?.files ?? [])
      if (files.length === 0) return
      e.preventDefault()
      upload(files)
    }
    window.addEventListener("paste", onPaste)
    return () => window.removeEventListener("paste", onPaste)
  }, [busy, upload])

  return (
    <div className="flex flex-col items-end gap-1">
      <input ref={input} type="file" multiple accept="image/png,image/jpeg,image/webp" className="hidden"
             onChange={(e) => upload(Array.from(e.target.files ?? []))} />
      <Button className="min-h-11" disabled={busy} onClick={() => input.current?.click()}>
        {busy ? `Reading ${progress.done + 1} of ${progress.total}…` : "Upload receipts"}
      </Button>
      <span className="text-xs text-neutral-500">{busy ? "One at a time, so nothing is missed" : "or paste a screenshot (Ctrl+V)"}</span>
      {error && <span className="text-sm text-red-700">{error}</span>}
    </div>
  )
}