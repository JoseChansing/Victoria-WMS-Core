// src/Victoria.UI/src/components/MainLayout.tsx
import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
    LayoutDashboard,
    Truck,
    Package,
    LogOut,
    UserCircle,
    ChevronRight,
    Bell
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';

interface SidebarItemProps {
    icon: React.ReactNode;
    label: string;
    active: boolean;
    onClick: () => void;
}

const SidebarItem: React.FC<SidebarItemProps> = ({ icon, label, active, onClick }) => (
    <button
        onClick={onClick}
        className={`w-full flex items-center space-x-3 px-4 py-3 rounded-xl transition-all duration-200 group ${active
                ? 'bg-blue-600 text-white shadow-lg shadow-blue-200'
                : 'text-slate-500 hover:bg-blue-50 hover:text-blue-600'
            }`}
    >
        <span className={`${active ? 'text-white' : 'text-slate-400 group-hover:text-blue-600'} transition-colors`}>
            {icon}
        </span>
        <span className="font-semibold text-sm">{label}</span>
        {active && <ChevronRight className="w-4 h-4 ml-auto text-white" />}
    </button>
);

const MainLayout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { user, logout, tenant } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();

    const menuItems = [
        { icon: <LayoutDashboard className="w-5 h-5" />, label: 'Dashboard', path: '/inventory' },
        { icon: <Truck className="w-5 h-5" />, label: 'Módulo Inbound', path: '/inbound' },
        { icon: <Package className="w-5 h-5" />, label: 'Maestro SKUs', path: '/skus' },
    ];

    if (!user) return <>{children}</>;

    return (
        <div className="flex min-h-screen bg-slate-50 font-sans text-slate-900">
            {/* Sidebar */}
            <aside className="w-72 bg-white border-r border-slate-200 flex flex-col p-6 fixed h-full z-20">
                <div className="flex items-center space-x-3 mb-10 px-2">
                    <div className="w-10 h-10 bg-blue-600 rounded-xl flex items-center justify-center shadow-lg shadow-blue-100">
                        <Package className="text-white w-6 h-6" />
                    </div>
                    <div>
                        <h1 className="text-xl font-bold tracking-tight">Victoria WMS</h1>
                        <p className="text-[10px] uppercase tracking-widest text-slate-400 font-bold">Torre de Control</p>
                    </div>
                </div>

                <nav className="flex-1 space-y-2">
                    {menuItems.map((item) => (
                        <SidebarItem
                            key={item.path}
                            icon={item.icon}
                            label={item.label}
                            active={location.pathname === item.path}
                            onClick={() => navigate(item.path)}
                        />
                    ))}
                </nav>

                <div className="mt-auto pt-6 border-t border-slate-100 space-y-4">
                    <div className="bg-slate-50 rounded-2xl p-4 flex items-center space-x-3">
                        <div className="w-10 h-10 bg-white rounded-full border border-slate-200 flex items-center justify-center overflow-hidden">
                            <UserCircle className="w-8 h-8 text-slate-300" />
                        </div>
                        <div className="flex-1 overflow-hidden">
                            <p className="text-xs font-bold truncate">{user.id}</p>
                            <p className="text-[10px] text-slate-500 font-medium uppercase">{tenant}</p>
                        </div>
                    </div>

                    <button
                        onClick={logout}
                        className="w-full flex items-center space-x-3 px-4 py-3 rounded-xl text-rose-500 hover:bg-rose-50 transition-all font-semibold text-sm"
                    >
                        <LogOut className="w-5 h-5" />
                        <span>Cerrar Sesión</span>
                    </button>
                </div>
            </aside>

            {/* Main Content */}
            <main className="flex-1 ml-72">
                {/* Header */}
                <header className="h-20 bg-white/80 backdrop-blur-md border-b border-slate-200 px-8 flex items-center justify-between sticky top-0 z-10">
                    <div className="flex items-center space-x-2">
                        <span className="text-slate-400 text-sm font-medium">Pages</span>
                        <ChevronRight className="w-4 h-4 text-slate-300" />
                        <span className="text-slate-900 text-sm font-semibold capitalize">
                            {location.pathname.replace('/', '') || 'Dashboard'}
                        </span>
                    </div>

                    <div className="flex items-center space-x-4">
                        <button className="p-2 text-slate-400 hover:text-blue-600 transition-colors relative">
                            <Bell className="w-5 h-5" />
                            <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-rose-500 border-2 border-white rounded-full"></span>
                        </button>
                        <div className="h-8 w-[1px] bg-slate-200 mx-2"></div>
                        <div className="flex items-center space-x-3">
                            <span className="text-xs font-bold text-slate-700">{user.role}</span>
                            <div className="w-8 h-8 bg-slate-900 rounded-lg flex items-center justify-center">
                                <span className="text-[10px] text-white font-black">{user.id.substring(0, 2).toUpperCase()}</span>
                            </div>
                        </div>
                    </div>
                </header>

                <div className="p-8">
                    {children}
                </div>
            </main>
        </div>
    );
};

export default MainLayout;
