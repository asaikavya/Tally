import { BrowserRouter, Route, Routes } from "react-router"
import { AppShell } from "@/components/AppShell"
import { HomePage } from "@/features/home/HomePage"
import { PaymentsPage } from "@/features/payments/PaymentsPage"
import { PayPage } from "@/features/pay/PayPage"

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/payments" element={<PaymentsPage />} />
        </Route>
         <Route path="/pay/:token" element={<PayPage />} />
      </Routes>
    </BrowserRouter>
  )
}