import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { LoginPage } from './features/auth/LoginPage';
import { InventoryDashboard } from './features/inventory/InventoryDashboard';
import { LpnMaster } from './features/inventory/LpnMaster';
import { InventoryByItem } from './features/inventory/InventoryByItem';
import { InventoryByLocation } from './features/inventory/InventoryByLocation';
import { SkuMaster } from './features/inventory/SkuMaster';
import { LocationMaster } from './features/inventory/LocationMaster';
import InboundDashboard from './components/Inbound/InboundDashboard';
import ReceiveStation from './components/Inbound/ReceiveStation';
import MainLayout from './components/MainLayout';
import OutboundDashboard from './features/outbound/OutboundDashboard';
import { ProtectedRoute } from './components/ProtectedRoute';

import { Toaster } from 'sonner';

function App() {
  return (
    <AuthProvider>
      <Toaster position="top-right" expand={true} richColors />
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
            path="/inbound/receive/rfid/:workingMode/:orderId"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <ReceiveStation mode="rfid" />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/outbound"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <OutboundDashboard />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inbound/receive/:workingMode/:orderId"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <ReceiveStation mode="standard" />
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
          <Route
            path="/skus"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <SkuMaster />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/locations"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <LocationMaster />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inventory-item"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <InventoryByItem />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inventory-master"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <LpnMaster />
                </MainLayout>
              </ProtectedRoute>
            }
          />
          <Route
            path="/inventory-location"
            element={
              <ProtectedRoute>
                <MainLayout>
                  <InventoryByLocation />
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
