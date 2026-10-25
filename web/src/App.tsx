import { BrowserRouter, Route, Routes } from "react-router"
import { AppShell } from "@/components/AppShell"
import { HomePage } from "@/features/home/HomePage"
import { PaymentsPage } from "@/features/payments/PaymentsPage"

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/payments" element={<PaymentsPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}