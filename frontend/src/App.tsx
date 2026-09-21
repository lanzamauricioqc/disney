import { useCallback, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Dashboard } from './features/dashboard/Dashboard'
import { Admin } from './features/admin/Admin'
import { AttractionPriorities } from './features/visit/AttractionPriorities'
import { VisitSetup } from './features/visit/VisitSetup'
import {
  updatePriorityForPark,
  type AttractionPriority,
  type PrioritySelectionsByPark,
} from './features/visit/priorityModel'
import type { VisitDetails } from './features/visit/visitSetupModel'

export default function App() {
  // The visit draft intentionally lives only for this browser session. There is
  // no visit persistence API yet, so refreshing starts a new planning flow.
  const [visitDetails, setVisitDetails] = useState<VisitDetails | null>(null)
  const [selectedParkId, setSelectedParkId] = useState<number>()
  const [prioritySelections, setPrioritySelections] =
    useState<PrioritySelectionsByPark>({})

  const changePriority = useCallback((
    parkId: number,
    attractionId: number,
    priority: AttractionPriority | null,
  ) => {
    setPrioritySelections((current) =>
      updatePriorityForPark(current, parkId, attractionId, priority),
    )
  }, [])

  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route
        path="/visit"
        element={<VisitSetup savedDetails={visitDetails} onContinue={setVisitDetails} />}
      />
      <Route
        path="/visit/priorities"
        element={
          visitDetails ? (
            <AttractionPriorities
              onParkChange={setSelectedParkId}
              onPriorityChange={changePriority}
              priorities={
                selectedParkId === undefined
                  ? {}
                  : prioritySelections[selectedParkId] ?? {}
              }
              selectedParkId={selectedParkId}
              visitDetails={visitDetails}
            />
          ) : (
            <Navigate to="/visit" replace />
          )
        }
      />
      <Route path="/admin" element={<Admin />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}