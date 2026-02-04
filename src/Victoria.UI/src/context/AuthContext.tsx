import React, { createContext, useContext, useState, useEffect } from 'react';
import { jwtDecode } from 'jwt-decode';

interface User {
    id: string;
    tenantId: string;
    role: 'Operator' | 'Supervisor' | 'Admin';
}

interface AuthContextType {
    user: User | null;
    tenant: string | null;
    login: (token: string, tenant: string) => void;
    logout: () => void;
    setTenant: (tenant: string) => void;
    isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [user, setUser] = useState<User | null>(null);
    const [tenant, setTenantState] = useState<string | null>(localStorage.getItem('vicky_tenant'));

    useEffect(() => {
        const token = localStorage.getItem('vicky_token');
        if (token) {
            try {
                const decoded: any = jwtDecode(token);
                setUser({
                    id: decoded.sub || 'unknown',
                    tenantId: decoded.tenant || tenant || '',
                    role: decoded.role || 'Operator'
                });
            } catch (e) {
                logout();
            }
        }
    }, []);

    const login = (token: string, selectedTenant: string) => {
        localStorage.setItem('vicky_token', token);
        localStorage.setItem('vicky_tenant', selectedTenant);
        setTenantState(selectedTenant);
        // Simulación de deco
        setUser({ id: 'SUPER-01', tenantId: selectedTenant, role: 'Supervisor' });
    };

    const logout = () => {
        localStorage.removeItem('vicky_token');
        localStorage.removeItem('vicky_tenant');
        setUser(null);
        setTenantState(null);
    };

    const setTenant = (newTenant: string) => {
        localStorage.setItem('vicky_tenant', newTenant);
        setTenantState(newTenant);
    };

    return (
        <AuthContext.Provider value={{ user, tenant, login, logout, setTenant, isAuthenticated: !!user }}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) throw new Error('useAuth must be used within an AuthProvider');
    return context;
};
