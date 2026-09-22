import { useCallback, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Dashboard } from './features/dashboard/Dashboard'
import { Admin } from './features/admin/Admin'
import { AttractionPriorities } from './features/visit/AttractionPriorities'
import { ItineraryResults } from './features/visit/ItineraryResults'
import { VisitSetup } from './features/visit/VisitSetup'
import { ActiveVisit } from './features/visit/ActiveVisit'
import {
  updatePriorityForPark,
  type AttractionPriority,
  type PrioritySelectionsByPark,
} from './features/visit/priorityModel'
import type { VisitDetails } from './features/visit/visitSetupModel'
import type { GeneratedItinerary } from './features/visit/itineraryModel'

export default function App() {
  // The visit draft intentionally lives only for this browser session. There is
  // no visit persistence API yet, so refreshing starts a new planning flow.
  const [visitDetails, setVisitDetails] = useState<VisitDetails | null>(null)
  const [selectedParkId, setSelectedParkId] = useState<number>()
  const [prioritySelections, setPrioritySelections] =
    useState<PrioritySelectionsByPark>({})
  const [generatedItinerary, setGeneratedItinerary] =
    useState<GeneratedItinerary | null>(null)

  const changePriority = useCallback((
    parkId: number,
    attractionId: number,
    priority: AttractionPriority | null,
  ) => {
    setPrioritySelections((current) =>
      updatePriorityForPark(current, parkId, attractionId, priority),
    )
    setGeneratedItinerary(null)
  }, [])

  const saveVisitDetails = useCallback((details: VisitDetails) => {
    setVisitDetails(details)
    setGeneratedItinerary(null)
  }, [])

  const changePark = useCallback((parkId: number) => {
    setSelectedParkId(parkId)
    setGeneratedItinerary(null)
  }, [])

  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route
        path="/visit"
        element={<VisitSetup savedDetails={visitDetails} onContinue={saveVisitDetails} />}
      />
      <Route
        path="/visit/priorities"
        element={
          visitDetails ? (
            <AttractionPriorities
              onParkChange={changePark}
              onGenerated={setGeneratedItinerary}
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
      <Route
        path="/visit/itinerary"
        element={
          generatedItinerary ? (
            <ItineraryResults plan={generatedItinerary} />
          ) : (
            <Navigate to={visitDetails ? '/visit/priorities' : '/visit'} replace />
          )
        }
      />
      <Route path="/visit/session" element={<ActiveVisit />} />
      <Route path="/admin" element={<Admin />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}