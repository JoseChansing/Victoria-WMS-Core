import React, { useState, useEffect } from 'react';
import { X, Trash2, AlertCircle, CheckSquare, Square, Search } from 'lucide-react';
import axios from 'axios';
import { useInbound } from '../../hooks/useInbound';

interface LpnDetail {
    id: string;
    sku: string;
    currentLocationId: string;
    status: string;
    quantity: number;
}

interface LpnManagementModalProps {
    sku: string;
    orderId: string;
    onClose: () => void;
    onVoidSuccess: () => void;
}

const LpnManagementModal: React.FC<LpnManagementModalProps> = ({ sku, orderId, onClose, onVoidSuccess }) => {
    const { voidLpn } = useInbound();
    const [lpns, setLpns] = useState<LpnDetail[]>([]);
    const [loading, setLoading] = useState(true);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [isVoiding, setIsVoiding] = useState(false);
    const [searchTerm, setSearchTerm] = useState('');

    useEffect(() => {
        fetchLpns();
    }, [sku, orderId]);

    const fetchLpns = async () => {
        setLoading(true);
        try {
            // Usando el endpoint de inspección que ya existe o uno similar
            const response = await axios.get(`http://localhost:5242/api/v1/inbound/debug/inspect-reception?orderNumber=${orderId}`);
            // Filtrar por SKU y que no estén ya anulados (Status 11 o "Voided")
            const filteredLpns = (response.data.lpns || []).filter((l: any) =>
                l.sku.value === sku && l.status !== 'Voided' && l.status !== 11
            );
            setLpns(filteredLpns);
        } catch (error) {
            console.error("Error fetching LPNs", error);
        } finally {
            setLoading(false);
        }
    };

    const toggleSelect = (id: string) => {
        setSelectedIds(prev =>
            prev.includes(id) ? prev.filter(i => i !== id) : [...prev, id]
        );
    };

    const toggleSelectAll = () => {
        if (selectedIds.length === filteredLpns.length) {
            setSelectedIds([]);
        } else {
            setSelectedIds(filteredLpns.map(l => l.id));
        }
    };

    const handleBulkVoid = async () => {
        if (selectedIds.length === 0) return;

        if (!window.confirm(`¿Estás seguro de anular ${selectedIds.length} LPNs? Esta acción no se puede deshacer.`)) {
            return;
        }

        setIsVoiding(true);
        try {
            for (const id of selectedIds) {
                await voidLpn(id);
            }
            onVoidSuccess();
            fetchLpns();
            setSelectedIds([]);
        } catch (error) {
            alert("Error al anular algunos LPNs");
            console.error(error);
        } finally {
            setIsVoiding(false);
        }
    };

    const filteredLpns = lpns.filter(l => l.id.toLowerCase().includes(searchTerm.toLowerCase()));

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in duration-300">
            <div className="bg-slate-900 border border-slate-700/50 rounded-2xl shadow-2xl w-full max-w-2xl overflow-hidden animate-in zoom-in-95 duration-200">
                <div className="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-900 sticky top-0">
                    <div>
                        <h3 className="text-lg font-bold text-white">Gestionar Bultos - {sku}</h3>
                        <p className="text-xs text-slate-400">Visualizando LPNs recibidos para este ítem</p>
                    </div>
                    <button onClick={onClose} className="p-2 hover:bg-slate-800 rounded-full transition-colors">
                        <X className="w-5 h-5 text-slate-500" />
                    </button>
                </div>

                <div className="p-6">
                    <div className="flex gap-4 mb-6">
                        <div className="flex-1 relative">
                            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                            <input
                                type="text"
                                placeholder="Buscar LPN ID..."
                                className="w-full pl-10 pr-4 py-2 bg-slate-800/50 border border-slate-700 rounded-lg text-sm text-white focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 transition-all outline-none placeholder:text-slate-500"
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                            />
                        </div>
                    </div>

                    <div className="border border-slate-100 rounded-xl overflow-hidden mb-6">
                        <div className="max-h-[400px] overflow-y-auto">
                            <table className="w-full text-left border-collapse">
                                <thead className="bg-slate-50 sticky top-0 z-10">
                                    <tr>
                                        <th className="p-3 w-10">
                                            <button onClick={toggleSelectAll} className="text-slate-400 hover:text-indigo-600 transition-colors">
                                                {selectedIds.length > 0 && selectedIds.length === filteredLpns.length ? (
                                                    <CheckSquare className="w-5 h-5 text-indigo-600" />
                                                ) : (
                                                    <Square className="w-5 h-5" />
                                                )}
                                            </button>
                                        </th>
                                        <th className="p-3 text-xs font-bold text-slate-400 uppercase tracking-wider">LPN ID</th>
                                        <th className="p-3 text-xs font-bold text-slate-400 uppercase tracking-wider">Ubicación</th>
                                        <th className="p-3 text-xs font-bold text-slate-400 uppercase tracking-wider text-right">Cantidad</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-slate-100">
                                    {loading ? (
                                        <tr><td colSpan={4} className="p-10 text-center text-slate-500 text-sm">Cargando...</td></tr>
                                    ) : filteredLpns.length === 0 ? (
                                        <tr><td colSpan={4} className="p-10 text-center text-slate-500 text-sm">No se encontraron LPNs</td></tr>
                                    ) : (
                                        filteredLpns.map((lpn) => (
                                            <tr key={lpn.id} className={`hover:bg-slate-800/50 transition-colors border-b border-slate-800/50 ${selectedIds.includes(lpn.id) ? 'bg-indigo-500/10' : ''}`}>
                                                <td className="p-3">
                                                    <button onClick={() => toggleSelect(lpn.id)} className="text-slate-500 hover:text-indigo-400 transition-colors">
                                                        {selectedIds.includes(lpn.id) ? (
                                                            <CheckSquare className="w-5 h-5 text-indigo-400" />
                                                        ) : (
                                                            <Square className="w-5 h-5" />
                                                        )}
                                                    </button>
                                                </td>
                                                <td className="p-3 text-sm font-mono font-medium text-slate-300">{lpn.id}</td>
                                                <td className="p-3">
                                                    <span className="text-xs px-2 py-1 bg-slate-800 text-slate-400 border border-slate-700 rounded-md font-medium">
                                                        {lpn.currentLocationId || 'Unknown'}
                                                    </span>
                                                </td>
                                                <td className="p-3 text-right">
                                                    <span className="text-sm font-bold text-white">
                                                        {lpn.quantity}
                                                    </span>
                                                </td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>

                <div className="p-6 bg-slate-900 border-t border-slate-800 flex items-center justify-between">
                    <div className="flex items-center gap-2 text-sm text-slate-400">
                        <AlertCircle className="w-4 h-4 text-slate-500" />
                        {selectedIds.length} seleccionados
                    </div>
                    <div className="flex gap-3">
                        <button
                            onClick={onClose}
                            className="px-6 py-2 bg-slate-800 border border-slate-700 text-slate-300 text-sm font-bold rounded-xl hover:bg-slate-700 transition-all font-inter"
                        >
                            Cerrar
                        </button>
                        <button
                            onClick={handleBulkVoid}
                            disabled={selectedIds.length === 0 || isVoiding}
                            className="flex items-center gap-2 px-6 py-2 bg-red-600 text-white text-sm font-bold rounded-xl hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-red-200 transition-all font-inter"
                        >
                            {isVoiding ? 'Anulando...' : 'Anular Seleccionados'}
                            <Trash2 className="w-4 h-4" />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LpnManagementModal;
