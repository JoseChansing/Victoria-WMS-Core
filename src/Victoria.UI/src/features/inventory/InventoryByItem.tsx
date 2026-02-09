import { useState, useEffect } from 'react';
import {
    Package,
    Search,
    RotateCcw,
    Activity,
    Box
} from 'lucide-react';
import api from '../../api/axiosConfig';

interface InventoryItemView {
    id: string; // SKU
    sku: string;
    description: string;
    totalQuantity: number;
    primaryLocation: string;
    lastUpdated: string;
}

export const InventoryByItem = () => {
    const [items, setItems] = useState<InventoryItemView[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/inventory/items');
            setItems(data);
        } catch (error) {
            console.error('Error fetching items summary:', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const filteredItems = items.filter(item =>
        item.sku.toLowerCase().includes(search.toLowerCase()) ||
        (item.description && item.description.toLowerCase().includes(search.toLowerCase()))
    );

    return (
        <div className="space-y-6 animate-in fade-in duration-500">
            {/* Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <Box className="text-blue-400 w-8 h-8" />
                        Inventario por Item (SKU)
                    </h2>
                    <p className="text-slate-400 font-medium">Resumen consolidado de stock por producto</p>
                </div>
                <div className="flex items-center gap-3">
                    <button
                        onClick={fetchData}
                        className="p-2.5 bg-corp-nav/40 border border-corp-secondary text-slate-300 rounded-xl hover:bg-corp-accent/40 hover:text-white transition-all shadow-sm"
                    >
                        <RotateCcw className={`w-5 h-5 ${loading ? 'animate-spin' : ''}`} />
                    </button>
                </div>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-blue-500/10 rounded-2xl border border-blue-500/20">
                        <Package className="w-6 h-6 text-blue-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">SKUs Activos</p>
                        <p className="text-2xl font-black text-white">{items.length}</p>
                    </div>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-emerald-500/10 rounded-2xl border border-emerald-500/20">
                        <Activity className="w-6 h-6 text-emerald-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Total Unidades</p>
                        <p className="text-2xl font-black text-white">
                            {items.reduce((acc, curr) => acc + curr.totalQuantity, 0).toLocaleString()}
                        </p>
                    </div>
                </div>
            </div>

            {/* Table */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 bg-corp-base/30">
                    <div className="relative flex-1 max-w-xl">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Filtrar por SKU o Descripción..."
                            className="w-full pl-11 pr-4 py-3 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-corp-accent/5 border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Item</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Descripción</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Ubicación</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Stock Total</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Actualizado</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {loading ? (
                                Array(5).fill(0).map((_, i) => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={5} className="px-6 py-8">
                                            <div className="h-4 bg-slate-100/5 rounded-full w-full"></div>
                                        </td>
                                    </tr>
                                ))
                            ) : filteredItems.map(item => (
                                <tr key={item.id} className="hover:bg-corp-accent/5 transition-colors group">
                                    <td className="px-6 py-5">
                                        <span className="font-bold text-white group-hover:text-corp-accent">{item.sku}</span>
                                    </td>
                                    <td className="px-6 py-5">
                                        <span className="text-slate-400 text-sm">{item.description || 'Sin descripción'}</span>
                                    </td>
                                    <td className="px-6 py-5 font-bold text-blue-400 text-sm">
                                        {item.primaryLocation || 'N/A'}
                                    </td>
                                    <td className="px-6 py-5 text-center">
                                        <span className="px-3 py-1 bg-corp-base border border-corp-secondary rounded-lg font-black text-white text-sm">
                                            {item.totalQuantity.toLocaleString()}
                                        </span>
                                    </td>
                                    <td className="px-6 py-5 text-right text-slate-400 text-xs">
                                        {new Date(item.lastUpdated).toLocaleString()}
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
