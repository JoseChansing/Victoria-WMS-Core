import { useState, useEffect } from 'react';
import {
    Package,
    Search,
    Activity,
    RotateCcw,
    ChevronRight,
    Printer,
    History,
    Layout,
} from 'lucide-react';
import api from '../../api/axiosConfig';
import { LpnHistoryModal } from './components/LpnHistoryModal';

interface InventoryItem {
    id: string;
    code: { value: string };
    sku: { value: string };
    type: number;
    quantity: number;
    allocatedQuantity: number;
    currentLocationId: string;
    status: number;
    createdAt: string;
}

const LPN_TYPES: Record<number, string> = {
    0: 'Loose',
    1: 'Pack',
    2: 'Pallet'
};

const LPN_STATUS: Record<number, string> = {
    0: 'Created',
    1: 'Received',
    2: 'Stowed',
    3: 'Allocated',
    4: 'Picked',
    5: 'Packed',
    6: 'Dispatched'
};

export const InventoryDashboard = () => {
    const [inventory, setInventory] = useState<InventoryItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [selectedLpnId, setSelectedLpnId] = useState<string | null>(null);

    const handlePrintLabel = (lpnId: string) => {
        const url = `${api.defaults.baseURL}/printing/lpn/${lpnId}/label`;
        window.open(url, '_blank', 'width=400,height=600');
    };

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/inventory/lpns');
            setInventory(data);
        } catch (error) {
            console.error('Error fetching inventory:', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const filteredInventory = inventory.filter(item => {
        const matchesSearch =
            item.sku.value.toLowerCase().includes(search.toLowerCase()) ||
            item.id.toLowerCase().includes(search.toLowerCase()) ||
            item.code.value.toLowerCase().includes(search.toLowerCase());

        return matchesSearch;
    });

    const getStatusColor = (status: number) => {
        switch (status) {
            case 2: return 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20'; // Ubicado (Available)
            case 3: return 'bg-blue-500/10 text-blue-500 border-blue-500/20'; // Asignado (Reserved)
            case 4: return 'bg-amber-500/10 text-amber-500 border-amber-500/20'; // Picked
            default: return 'bg-slate-500/10 text-slate-500 border-slate-500/20';
        }
    };

    return (
        <div className="space-y-6 animate-in fade-in duration-500">
            {/* ... stats and layout code ... */}
            {/* Header omitted for brevity in targetContent but included in replacement to match file start */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <Package className="text-corp-accent w-8 h-8" />
                        LPN Viewer (Master)
                    </h2>
                    <p className="text-slate-400 font-medium">Unit control by container and master container</p>
                </div>
                <div className="flex items-center gap-3">
                    <button
                        onClick={fetchData}
                        className="p-2.5 bg-corp-nav/40 border border-corp-secondary text-slate-300 rounded-xl hover:bg-corp-accent/40 hover:text-white transition-all shadow-sm"
                    >
                        <RotateCcw className={`w-5 h-5 ${loading ? 'animate-spin' : ''}`} />
                    </button>
                    <div className="h-8 w-[1px] bg-corp-secondary" />
                    <div className="bg-corp-accent/10 px-4 py-2 rounded-xl border border-corp-accent/20">
                        <span className="text-xs font-bold text-corp-accent uppercase tracking-widest leading-none block mb-0.5">Total Units</span>
                        <span className="text-lg font-black text-white leading-none">
                            {inventory.reduce((acc, curr) => acc + curr.quantity, 0).toLocaleString()} <span className="text-xs font-medium text-slate-400">UNITS</span>
                        </span>
                    </div>
                </div>
            </div>

            {/* Stats */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-emerald-500/10 rounded-2xl border border-emerald-500/20">
                        <Activity className="w-6 h-6 text-emerald-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Stowed / Avail.</p>
                        <p className="text-2xl font-black text-white">
                            {inventory.filter(i => i.status === 2).length} <span className="text-xs text-slate-400">LPNs</span>
                        </p>
                    </div>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-blue-500/10 rounded-2xl border border-blue-500/20">
                        <Activity className="w-6 h-6 text-blue-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Committed (Picking)</p>
                        <p className="text-2xl font-black text-white">
                            {inventory.filter(i => i.status === 3).length} <span className="text-xs text-slate-400">LPNs</span>
                        </p>
                    </div>
                </div>
            </div>

            {/* Filters & Table */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 flex flex-col md:flex-row md:items-center justify-between gap-4 bg-corp-base/30">
                    <div className="relative flex-1 max-w-xl">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Search by SKU, or LPN ID..."
                            className="w-full pl-11 pr-4 py-3 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium placeholder:text-slate-600"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-corp-accent/5 border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">LPN Info</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">SKU</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Quantity</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Location</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Status</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {loading ? (
                                Array(5).fill(0).map((_, i) => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={6} className="px-6 py-8">
                                            <div className="h-4 bg-slate-100/5 rounded-full w-full"></div>
                                        </td>
                                    </tr>
                                ))
                            ) : filteredInventory.length === 0 ? (
                                <tr>
                                    <td colSpan={6} className="px-6 py-20 text-center">
                                        <div className="flex flex-col items-center gap-3">
                                            <div className="p-4 bg-corp-secondary/20 rounded-full">
                                                <Package className="w-8 h-8 text-slate-600" />
                                            </div>
                                            <p className="text-slate-500 font-bold">No LPNs found</p>
                                        </div>
                                    </td>
                                </tr>
                            ) : filteredInventory.map(item => (
                                <tr key={item.id} className="hover:bg-corp-accent/5 transition-colors group">
                                    <td className="px-6 py-5">
                                        <div className="flex items-center gap-3">
                                            <div className="w-10 h-10 bg-corp-base rounded-xl border border-corp-secondary flex items-center justify-center group-hover:border-corp-accent/50 transition-colors shadow-sm">
                                                <Layout className="w-5 h-5 text-slate-500 group-hover:text-corp-accent transition-colors" />
                                            </div>
                                            <div className="flex flex-col text-sm">
                                                <button
                                                    onClick={() => setSelectedLpnId(item.id)}
                                                    className="text-left font-bold text-white hover:text-corp-accent transition-colors"
                                                >
                                                    {item.id}
                                                </button>
                                                <span className="text-[10px] font-bold text-slate-500 uppercase tracking-wider">{LPN_TYPES[item.type] || 'Unknown'}</span>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="px-6 py-5">
                                        <div className="flex flex-col text-sm">
                                            <span className="font-bold text-white group-hover:text-corp-accent transition-colors">{item.sku.value}</span>
                                        </div>
                                    </td>
                                    <td className="px-6 py-5 text-center">
                                        <span className="px-3 py-1 bg-corp-base border border-corp-secondary rounded-lg font-black text-white text-sm">
                                            {item.quantity}
                                        </span>
                                    </td>
                                    <td className="px-6 py-5">
                                        <div className="flex flex-col">
                                            <div className="flex items-center gap-2">
                                                <div className="w-2 h-2 rounded-full bg-corp-accent shadow-[0_0_8px_rgba(59,130,246,0.5)]" />
                                                <span className="font-bold text-sm text-white uppercase tracking-tight">{item.currentLocationId}</span>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="px-6 py-5 text-center">
                                        <span className={`inline-flex items-center px-2.5 py-1 rounded-lg border text-[10px] font-black uppercase tracking-widest ${getStatusColor(item.status)}`}>
                                            {LPN_STATUS[item.status] || 'Unknown'}
                                        </span>
                                    </td>
                                    <td className="px-6 py-5 text-right">
                                        <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-all">
                                            <button
                                                onClick={() => handlePrintLabel(item.id)}
                                                className="p-2 text-slate-500 hover:text-white hover:bg-corp-accent/40 rounded-lg transition-all border border-transparent hover:border-corp-secondary/50"
                                                title="Print Label"
                                            >
                                                <Printer className="w-4 h-4" />
                                            </button>
                                            <button
                                                onClick={() => setSelectedLpnId(item.id)}
                                                className="p-2 text-slate-500 hover:text-white hover:bg-corp-accent/40 rounded-lg transition-all border border-transparent hover:border-corp-secondary/50"
                                                title="View History"
                                            >
                                                <History className="w-4 h-4" />
                                            </button>
                                            <div className="w-4" />
                                            <ChevronRight className="w-4 h-4 text-slate-600" />
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>

            {/* History Modal */}
            {selectedLpnId && (
                <LpnHistoryModal
                    lpnId={selectedLpnId}
                    onClose={() => setSelectedLpnId(null)}
                />
            )}
        </div>
    );
};
