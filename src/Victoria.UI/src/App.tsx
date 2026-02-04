import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { LoginPage } from './features/auth/LoginPage';
import { InventoryDashboard } from './features/inventory/InventoryDashboard';
import { ProtectedRoute } from './components/ProtectedRoute';
import { CssBaseline, ThemeProvider, createTheme, Box, AppBar, Toolbar, Typography, Button } from '@mui/material';
import { useAuth } from './context/AuthContext';

const theme = createTheme({
  palette: {
    primary: { main: '#1976d2' },
    secondary: { main: '#dc004e' },
    background: { default: '#f4f6f8' },
  },
});

const Layout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { user, logout, tenant } = useAuth();

  if (!user) return <>{children}</>;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component="div" sx={{ flexGrow: 1, fontWeight: 'bold' }}>
            Victoria WMS - Torre de Control
          </Typography>
          <Typography variant="body2" sx={{ mr: 2 }}>
            {user.id} ({user.role}) @ {tenant}
          </Typography>
          <Button color="inherit" onClick={logout}>Salir</Button>
        </Toolbar>
      </AppBar>
      <Box component="main" sx={{ flexGrow: 1 }}>
        {children}
      </Box>
    </Box>
  );
};

function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AuthProvider>
        <Router>
          <Layout>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route
                path="/"
                element={
                  <ProtectedRoute>
                    <InventoryDashboard />
                  </ProtectedRoute>
                }
              />
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Layout>
        </Router>
      </AuthProvider>
    </ThemeProvider>
  );
}

export default App;
