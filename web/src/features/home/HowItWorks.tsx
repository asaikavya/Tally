import { ArrowRight, Image as ImageIcon, ScanText, FileCheck } from "lucide-react"

// The whole app in one line: what happens to a parent's screenshot.
const PILLS = [
  { icon: ImageIcon, text: "Parent sends a screenshot", start: true },
  { icon: ScanText,  text: "AI reads it · Tally matches", start: false },
  { icon: FileCheck, text: "Noted on the PayPal bill", start: false },
]

export function HowItWorks() {
  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      {PILLS.map((p, i) => (
        <span key={p.text} className="flex items-center gap-2">
          <span className={`flex items-center gap-1.5 rounded-full px-3 py-1 font-medium ${
            p.start ? "bg-[#16476A] text-white" : "border border-neutral-200 bg-white text-neutral-700"}`}>
            <p.icon className="size-4" aria-hidden="true" />
            {p.text}
          </span>
          {i < PILLS.length - 1 && <ArrowRight className="size-4 text-[#16476A]" aria-hidden="true" />}
        </span>
      ))}
    </div>
  )
}