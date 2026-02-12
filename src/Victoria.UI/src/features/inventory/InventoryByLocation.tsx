import { useState, useEffect } from 'react';
import {
    MapPin,
    Search,
    RotateCcw,
    Activity,
    Package,
    Layers,
    Info
} from 'lucide-react';
import api from '../../api/axiosConfig';

interface LpnDetailSummary {
    lpnId: string;
    sku: string;
    description: string;
    quantity: number;
    allocatedQuantity: number;
    status: number;
}

interface LocationInventoryView {
    id: string; // LocationId
    locationType: string;
    lpns: LpnDetailSummary[];
    totalItems: number;
}

interface FlattenedInventory {
    locationId: string;
    locationType: string;
    lpnId: string;
    sku: string;
    description: string;
    quantity: number;
    allocatedQuantity: number;
    status: number;
}

export const InventoryByLocation = () => {
    const [locations, setLocations] = useState<LocationInventoryView[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/inventory/by-location');
            // Data structure: { locationId, totalQty, lpnCount, items: [ { sku, quantity } ] }
            const flattened: FlattenedInventory[] = data.flatMap((loc: any) =>
                loc.items.map((item: any) => ({
                    locationId: loc.locationId,
                    locationType: 'Almacenamiento',
                    lpnId: item.quantity === loc.totalQty && loc.lpnCount === 1 ? 'PALLET' : 'LOOSE',
                    sku: item.sku,
                    description: item.description || '',
                    quantity: item.quantity,
                    allocatedQuantity: 0,
                    status: 2
                }))
            );
            setLocations(flattened as any); // Adapt manually to avoid major refactor
        } catch (error) {
            console.error('Error fetching inventory by location:', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const flattenedData: FlattenedInventory[] = Array.isArray(locations) ? locations as any : [];

    const filteredData = flattenedData.filter(item =>
        item.sku.toLowerCase().includes(search.toLowerCase()) ||
        item.locationId.toLowerCase().includes(search.toLowerCase()) ||
        (item.description && item.description.toLowerCase().includes(search.toLowerCase())) ||
        item.lpnId.toLowerCase().includes(search.toLowerCase())
    );

    const getStatusInfo = (status: number) => {
        switch (status) {
            case 1: return { label: 'Recibido', color: 'bg-blue-500/10 text-blue-500 border-blue-500/20' };
            case 2: return { label: 'Ubicado', color: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20' };
            case 3: return { label: 'En Picking', color: 'bg-amber-500/10 text-amber-500 border-amber-500/20' };
            default: return { label: 'Desconocido', color: 'bg-slate-500/10 text-slate-500 border-slate-500/20' };
        }
    };

    return (
        <div className="space-y-6 animate-in fade-in duration-500">
            {/* Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <MapPin className="text-emerald-400 w-8 h-8" />
                        Inventario por Ubicación
                    </h2>
                    <p className="text-slate-400 font-medium">Visualización detallada de existencias por estantería y zona</p>
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
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-blue-500/10 rounded-2xl border border-blue-500/20">
                        <MapPin className="w-6 h-6 text-blue-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Ubicaciones con Stock</p>
                        <p className="text-2xl font-black text-white">{locations.length}</p>
                    </div>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-purple-500/10 rounded-2xl border border-purple-500/20">
                        <Layers className="w-6 h-6 text-purple-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Total LPNs</p>
                        <p className="text-2xl font-black text-white">{flattenedData.length}</p>
                    </div>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-emerald-500/10 rounded-2xl border border-emerald-500/20">
                        <Activity className="w-6 h-6 text-emerald-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Stock Total</p>
                        <p className="text-2xl font-black text-white">
                            {flattenedData.reduce((acc, curr) => acc + curr.quantity, 0).toLocaleString()}
                        </p>
                    </div>
                </div>
            </div>

            {/* Table */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 bg-corp-base/30 flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <div className="relative flex-1 max-w-xl">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Buscar por Ubicación, SKU, LPN o Descripción..."
                            className="w-full pl-11 pr-4 py-3 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                    <div className="flex items-center gap-2 text-xs text-slate-500 font-bold bg-corp-base/50 px-4 py-2 rounded-xl border border-corp-secondary/30">
                        <Info className="w-4 h-4 text-blue-400" />
                        Muestra el detalle atomizado por cada contenedor en ubicación
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-corp-accent/5 border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Item / SKU</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Ubicación</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">LPN</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Estado</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Cant. Actual</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Allocated</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Available</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {loading ? (
                                Array(5).fill(0).map((_, i) => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={7} className="px-6 py-8">
                                            <div className="h-4 bg-slate-100/5 rounded-full w-full"></div>
                                        </td>
                                    </tr>
                                ))
                            ) : filteredData.length === 0 ? (
                                <tr>
                                    <td colSpan={7} className="px-6 py-12 text-center">
                                        <div className="flex flex-col items-center gap-3">
                                            <Package className="w-12 h-12 text-slate-700" />
                                            <p className="text-slate-500 font-bold">No se encontró inventario en ubicaciones</p>
                                        </div>
                                    </td>
                                </tr>
                            ) : filteredData.map((item, idx) => {
                                const status = getStatusInfo(item.status);
                                return (
                                    <tr key={`${item.lpnId}-${idx}`} className="hover:bg-corp-accent/5 transition-colors group">
                                        <td className="px-6 py-5">
                                            <div className="flex flex-col">
                                                <span className="font-bold text-white group-hover:text-corp-accent">{item.sku}</span>
                                                <span className="text-[10px] text-slate-500 font-medium truncate max-w-[150px]">
                                                    {item.description || 'Sin descripción'}
                                                </span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-5">
                                            <div className="flex flex-col">
                                                <span className="font-bold text-emerald-400 text-sm">{item.locationId}</span>
                                                <span className="text-[10px] text-slate-500 font-black uppercase tracking-tighter">
                                                    {item.locationType}
                                                </span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className="text-xs font-mono text-slate-300 bg-corp-base/50 px-2 py-1 rounded border border-corp-secondary/30">
                                                {item.lpnId}
                                            </span>
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className={`px-2 py-1 rounded-md text-[10px] font-black uppercase border ${status.color}`}>
                                                {status.label}
                                            </span>
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className="font-black text-white">{item.quantity}</span>
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className="text-slate-400 font-medium">{item.allocatedQuantity}</span>
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className="text-emerald-400 font-black">
                                                {item.quantity - item.allocatedQuantity}
                                            </span>
                                        </td>
                                    </tr>
                                );
                            })}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};
