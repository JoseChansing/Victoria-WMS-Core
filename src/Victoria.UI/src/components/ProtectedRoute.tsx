import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

interface ProtectedRouteProps {
    children: React.ReactNode;
    requiredRole?: 'Operator' | 'Supervisor' | 'Admin';
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, requiredRole }) => {
    const { isAuthenticated, user } = useAuth();

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    if (requiredRole && user?.role !== requiredRole && user?.role !== 'Admin') {
        return (
            <div style={{ padding: '2rem', textAlign: 'center' }}>
                <h1>Acceso Denegado</h1>
                <p>Se requiere rol de {requiredRole} para acceder a esta sección.</p>
                <button onClick={() => window.history.back()}>Volver</button>
            </div>
        );
    }

    return <>{children}</>;
};
