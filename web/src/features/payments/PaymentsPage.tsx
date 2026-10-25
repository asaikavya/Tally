import { useEffect, useMemo, useRef, useState } from "react"
import { AgGridReact } from "ag-grid-react"
import type { ColDef, ExcelStyle } from "ag-grid-community"
import { Button } from "@/components/ui/button"
import { getJson } from "@/lib/api"
import { money } from "@/lib/money"
import { q } from "@/lib/demo"
import { StatusPill } from "./StatusPill"
import { FamilyCell } from "./FamilyCell"
import { ActionCell } from "./ActionCell"
import { familyStatus, distinctJoin } from "./familyAgg"
import { tallyGridTheme } from "./gridTheme"
import type { PaymentRow } from "./types"

const filters = ["All", "Paid", "Partial", "Says paid", "Late", "Due"] as const
type Filter = (typeof filters)[number]


// Excel gets real currency cells, not text.
const excelStyles: ExcelStyle[] = [{ id: "currency", numberFormat: { format: "$#,##0.00" } }]



export function PaymentsPage() {
  const grid = useRef<AgGridReact<PaymentRow>>(null)
  const [rows, setRows] = useState<PaymentRow[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState("")
  const [filter, setFilter] = useState<Filter>("All")

  useEffect(() => {
    getJson<PaymentRow[]>(`/api/payments?${q}`).then(setRows).catch((e: Error) => setError(e.message))
  }, [])

  const columns = useMemo<ColDef<PaymentRow>[]>(() => [
    { field: "family", rowGroup: true, hide: true },
    { field: "student", headerName: "Student", minWidth: 140 },
    { field: "offering", headerName: "Class", minWidth: 100 },
    { field: "plan", headerName: "Plan", maxWidth: 110 },
       { field: "amountDue", headerName: "Amount", aggFunc: "sum", type: "rightAligned", minWidth: 120,
      cellClass: ["currency", "ag-right-aligned-cell"], valueFormatter: (p) => money(p.value ?? 0) },
    { field: "remaining", headerName: "Still due", aggFunc: "sum", type: "rightAligned", minWidth: 120,
      cellClass: ["currency", "ag-right-aligned-cell"], valueFormatter: (p) => money(p.value ?? 0) },
    { field: "status", headerName: "Status", aggFunc: "familyStatus", cellRenderer: StatusPill, minWidth: 120 },
    { field: "paidVia", headerName: "Paid via", aggFunc: "distinctJoin", minWidth: 110 },
    { headerName: "", colId: "actions", cellRenderer: ActionCell, minWidth: 140, maxWidth: 150,
      pinned: "right", sortable: false, filter: false },
  ], [])

  const shown = useMemo(
    () => (rows ?? []).filter((r) => filter === "All" || r.status === filter),
    [rows, filter],
  )
  const count = (f: Filter) => (f === "All" ? rows?.length ?? 0 : rows?.filter((r) => r.status === f).length ?? 0)

  const exportExcel = () =>
    grid.current?.api.exportDataAsExcel({
      fileName: "tally-november.xlsx",
      sheetName: "November",
      processRowGroupCallback: (p) => p.node.key ?? "",
    })

  return (
    <main className="mx-auto flex max-w-7xl flex-col gap-5 p-8">
      <div className="flex flex-wrap items-end justify-between gap-3">
      <div>
          <h1 className="text-3xl font-extrabold tracking-tight">November payments</h1>
          <p className="text-sm text-neutral-500">Bills were due Nov 10</p>
        </div>
        <Button variant="outline" onClick={exportExcel}>Export to Excel</Button>
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
        <AgGridReact<PaymentRow>
          ref={grid}
          theme={tallyGridTheme}
          rowData={shown}
          columnDefs={columns}
          defaultColDef={{ sortable: true, filter: true, resizable: true, flex: 1, minWidth: 90, suppressHeaderMenuButton: true }}
          autoGroupColumnDef={{ headerName: "Family", minWidth: 290, cellRendererParams: { innerRenderer: FamilyCell, suppressCount: true } }}
          aggFuncs={{ familyStatus, distinctJoin }}
          suppressAggFuncInHeader
          groupDefaultExpanded={1}
          grandTotalRow="bottom"
          rowHeight={48}
          headerHeight={44}
          quickFilterText={search}
          domLayout="autoHeight"
          excelStyles={excelStyles}
          getRowId={(p) => p.data.chargeId}
        />
      )}
    </main>
  )
}