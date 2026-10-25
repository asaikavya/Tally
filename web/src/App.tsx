import { BrowserRouter, Route, Routes } from "react-router"
import { AppShell } from "@/components/AppShell"
import { HomePage } from "@/features/home/HomePage"

function PaymentsPlaceholder() {
  return <p className="p-8 text-neutral-500">Payments grid comes in Step 5.</p>
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/payments" element={<PaymentsPlaceholder />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}