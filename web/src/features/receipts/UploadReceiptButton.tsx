import { useRef, useState } from "react"
import { useNavigate } from "react-router"
import { Button } from "@/components/ui/button"
import { postFile } from "@/lib/api"
import type { Receipt } from "./types"


export function UploadReceiptButton() {
  const input = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const upload = async (file: File | undefined) => {
    if (!file) return
    setBusy(true)
    setError(null)
    try {
      const r = await postFile<Receipt>("/api/receipts", file)
      if (r) navigate(`/receipts/${r.id}`)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setBusy(false)
      if (input.current) input.current.value = ""
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <input ref={input} type="file" accept="image/png,image/jpeg,image/webp" className="hidden"
             onChange={(e) => upload(e.target.files?.[0])} />
      <Button className="min-h-11" disabled={busy} onClick={() => input.current?.click()}>
        {busy ? "Reading receipt…" : "Upload a receipt"}
      </Button>
      {error && <span className="text-sm text-red-700">{error}</span>}
    </div>
  )
}