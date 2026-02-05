import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { LoginPage } from './features/auth/LoginPage';
import { InventoryDashboard } from './features/inventory/InventoryDashboard';
import InboundDashboard from './components/Inbound/InboundDashboard';
import MainLayout from './components/MainLayout';
import { ProtectedRoute } from './components/ProtectedRoute';

function App() {
  return (
    <AuthProvider>
      <Router>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <Navigate to="/inbound" replace />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inbound"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <InboundDashboard />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inventory"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <InventoryDashboard />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Router>
    </AuthProvider>
  );
}

export default App;
