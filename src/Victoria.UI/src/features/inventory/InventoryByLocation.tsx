import { useState, useEffect } from 'react';
import {
    MapPin,
    Search,
    RotateCcw,
    Activity,
    Package,
    Layers,
    Info,
    CheckSquare,
    Square,
    Lock
} from 'lucide-react';
import api from '../../api/axiosConfig';
import { inventoryService } from '../../services/inventory';
import { BulkActionsBar } from './components/BulkActionsBar';
import { toast } from 'sonner';
import { ItemLpnDetailModal } from './components/ItemLpnDetailModal';
import { BatchSampleModal } from './components/BatchSampleModal';
import type { InventoryTask } from '../../services/inventory';

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
    locationType?: string;
    lpnId: string;
    sku: string;
    description: string;
    quantity: number;
    allocatedQuantity: number;
    currentTaskId?: string;
    status: number;
    lpnType?: string;
}

export const InventoryByLocation = () => {
    const [locations, setLocations] = useState<LocationInventoryView[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');

    // Bulk Actions State
    const [selectedIds, setSelectedIds] = useState<string[]>([]); // Selecting LpnIds
    const [isGenerating, setIsGenerating] = useState(false);
    const [selectedSku, setSelectedSku] = useState<string | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [samplingTask, setSamplingTask] = useState<InventoryTask | null>(null);
    const [isSampleModalOpen, setIsSampleModalOpen] = useState(false);

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/inventory/by-location');
            // Backend now returns flat list of LPNs
            setLocations(data);
            setSelectedIds([]);
        } catch (error) {
            console.error('Error fetching inventory by location:', error);
            toast.error('Failed to load inventory');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const flattenedData: FlattenedInventory[] = Array.isArray(locations) ? locations as any : [];
    const uniqueLocationsCount = new Set(flattenedData.map(i => i.locationId)).size;

    const filteredData = flattenedData.filter(item =>
        item.sku.toLowerCase().includes(search.toLowerCase()) ||
        item.locationId.toLowerCase().includes(search.toLowerCase()) ||
        (item.description && item.description.toLowerCase().includes(search.toLowerCase())) ||
        item.lpnId.toLowerCase().includes(search.toLowerCase())
    );

    // Selection Handlers
    const toggleSelectAll = () => {
        if (selectedIds.length === filteredData.length && filteredData.length > 0) {
            setSelectedIds([]);
        } else {
            setSelectedIds(filteredData.map(i => i.lpnId));
        }
    };

    const toggleSelect = (id: string) => {
        if (selectedIds.includes(id)) {
            setSelectedIds(selectedIds.filter(i => i !== id));
        } else {
            setSelectedIds([...selectedIds, id]);
        }
    };

    const handleGenerateBatch = async (taskType: string) => {
        if (selectedIds.length === 0) return;

        setIsGenerating(true);
        try {
            const result = await inventoryService.createBatchTasks({
                taskType: taskType,
                priority: 'Normal',
                targetType: 'Lpn',
                targetIds: selectedIds
            });

            if (taskType === 'TakeSample') {
                const allTasks = await inventoryService.getTasks();
                const fullTask = allTasks.find(t => t.id === result.taskId);
                if (fullTask) {
                    setSamplingTask(fullTask);
                    setIsSampleModalOpen(true);
                } else {
                    toast.error("Created task not found for sampling modal");
                }
            } else {
                toast.success(`Task generated successfully with ${selectedIds.length} lines`);
            }

            setSelectedIds([]);
        } catch (error) {
            console.error('Batch creation failed', error);
            toast.error('Failed to generate tasks');
        } finally {
            setIsGenerating(false);
        }
    };

    const getStatusInfo = (status: number) => {
        switch (status) {
            case 1: return { label: 'Received', color: 'bg-blue-500/10 text-blue-500 border-blue-500/20' };
            case 2: return { label: 'Putaway', color: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20' };
            case 3: return { label: 'Available', color: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20' };
            case 4: return { label: 'Allocated', color: 'bg-blue-500/10 text-blue-500 border-blue-500/20' };
            case 5: return { label: 'Picked', color: 'bg-amber-500/10 text-amber-500 border-amber-500/20' };
            case 9: return { label: 'Counting', color: 'bg-yellow-500/10 text-yellow-500 border-yellow-500/20' };
            case 8: return { label: 'Quarantine', color: 'bg-rose-500/10 text-rose-500 border-rose-500/20' };
            case 10: return { label: 'Consumed', color: 'bg-slate-500/10 text-slate-500 border-slate-500/20' };
            case 11: return { label: 'Voided', color: 'bg-red-500/10 text-red-500 border-red-500/20' };
            default: return { label: `Status ${status}`, color: 'bg-slate-500/10 text-slate-500 border-slate-500/20' };
        }
    };

    return (
        <div className="flex flex-col h-[calc(100vh-160px)] animate-in fade-in duration-500">
            {/* Header */}
            <div className="shrink-0 flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <MapPin className="text-emerald-400 w-8 h-8" />
                        Inventory by Location
                    </h2>
                    <p className="text-slate-400 font-medium">Detailed stock visualization by rack and zone</p>
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
            <div className="shrink-0 grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-blue-500/10 rounded-2xl border border-blue-500/20">
                        <MapPin className="w-6 h-6 text-blue-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Locations with Stock</p>
                        <p className="text-2xl font-black text-white">{uniqueLocationsCount}</p>
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
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Total Stock</p>
                        <p className="text-2xl font-black text-white">
                            {flattenedData.reduce((acc, curr) => acc + curr.quantity, 0).toLocaleString()}
                        </p>
                    </div>
                </div>
            </div>

            {/* Table Container */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl flex flex-col flex-1 overflow-hidden min-h-0">
                <div className="p-6 border-b border-corp-secondary/50 bg-corp-base/30 flex flex-col md:flex-row md:items-center justify-between gap-4 shrink-0">
                    <div className="relative flex-1 max-w-xl">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Search by Location, SKU, LPN or Description..."
                            className="w-full pl-11 pr-4 py-3 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                    <div className="flex items-center gap-2 text-xs text-slate-500 font-bold bg-corp-base/50 px-4 py-2 rounded-xl border border-corp-secondary/30">
                        <Info className="w-4 h-4 text-blue-400" />
                        Shows atomized detail per container in location
                    </div>
                </div>

                <div className="flex-1 overflow-auto no-scrollbar">
                    <table className="w-full text-left border-collapse">
                        <thead className="sticky top-0 z-10 bg-corp-base/90 backdrop-blur-md">
                            <tr className="border-b border-corp-secondary/30">
                                <th className="px-6 py-4 w-12">
                                    <button
                                        onClick={toggleSelectAll}
                                        className="flex items-center justify-center text-slate-400 hover:text-white transition-colors"
                                    >
                                        {selectedIds.length > 0 && selectedIds.length === filteredData.length ? (
                                            <CheckSquare className="w-5 h-5 text-corp-accent" />
                                        ) : (
                                            <Square className="w-5 h-5" />
                                        )}
                                    </button>
                                </th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest min-w-[120px]">Item / SKU</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Location</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">LPN</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Status</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Quantity</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center hidden lg:table-cell">Allocated</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center hidden lg:table-cell">Available</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {loading ? (
                                Array(5).fill(0).map((_, i) => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={8} className="px-6 py-8">
                                            <div className="h-4 bg-slate-100/5 rounded-full w-full"></div>
                                        </td>
                                    </tr>
                                ))
                            ) : filteredData.length === 0 ? (
                                <tr>
                                    <td colSpan={8} className="px-6 py-12 text-center">
                                        <div className="flex flex-col items-center gap-3">
                                            <Package className="w-12 h-12 text-slate-700" />
                                            <p className="text-slate-500 font-bold">No inventory found in locations</p>
                                        </div>
                                    </td>
                                </tr>
                            ) : filteredData.map((item, idx) => {
                                const status = getStatusInfo(item.status);
                                const isSelected = selectedIds.includes(item.lpnId);
                                const isLockedByTask = item.currentTaskId != null || item.status === 9; // Counting = 9
                                const isAllocated = item.status === 4; // Allocated = 4
                                const isDisabled = isLockedByTask || isAllocated;

                                return (
                                    <tr key={`${item.lpnId}-${idx}`} className={`transition-colors group ${isSelected ? 'bg-corp-accent/10' : 'hover:bg-corp-accent/5'} ${isDisabled ? 'opacity-60' : ''}`}>
                                        <td className="px-6 py-5">
                                            <button
                                                onClick={() => !isDisabled && toggleSelect(item.lpnId)}
                                                disabled={isDisabled}
                                                className={`flex items-center justify-center transition-colors ${isDisabled ? 'cursor-not-allowed' : 'text-slate-400 hover:text-white'}`}
                                            >
                                                {isLockedByTask ? (
                                                    <Lock className="w-5 h-5 text-yellow-500" />
                                                ) : isAllocated ? (
                                                    <Lock className="w-5 h-5 text-blue-500" />
                                                ) : isSelected ? (
                                                    <CheckSquare className="w-5 h-5 text-corp-accent" />
                                                ) : (
                                                    <Square className="w-5 h-5" />
                                                )}
                                            </button>
                                        </td>
                                        <td className="px-6 py-5">
                                            <div className="flex flex-col">
                                                <button
                                                    onClick={() => {
                                                        setSelectedSku(item.sku);
                                                        setIsModalOpen(true);
                                                    }}
                                                    className="group/link flex flex-col items-start px-0 text-left"
                                                >
                                                    <span className="font-bold text-white group-hover/link:text-corp-accent whitespace-nowrap border-b border-transparent group-hover/link:border-corp-accent/30 transition-all">
                                                        {item.sku}
                                                    </span>
                                                </button>
                                                <span className="text-[10px] text-slate-500 font-medium truncate max-w-[150px] hidden xl:block">
                                                    {item.description || 'No description'}
                                                </span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-5">
                                            <div className="flex flex-col">
                                                <span className="font-bold text-emerald-400 text-sm">{item.locationId}</span>
                                                <span className="text-[10px] text-slate-500 font-black uppercase tracking-tighter">
                                                    {(item.locationId || '').startsWith('STAG') ? 'Storage' : 'Picking'}
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
                                        <td className="px-6 py-5 text-center hidden lg:table-cell">
                                            <span className="text-slate-400 font-medium">{item.allocatedQuantity}</span>
                                        </td>
                                        <td className="px-6 py-5 text-center hidden lg:table-cell">
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

            <BulkActionsBar
                selectedCount={selectedIds.length}
                onGenerateTask={handleGenerateBatch}
                onClear={() => setSelectedIds([])}
                isProcessing={isGenerating}
            />

            <ItemLpnDetailModal
                sku={selectedSku}
                isOpen={isModalOpen}
                onClose={() => setIsModalOpen(false)}
            />

            <BatchSampleModal
                task={samplingTask}
                isOpen={isSampleModalOpen}
                onClose={() => {
                    setIsSampleModalOpen(false);
                    setSamplingTask(null);
                    fetchData(); // Refresh to show released/consumed LPNs
                }}
                onComplete={() => {
                    fetchData();
                }}
            />
        </div>
    );
};
