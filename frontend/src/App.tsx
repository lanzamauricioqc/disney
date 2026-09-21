import { useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Dashboard } from './features/dashboard/Dashboard'
import { Admin } from './features/admin/Admin'
import { VisitSetup } from './features/visit/VisitSetup'
import type { VisitDetails } from './features/visit/visitSetupModel'

export default function App() {
  // Visit data is intentionally kept in app memory for the planning flow.
  // There is no visit persistence API yet, so a browser refresh starts a new draft.
  const [visitDetails, setVisitDetails] = useState<VisitDetails | null>(null)

  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route
        path="/visit"
        element={<VisitSetup savedDetails={visitDetails} onContinue={setVisitDetails} />}
      />
      <Route path="/admin" element={<Admin />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
