import { useState, useEffect } from 'react';
import {
    Package,
    Search,
    RotateCcw,
    Activity,
    Box,
    CheckSquare,
    Square
} from 'lucide-react';
import api from '../../api/axiosConfig';
import { inventoryService } from '../../services/inventory';
import { BulkActionsBar } from './components/BulkActionsBar';
import { toast } from 'sonner';
import { ItemLpnDetailModal } from './components/ItemLpnDetailModal';
import { BatchSampleModal } from './components/BatchSampleModal';
import type { InventoryTask } from '../../services/inventory';

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

    // Bulk Actions State
    const [selectedSkus, setSelectedSkus] = useState<string[]>([]);
    const [isGenerating, setIsGenerating] = useState(false);
    const [selectedSku, setSelectedSku] = useState<string | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [samplingTask, setSamplingTask] = useState<InventoryTask | null>(null);
    const [isSampleModalOpen, setIsSampleModalOpen] = useState(false);

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/inventory/items');
            setItems(data);
            setSelectedSkus([]);
        } catch (error) {
            console.error('Error fetching items summary:', error);
            toast.error('Failed to load inventory items');
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

    // Selection Handlers
    const toggleSelectAll = () => {
        if (selectedSkus.length === filteredItems.length && filteredItems.length > 0) {
            setSelectedSkus([]);
        } else {
            setSelectedSkus(filteredItems.map(i => i.sku));
        }
    };

    const toggleSelect = (sku: string) => {
        if (selectedSkus.includes(sku)) {
            setSelectedSkus(selectedSkus.filter(s => s !== sku));
        } else {
            setSelectedSkus([...selectedSkus, sku]);
        }
    };

    const handleGenerateBatch = async (taskType: string) => {
        if (selectedSkus.length === 0) return;

        setIsGenerating(true);
        try {
            const result = await inventoryService.createBatchTasks({
                taskType: taskType,
                priority: 'Normal',
                targetType: 'Product', // Backend maps this to SKU
                targetIds: selectedSkus
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
                toast.success(`Task generated successfully with ${selectedSkus.length} lines`);
            }

            setSelectedSkus([]);
        } catch (error) {
            console.error('Batch creation failed', error);
            toast.error('Failed to generate tasks');
        } finally {
            setIsGenerating(false);
        }
    };

    return (
        <div className="flex flex-col h-[calc(100vh-160px)] animate-in fade-in duration-500">
            {/* Header */}
            <div className="shrink-0 flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <Box className="text-blue-400 w-8 h-8" />
                        Inventory by Item (SKU)
                    </h2>
                    <p className="text-slate-400 font-medium">Consolidated stock summary by product</p>
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
            <div className="shrink-0 grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-blue-500/10 rounded-2xl border border-blue-500/20">
                        <Package className="w-6 h-6 text-blue-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Active SKUs</p>
                        <p className="text-2xl font-black text-white">{items.length}</p>
                    </div>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-3xl border border-corp-secondary shadow-xl shadow-black/10 flex items-center gap-4">
                    <div className="p-3 bg-emerald-500/10 rounded-2xl border border-emerald-500/20">
                        <Activity className="w-6 h-6 text-emerald-500" />
                    </div>
                    <div>
                        <p className="text-xs font-black text-slate-500 uppercase tracking-wider">Total Units</p>
                        <p className="text-2xl font-black text-white">
                            {items.reduce((acc, curr) => acc + curr.totalQuantity, 0).toLocaleString()}
                        </p>
                    </div>
                </div>
            </div>

            {/* Table Container */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl flex flex-col flex-1 overflow-hidden min-h-0">
                <div className="p-6 border-b border-corp-secondary/50 bg-corp-base/30 shrink-0">
                    <div className="relative flex-1 max-w-xl">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Filter by SKU or Description..."
                            className="w-full pl-11 pr-4 py-3 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
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
                                        {selectedSkus.length > 0 && selectedSkus.length === filteredItems.length ? (
                                            <CheckSquare className="w-5 h-5 text-corp-accent" />
                                        ) : (
                                            <Square className="w-5 h-5" />
                                        )}
                                    </button>
                                </th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest min-w-[140px]">Item</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest hidden md:table-cell">Description</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Location</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Total Stock</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right hidden lg:table-cell">Last Updated</th>
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
                            ) : filteredItems.map(item => {
                                const isSelected = selectedSkus.includes(item.sku);
                                return (
                                    <tr key={item.id} className={`transition-colors group ${isSelected ? 'bg-corp-accent/10' : 'hover:bg-corp-accent/5'}`}>
                                        <td className="px-6 py-5">
                                            <button
                                                onClick={() => toggleSelect(item.sku)}
                                                className="flex items-center justify-center text-slate-400 hover:text-white transition-colors"
                                            >
                                                {isSelected ? (
                                                    <CheckSquare className="w-5 h-5 text-corp-accent" />
                                                ) : (
                                                    <Square className="w-5 h-5" />
                                                )}
                                            </button>
                                        </td>
                                        <td className="px-6 py-5">
                                            <button
                                                onClick={() => {
                                                    setSelectedSku(item.sku);
                                                    setIsModalOpen(true);
                                                }}
                                                className="group/link flex flex-col items-start"
                                            >
                                                <span className="font-mono text-sm font-bold text-white group-hover/link:text-corp-accent border-b border-transparent group-hover/link:border-corp-accent/30 transition-all">
                                                    {item.sku}
                                                </span>
                                            </button>
                                        </td>
                                        <td className="px-6 py-5 hidden md:table-cell">
                                            <span className="text-slate-400 text-sm line-clamp-1">{item.description || 'No description'}</span>
                                        </td>
                                        <td className="px-6 py-5 font-bold text-blue-400 text-sm whitespace-nowrap">
                                            {item.primaryLocation || 'N/A'}
                                        </td>
                                        <td className="px-6 py-5 text-center">
                                            <span className="px-3 py-1 bg-corp-base border border-corp-secondary rounded-lg font-black text-white text-sm">
                                                {item.totalQuantity.toLocaleString()}
                                            </span>
                                        </td>
                                        <td className="px-6 py-5 text-right text-slate-400 text-xs hidden lg:table-cell">
                                            {item.lastUpdated && !isNaN(new Date(item.lastUpdated).getTime())
                                                ? new Date(item.lastUpdated).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                                                : 'N/A'}
                                        </td>
                                    </tr>
                                )
                            })}
                        </tbody>
                    </table>
                </div>
            </div>

            <BulkActionsBar
                selectedCount={selectedSkus.length}
                onGenerateTask={handleGenerateBatch}
                onClear={() => setSelectedSkus([])}
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
