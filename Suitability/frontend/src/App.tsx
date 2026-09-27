import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { FormeProvider } from './context/FormeContext'
import { AssistantPage } from './pages/AssistantPage'
import { BagPage } from './pages/BagPage'
import { BrowsePage } from './pages/BrowsePage'
import { ComparePage } from './pages/ComparePage'
import { EventPage } from './pages/EventPage'
import { HomePage } from './pages/HomePage'
import { TryOnPage } from './pages/TryOnPage'
import './app-layout.css'

function App() {
  return (
    <FormeProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="event" element={<EventPage />} />
          <Route path="browse" element={<BrowsePage />} />
          <Route path="try-on" element={<TryOnPage />} />
          <Route path="compare" element={<ComparePage />} />
          <Route path="assistant" element={<AssistantPage />} />
          <Route path="bag" element={<BagPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </FormeProvider>
  )
}

export default App
