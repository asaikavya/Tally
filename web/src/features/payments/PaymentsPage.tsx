import { useEffect, useMemo, useRef, useState } from "react"
import { AgGridReact } from "ag-grid-react"
import type { ColDef } from "ag-grid-community"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import { q } from "@/lib/demo"
import { StatusPill } from "./StatusPill"
import type { PaymentRow } from "./types"
import { FamilyCell } from "./FamilyCell"

const filters = ["All", "Paid", "Partial", "Late", "Due"] as const
type Filter = (typeof filters)[number]

export function PaymentsPage() {
  const grid = useRef<AgGridReact<PaymentRow>>(null)
  const [rows, setRows] = useState<PaymentRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState("")
  const [filter, setFilter] = useState<Filter>("All")

  useEffect(() => {
    getJson<PaymentRow[]>(`/api/payments?${q}`).then(setRows).catch((e: Error) => setError(e.message))
  }, [])

  // Columns are defined once (useMemo), not rebuilt on every redraw.
  const columns = useMemo<ColDef<PaymentRow>[]>(() => [
    { field: "family", rowGroup: true, hide: true },
    {  field: "student", headerName: "Student", minWidth: 140 },
    { field: "offering", headerName: "Class", minWidth: 100 },
    { field: "plan", headerName: "Plan", maxWidth: 110 },
    { field: "amountDue", headerName: "Amount", aggFunc: "sum", type: "rightAligned", maxWidth: 120, valueFormatter: (p) => money(p.value ?? 0)  },
    { field: "remaining", headerName: "Still due", aggFunc: "sum", type: "rightAligned", maxWidth: 120, valueFormatter: (p) => money(p.value ?? 0) },
    {  field: "dueDate", headerName: "Due", maxWidth: 110,
  valueFormatter: (p) => p.value ? new Date(p.value + "T00:00").toLocaleDateString("en-US", { month: "short", day: "numeric" }) : ""},
    {  field: "status", headerName: "Status", cellRenderer: StatusPill, minWidth: 120 },
    { field: "paidVia", headerName: "Paid via", minWidth: 110 },
  ], [])

  const shown = useMemo(
    () => (rows ?? []).filter((r) => filter === "All" || r.status === filter),
    [rows, filter],
  )

  const count = (f: Filter) => (f === "All" ? rows?.length ?? 0 : rows?.filter((r) => r.status === f).length ?? 0)

  return (
    <main className="mx-auto flex max-w-7xl flex-col gap-5 p-8">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <h1 className="text-3xl font-extrabold tracking-tight">November payments</h1>
        <Button variant="outline" onClick={() => grid.current?.api.exportDataAsCsv({ fileName: "tally-november.csv" })}>
          Export CSV
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <input
          type="search"
          aria-label="Search"
          placeholder="Search family, student or class"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="min-h-11 min-w-0 flex-[1_1_260px] rounded-lg border border-neutral-300 bg-white px-3"
        />
        {filters.map((f) => (
          <Button key={f} variant={filter === f ? "default" : "outline"} aria-pressed={filter === f} onClick={() => setFilter(f)}>
            {f} · {count(f)}
          </Button>
        ))}
      </div>

      {error && <p className="text-red-700">Couldn't load payments: {error}</p>}
      {!rows && !error && <p className="text-neutral-500">Loading…</p>}

      {rows && (
        <div className="rounded-2xl border border-neutral-200 bg-white p-2">
          <AgGridReact<PaymentRow>
            ref={grid}
            rowData={shown}
            columnDefs={columns}
           defaultColDef={{ sortable: true, filter: true, resizable: true, flex: 1, minWidth: 90, suppressHeaderMenuButton: true }}
           autoGroupColumnDef={{
                      headerName: "Family",
                      minWidth: 290,
                      cellRendererParams: { innerRenderer: FamilyCell, suppressCount: true },
                    }}
            groupDefaultExpanded={1}
            quickFilterText={search}
            domLayout="autoHeight"
            getRowId={(p) => p.data.chargeId}
            suppressAggFuncInHeader
          />
        </div>
      )}
    </main>
  )
}