import { NavLink, Outlet } from "react-router"

const link = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center rounded-lg px-3 text-[15px] ${
    isActive ? "bg-[#EEF3F7] font-semibold text-[#16476A]" : "text-neutral-700 hover:bg-neutral-100"
  }`

export function AppShell() {
  return (
    <div className="flex min-h-svh flex-wrap bg-[#F6F5F1] text-neutral-900">
      <aside className="flex w-full flex-col gap-5 border-r border-neutral-200 bg-white p-4 md:min-h-svh md:w-56">
        <div className="flex items-center gap-2.5 px-1.5">
          <div className="flex size-8 items-center justify-center rounded-lg bg-[#1F5F8B] font-bold text-white">T</div>
          <div>
            <div className="text-lg font-bold leading-tight">Tally</div>
            <div className="text-xs text-neutral-500">Melody Music Academy</div>
          </div>
        </div>
        <nav className="flex flex-col gap-0.5">
          <NavLink to="/" end className={link}>Home</NavLink>
          <NavLink to="/payments" className={link}>Payments</NavLink>
        </nav>
        <div className="mt-auto rounded-xl border border-[#CFE0EC] bg-[#F2F7FB] p-3">
          <div className="text-sm font-bold text-[#16476A]">✓ Connected to PayPal</div>
          <div className="mt-1 text-xs leading-snug text-neutral-700">
            PayPal bills and payments sync automatically. Payments made outside PayPal are noted on the bill.
          </div>
        </div>
      </aside>
      <div className="min-w-0 flex-1">
        <Outlet />
      </div>
    </div>
  )
}