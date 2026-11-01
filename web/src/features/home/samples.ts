import { postFile } from "@/lib/api"
import type { Receipt } from "@/features/receipts/types"

export type Sample = { key: string; label: string; hint: string; file: string; twice?: boolean }

// Each sample shows one rule of the Payment Detective. Short words: a glance, not a read.
export const SAMPLES: Sample[] = [
  { key: "exact", label: "$170 Zelle",        hint: "No memo",            file: "zelle-r-kumar-170.png" },
  { key: "close", label: "$88.20 transfer",   hint: "$1.80 bank fee",     file: "transfer-lucia-lopez-88-20.png" },
  { key: "over",  label: "$300 check",        hint: "$30 extra",          file: "check-ravi-kumar-300.png" },
  { key: "dup",   label: "$150 Zelle, twice", hint: "Duplicate caught",   file: "zelle-r-kumar-150.png", twice: true },
]

// Load the demo image and send it through the normal upload, exactly like "Upload a receipt".
export async function uploadSample(file: string): Promise<string> {
  const img = await fetch(`/samples/${file}`)
  if (!img.ok) throw new Error(`Sample image ${file} is missing from web/public/samples`)
  const r = await postFile<Receipt>("/api/receipts", new File([await img.blob()], file, { type: "image/png" }))
  if (!r) throw new Error("Upload didn't return a receipt")
  return r.id
}

// Runs one sample and returns the receipt id to open (for "twice", the second copy: the duplicate).
export async function runSample(s: Sample): Promise<string> {
  const id = await uploadSample(s.file)
  return s.twice ? uploadSample(s.file) : id
}