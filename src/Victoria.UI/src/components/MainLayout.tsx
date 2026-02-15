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
    MapPin,
    Box
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import NotificationBell from './Layout/NotificationBell';
import { Toaster } from 'sonner';

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
            ? 'bg-corp-accent text-white shadow-lg shadow-black/20'
            : 'text-slate-400 hover:bg-corp-accent/50 hover:text-white'
            }`}
    >
        <span className={`${active ? 'text-white' : 'text-slate-500 group-hover:text-white'} transition-colors`}>
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
        { icon: <LayoutDashboard className="w-5 h-5" />, label: 'Tareas de Inventario', path: '/inventory' },
        { icon: <Truck className="w-5 h-5 text-blue-400" />, label: '📡 Inbound RFID', path: '/inbound?mode=rfid' },
        { icon: <Truck className="w-5 h-5 text-emerald-400" />, label: '🖨️ Inbound Standard', path: '/inbound?mode=standard' },
        { icon: <Package className="w-5 h-5" />, label: 'LPN Master', path: '/inventory-master' },
        { icon: <Box className="w-5 h-5 text-blue-400" />, label: 'Inventory by Item', path: '/inventory-item' },
        { icon: <MapPin className="w-5 h-5 text-emerald-400" />, label: 'Inventory by Location', path: '/inventory-location' },
        { icon: <Package className="w-5 h-5" />, label: 'SKU Master', path: '/skus' },
        { icon: <MapPin className="w-5 h-5" />, label: 'Location Master', path: '/locations' },
        { icon: <Truck className="w-5 h-5 text-orange-500" />, label: '📤 Outbound / Dispatch', path: '/outbound' },
    ];

    if (!user) return <>{children}</>;

    return (
        <div className="flex min-h-screen bg-corp-base font-sans text-white">
            {/* Sidebar */}
            <aside className="w-72 bg-corp-nav border-r border-corp-secondary flex flex-col p-6 fixed h-full z-20 shadow-xl">
                <div className="flex items-center space-x-3 mb-10 px-2">
                    <div className="w-10 h-10 bg-corp-accent rounded-xl flex items-center justify-center shadow-lg shadow-black/20">
                        <Package className="text-white w-6 h-6" />
                    </div>
                    <div>
                        <h1 className="text-xl font-bold tracking-tight text-white">Victoria WMS</h1>
                        <p className="text-[10px] uppercase tracking-widest text-slate-400 font-bold">Operations Command Center</p>
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

                <div className="mt-auto pt-6 border-t border-corp-secondary space-y-4">
                    <div className="bg-corp-accent/30 rounded-2xl p-4 flex items-center space-x-3">
                        <div className="w-10 h-10 bg-corp-nav rounded-full border border-corp-secondary flex items-center justify-center overflow-hidden">
                            <UserCircle className="w-8 h-8 text-slate-400" />
                        </div>
                        <div className="flex-1 overflow-hidden">
                            <p className="text-xs font-bold truncate text-white">{user.id}</p>
                            <p className="text-[10px] text-slate-400 font-medium uppercase">{tenant}</p>
                        </div>
                    </div>

                    <button
                        onClick={logout}
                        className="w-full flex items-center space-x-3 px-4 py-3 rounded-xl text-rose-400 hover:bg-rose-500/10 transition-all font-semibold text-sm"
                    >
                        <LogOut className="w-5 h-5" />
                        <span>Logout</span>
                    </button>
                </div>
            </aside>

            {/* Main Content */}
            <main className="flex-1 ml-72">
                {/* Header */}
                <header className="h-20 bg-corp-nav/90 backdrop-blur-md border-b border-corp-secondary px-8 flex items-center justify-between sticky top-0 z-10">
                    <div className="flex items-center space-x-2">
                        <span className="text-slate-400 text-sm font-medium">Pages</span>
                        <ChevronRight className="w-4 h-4 text-slate-500" />
                        <span className="text-white text-sm font-semibold capitalize tracking-wide">
                            {location.pathname.replace('/', '') || 'Dashboard'}
                        </span>
                    </div>

                    <div className="flex items-center space-x-4">
                        <NotificationBell />
                        <div className="h-8 w-[1px] bg-corp-secondary mx-2"></div>
                        <div className="flex items-center space-x-3">
                            <span className="text-xs font-bold text-slate-300">{user.role}</span>
                            <div className="w-8 h-8 bg-corp-accent rounded-lg flex items-center justify-center border border-corp-secondary">
                                <span className="text-[10px] text-white font-black">{user.id.substring(0, 2).toUpperCase()}</span>
                            </div>
                        </div>
                    </div>
                </header>

                <div className="p-8 bg-corp-base">
                    {children}
                </div>
            </main>
            <Toaster position="top-right" theme="dark" richColors />
        </div>
    );
};

export default MainLayout;
