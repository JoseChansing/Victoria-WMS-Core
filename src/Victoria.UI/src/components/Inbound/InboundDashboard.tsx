// src/Victoria.UI/src/components/Inbound/InboundDashboard.tsx
import React from 'react';
import {
    Package,
    Truck,
    AlertCircle,
    CheckCircle2,
    Info,
    Camera,
    ChevronRight,
    TrendingUp
} from 'lucide-react';
import type { ImageSource } from '../../types/inbound';
import { useInbound } from '../../hooks/useInbound';

import { useAuth } from '../../context/AuthContext';

const getImageIcon = (source: ImageSource | null) => {
    switch (source) {
        case 'variant':
        case 'master':
            return <CheckCircle2 className="w-5 h-5 text-emerald-500" />;
        case 'brand':
            return <Info className="w-5 h-5 text-blue-500" />;
        default:
            return <Camera className="w-5 h-5 text-rose-500" />;
    }
};

const InboundDashboard: React.FC = () => {
    const { tenant } = useAuth();
    const { kpis, orders, isLoading } = useInbound(tenant);

    if (isLoading) {
        return (
            <div className="p-6 space-y-8 bg-slate-50 min-h-screen flex items-center justify-center">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
            </div>
        );
    }

    return (
        <div className="p-6 space-y-8 bg-slate-50 min-h-screen">
            {/* KPI Section */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 flex items-center space-x-4">
                    <div className="p-3 bg-blue-50 rounded-xl">
                        <Truck className="w-8 h-8 text-blue-600" />
                    </div>
                    <div>
                        <p className="text-sm font-medium text-slate-500 uppercase tracking-wider">Órdenes Pendientes</p>
                        <p className="text-2xl font-bold text-slate-900">{kpis?.pendingOrders ?? 0}</p>
                    </div>
                </div>

                <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 flex items-center space-x-4">
                    <div className="p-3 bg-emerald-50 rounded-xl">
                        <Package className="w-8 h-8 text-emerald-600" />
                    </div>
                    <div>
                        <p className="text-sm font-medium text-slate-500 uppercase tracking-wider">Unidades por Recibir</p>
                        <p className="text-2xl font-bold text-slate-900">{kpis?.unitsToReceive.toLocaleString() ?? 0}</p>
                    </div>
                </div>

                <div className="bg-white p-6 rounded-2xl shadow-sm border border-slate-100 flex items-center space-x-4">
                    <div className="p-3 bg-rose-50 rounded-xl">
                        <AlertCircle className="w-8 h-8 text-rose-600" />
                    </div>
                    <div>
                        <p className="text-sm font-medium text-slate-500 uppercase tracking-wider">Prioridad Alta</p>
                        <p className="text-2xl font-bold text-slate-900">{kpis?.highPriorityCount ?? 0}</p>
                    </div>
                </div>
            </div>

            {/* Main Table */}
            <div className="bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
                <div className="p-6 border-b border-slate-100 flex justify-between items-center">
                    <h2 className="text-xl font-semibold text-slate-900 flex items-center space-x-2">
                        <TrendingUp className="w-5 h-5 text-blue-500" />
                        <span>Monitoreo de Arribos</span>
                    </h2>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-slate-50/50">
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">PO #</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">Proveedor</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">Fecha</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">Estado</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase text-center">Imágenes</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">Progreso</th>
                                <th className="p-4 text-xs font-semibold text-slate-500 uppercase">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                            {orders.map((po) => (
                                <tr key={po.id} className="hover:bg-slate-50/80 transition-colors">
                                    <td className="p-4 font-mono font-medium text-blue-600">{po.poNumber}</td>
                                    <td className="p-4 text-sm text-slate-700">{po.supplier}</td>
                                    <td className="p-4 text-sm text-slate-600 font-medium">{po.date}</td>
                                    <td className="p-4">
                                        <span className={`px-3 py-1 rounded-full text-xs font-bold uppercase tracking-tight ${po.status === 'Pending' ? 'bg-amber-100 text-amber-700' : 'bg-blue-100 text-blue-700'
                                            }`}>
                                            {po.status}
                                        </span>
                                    </td>
                                    <td className="p-4">
                                        <div className="flex justify-center space-x-1">
                                            {getImageIcon(po.lines[0]?.imageSource || null)}
                                        </div>
                                    </td>
                                    <td className="p-4">
                                        <div className="w-full bg-slate-100 rounded-full h-2 max-w-[120px]">
                                            <div className="bg-blue-500 h-2 rounded-full" style={{ width: `${(po.lines.reduce((acc, l) => acc + l.receivedQty, 0) / po.totalUnits) * 100}%` }}></div>
                                        </div>
                                    </td>
                                    <td className="p-4">
                                        <button className="flex items-center space-x-1 px-4 py-2 bg-slate-900 text-white rounded-lg text-sm font-medium hover:bg-slate-800 transition-all shadow-sm active:scale-95 group">
                                            <span>Iniciar</span>
                                            <ChevronRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};

export default InboundDashboard;
