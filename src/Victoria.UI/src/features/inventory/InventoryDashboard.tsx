// src/Victoria.UI/src/features/inventory/InventoryDashboard.tsx
import React from 'react';
import {
    AlertCircle,
    CheckCircle2,
    ShieldCheck,
    History,
    Search,
    Filter,
    ArrowUpRight,
    Package
} from 'lucide-react';
import { useInventory } from '../../hooks/useInventory';
import { useAuth } from '../../context/AuthContext';

export const InventoryDashboard: React.FC = () => {
    const { user } = useAuth();
    const { inventory, isLoading, approveAdjustment } = useInventory();

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
            </div>
        );
    }

    const handleApprove = async (lpnId: string, qty: number) => {
        if (window.confirm(`¿Autorizar ajuste a ${qty} unidades para el LPN ${lpnId}?`)) {
            await approveAdjustment.mutateAsync({
                lpnId,
                newQuantity: qty,
                reason: "SUPERVISOR_APPROVAL_UI"
            });
        }
    };

    return (
        <div className="space-y-8 animate-fade-in">
            {/* Header / Stats */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
                <div className="bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
                    <div className="flex items-center justify-between mb-2">
                        <span className="text-slate-400 font-bold text-[10px] uppercase tracking-wider">Total SKUs</span>
                        <Package className="w-4 h-4 text-blue-500" />
                    </div>
                    <p className="text-2xl font-black text-slate-900">{Array.from(new Set(inventory.map(i => i.sku))).length}</p>
                </div>

                <div className="bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
                    <div className="flex items-center justify-between mb-2">
                        <span className="text-slate-400 font-bold text-[10px] uppercase tracking-wider">Discrepancias</span>
                        <AlertCircle className="w-4 h-4 text-rose-500" />
                    </div>
                    <p className="text-2xl font-black text-slate-900">{inventory.filter(i => i.status === 'Quarantine').length}</p>
                </div>

                <div className="bg-white p-6 rounded-2xl border border-slate-100 shadow-sm">
                    <div className="flex items-center justify-between mb-2">
                        <span className="text-slate-400 font-bold text-[10px] uppercase tracking-wider">Conteo Total</span>
                        <History className="w-4 h-4 text-emerald-500" />
                    </div>
                    <p className="text-2xl font-black text-slate-900">{inventory.reduce((acc, curr) => acc + curr.quantity, 0).toLocaleString()}</p>
                </div>

                <div className="bg-blue-600 p-6 rounded-2xl shadow-lg shadow-blue-100 text-white">
                    <div className="flex items-center justify-between mb-2">
                        <span className="text-blue-100 font-bold text-[10px] uppercase tracking-wider">Salud del Inventario</span>
                        <ShieldCheck className="w-4 h-4 text-white" />
                    </div>
                    <p className="text-2xl font-black">98.2%</p>
                </div>
            </div>

            {/* Table Area */}
            <div className="bg-white rounded-3xl border border-slate-100 shadow-sm overflow-hidden">
                <div className="p-6 border-b border-slate-50 flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <h2 className="text-lg font-bold flex items-center space-x-2">
                        <div className="w-2 h-6 bg-blue-600 rounded-full"></div>
                        <span>Torre de Control de Inventario</span>
                    </h2>

                    <div className="flex items-center space-x-2">
                        <div className="relative">
                            <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                            <input
                                type="text"
                                placeholder="Buscar LPN o SKU..."
                                className="pl-10 pr-4 py-2 bg-slate-50 border-none rounded-xl text-sm focus:ring-2 focus:ring-blue-500 w-64 transition-all"
                            />
                        </div>
                        <button className="p-2 bg-slate-50 rounded-xl hover:bg-slate-100 transition-colors">
                            <Filter className="w-4 h-4 text-slate-600" />
                        </button>
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="border-b border-slate-50">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">LPN ID</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">SKU</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Ubicación</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Cantidad</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest px-8">Estado</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-50">
                            {inventory.length === 0 ? (
                                <tr>
                                    <td colSpan={6} className="px-6 py-12 text-center text-slate-400 italic text-sm">
                                        No hay discrepancias pendientes o inventario cargado para este tenant.
                                    </td>
                                </tr>
                            ) : (
                                inventory.map((item) => (
                                    <tr key={item.id} className="hover:bg-slate-50/50 transition-colors group">
                                        <td className="px-6 py-4">
                                            <span className="font-mono font-bold text-sm text-slate-700">{item.id}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="flex flex-col">
                                                <span className="text-sm font-bold text-slate-900">{item.sku}</span>
                                                <span className="text-[10px] text-slate-400 font-medium">Standard Product</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="flex items-center space-x-2">
                                                <div className="w-2 h-2 rounded-full bg-slate-300"></div>
                                                <span className="text-sm font-semibold text-slate-600">{item.location}</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <span className="text-sm font-black text-slate-900">{item.quantity}</span>
                                        </td>
                                        <td className="px-6 py-4 px-8">
                                            <div className={`inline-flex items-center space-x-1.5 px-3 py-1 rounded-full text-[10px] font-black uppercase tracking-tight ${item.status === 'Quarantine'
                                                ? 'bg-rose-50 text-rose-600'
                                                : item.status === 'Putaway'
                                                    ? 'bg-emerald-50 text-emerald-600'
                                                    : 'bg-slate-100 text-slate-600'
                                                }`}>
                                                {item.status === 'Quarantine' && <AlertCircle className="w-3 h-3" />}
                                                {item.status === 'Putaway' && <CheckCircle2 className="w-3 h-3" />}
                                                <span>{item.status}</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="flex justify-center">
                                                {item.status === 'Quarantine' && user?.role === 'Supervisor' ? (
                                                    <button
                                                        onClick={() => handleApprove(item.id, item.quantity)}
                                                        className="px-4 py-2 bg-rose-600 text-white rounded-xl text-xs font-bold shadow-lg shadow-rose-100 hover:bg-rose-700 transition-all active:scale-95 flex items-center space-x-2"
                                                    >
                                                        <span>Aprobar Ajuste</span>
                                                        <ArrowUpRight className="w-3 h-3" />
                                                    </button>
                                                ) : (
                                                    <button className="p-2 text-slate-300 hover:text-slate-600 transition-colors">
                                                        <History className="w-4 h-4" />
                                                    </button>
                                                )}
                                            </div>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};
