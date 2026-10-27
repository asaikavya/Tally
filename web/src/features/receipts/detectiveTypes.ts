export type Strength = "Strong" | "Medium" | "Weak"
export type Evidence = { strength: Strength; text: string }
export type OpenCharge = { chargeId: string; payerId: string; student: string; offering: string; remaining: number; dueDate: string }
export type Line = { chargeId: string; label: string; chargeRemaining: number; amount: number }
export type Option = { lines: Line[]; leftover: number; stillDueAfter: number; note: string | null }
export type Combo = { label: string; sum: number; fits: boolean }
export type Coverage = {
  kind: "Exact" | "Close" | "Ambiguous" | "Overpaid" | "Partial" | "TooMany" | "NothingOpen"
  options: Option[]; tried: Combo[]; combinationsChecked: number; evidence: Evidence[]
}
export type Sender = { payerId: string; name: string; confidence: "Certain" | "Strong" | "Possible"; evidence: Evidence[] }
export type Finding = { sender: Sender | null; payerId: string; name: string; openCharges: OpenCharge[]; coverage: Coverage; extra: Evidence[] }
export type DetectiveResult = {
  verdict: "Suggested" | "ChooseFamily" | "ChooseClasses" | "NoMatch"
  headline: string; best: Finding | null; candidates: Finding[]; ruledOut: string[]
}