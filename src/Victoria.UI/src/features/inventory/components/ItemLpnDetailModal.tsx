import React, { useEffect, useState } from 'react';
import { X, Package, MapPin, Clock, Calendar, CheckSquare, Square, Lock } from 'lucide-react';
import { inventoryService } from '../../../services/inventory';
import { BulkActionsBar } from '../components/BulkActionsBar';
import { toast } from 'sonner';

interface ItemLpnDetail {
    lpnId: string;
    locationId: string;
    locationType: string;
    quantity: number;
    allocatedQuantity: number;
    status: number;
    currentTaskId?: string;
    createdAt: string;
}

interface ItemLpnDetailModalProps {
    sku: string | null;
    isOpen: boolean;
    onClose: () => void;
}

export const ItemLpnDetailModal: React.FC<ItemLpnDetailModalProps> = ({ sku, isOpen, onClose }) => {
    const [lpns, setLpns] = useState<ItemLpnDetail[]>([]);
    const [loading, setLoading] = useState(false);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [isGenerating, setIsGenerating] = useState(false);

    useEffect(() => {
        if (isOpen && sku) {
            const fetchLpns = async () => {
                setLoading(true);
                try {
                    const data = await inventoryService.getItemLpns(sku);
                    setLpns(data);
                    setSelectedIds([]);
                } catch (error) {
                    console.error('Failed to fetch item LPNs:', error);
                    toast.error('Failed to load LPN details');
                } finally {
                    setLoading(false);
                }
            };
            fetchLpns();
        }
    }, [isOpen, sku]);

    const toggleSelectAll = () => {
        if (selectedIds.length === lpns.length && lpns.length > 0) {
            setSelectedIds([]);
        } else {
            setSelectedIds(lpns.map(l => l.lpnId));
        }
    };

    const toggleSelect = (id: string) => {
        if (selectedIds.includes(id)) {
            setSelectedIds(selectedIds.filter(i => i !== id));
        } else {
            setSelectedIds([...selectedIds, id]);
        }
    };

    const handleGenerateTask = async (taskType: string) => {
        if (selectedIds.length === 0) return;

        setIsGenerating(true);
        try {
            const response = await inventoryService.createBatchTasks({
                taskType: taskType,
                priority: 'Normal',
                targetType: 'Lpn',
                targetIds: selectedIds
            });

            const { taskId, warnings } = response;

            if (taskId) {
                toast.success(`Task generated successfully: ${taskId.substring(0, 8)}...`);
            }

            if (warnings && warnings.length > 0) {
                warnings.forEach(w => toast.warning(w, { duration: 5000 }));
            }

            setSelectedIds([]);
            // Refresh to show locks
            const data = await inventoryService.getItemLpns(sku!);
            setLpns(data);
        } catch (error: any) {
            console.error('Task generation failed', error);
            const msg = error.response?.data?.Error || 'Failed to generate tasks';
            toast.error(msg);
        } finally {
            setIsGenerating(false);
        }
    };

    if (!isOpen || !sku) return null;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm animate-in fade-in duration-300 p-4">
            <div className="bg-corp-base border border-corp-secondary w-full max-w-5xl rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-in zoom-in-95 duration-300 relative">
                {/* Header */}
                <div className="bg-corp-nav/60 px-8 py-6 border-b border-corp-secondary/50 flex justify-between items-center shrink-0">
                    <div className="flex items-center gap-4">
                        <div className="bg-corp-accent/20 p-3 rounded-2xl text-corp-accent border border-corp-accent/30">
                            <Package className="w-6 h-6" />
                        </div>
                        <div>
                            <h2 className="text-xl font-black text-white tracking-tight">LPN Inventory Details</h2>
                            <div className="flex items-center gap-2 mt-0.5">
                                <span className="text-sm font-bold text-corp-accent">{sku}</span>
                                <span className="w-1 h-1 bg-slate-700 rounded-full" />
                                <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">{lpns.length} Containers Found</span>
                            </div>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 text-slate-400 hover:text-white hover:bg-white/5 rounded-xl transition-all"
                    >
                        <X className="w-6 h-6" />
                    </button>
                </div>

                {/* Content */}
                <div className="flex-1 overflow-auto no-scrollbar p-6 pb-32">
                    {loading ? (
                        <div className="flex flex-col items-center justify-center py-20">
                            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-corp-accent"></div>
                            <p className="mt-4 text-slate-400 font-bold animate-pulse">Fetching inventory details...</p>
                        </div>
                    ) : lpns.length === 0 ? (
                        <div className="flex flex-col items-center justify-center py-20 text-center">
                            <div className="p-4 bg-corp-secondary/20 rounded-full mb-4">
                                <Package className="w-12 h-12 text-slate-600" />
                            </div>
                            <p className="text-slate-400 font-bold text-lg">No LPNs found for this item</p>
                            <p className="text-slate-600">This item might only exist in master data without active stock.</p>
                        </div>
                    ) : (
                        <div className="bg-corp-nav/40 rounded-2xl border border-corp-secondary overflow-hidden">
                            <table className="w-full text-left border-collapse">
                                <thead className="sticky top-0 z-10 bg-corp-base/90 backdrop-blur-md border-b border-corp-secondary/50">
                                    <tr>
                                        <th className="px-6 py-4 w-12">
                                            <button
                                                onClick={toggleSelectAll}
                                                className="flex items-center justify-center text-slate-400 hover:text-white transition-colors"
                                            >
                                                {selectedIds.length > 0 && selectedIds.length === lpns.length ? (
                                                    <CheckSquare className="w-5 h-5 text-corp-accent" />
                                                ) : (
                                                    <Square className="w-5 h-5" />
                                                )}
                                            </button>
                                        </th>
                                        <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none">LPN ID</th>
                                        <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none">Location</th>
                                        <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none text-center">Qty</th>
                                        <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none text-center">Status</th>
                                        <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none text-right">Created</th>
                                    </tr>
                                </thead>
                                <tbody className="divide-y divide-corp-secondary/20">
                                    {lpns.map((lpn) => {
                                        const isSelected = selectedIds.includes(lpn.lpnId);
                                        const isLockedByTask = lpn.currentTaskId != null || lpn.status === 9; // Counting = 9
                                        const isAllocated = lpn.status === 4; // Allocated = 4
                                        const isDisabled = isLockedByTask || isAllocated;

                                        return (
                                            <tr key={lpn.lpnId} className={`transition-colors group ${isSelected ? 'bg-corp-accent/10' : 'hover:bg-corp-accent/5'} ${isDisabled ? 'opacity-60' : ''}`}>
                                                <td className="px-6 py-4">
                                                    <button
                                                        onClick={() => !isDisabled && toggleSelect(lpn.lpnId)}
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
                                                <td className="px-6 py-4">
                                                    <span className="font-mono text-sm font-bold text-white group-hover:text-corp-accent transition-colors">{lpn.lpnId}</span>
                                                </td>
                                                <td className="px-6 py-4">
                                                    <div className="flex flex-col">
                                                        <div className="flex items-center gap-1.5 font-bold text-slate-300 text-sm">
                                                            <MapPin className="w-3 h-3 text-emerald-500" />
                                                            {lpn.locationId}
                                                        </div>
                                                        <span className="text-[10px] font-black text-slate-500 uppercase tracking-tighter mt-0.5 ml-4">
                                                            {lpn.locationType}
                                                        </span>
                                                    </div>
                                                </td>
                                                <td className="px-6 py-4 text-center">
                                                    <div className="flex flex-col items-center">
                                                        <span className="font-black text-white">{lpn.quantity}</span>
                                                        {lpn.allocatedQuantity > 0 && (
                                                            <span className="text-[10px] font-bold text-amber-500">+{lpn.allocatedQuantity} Res.</span>
                                                        )}
                                                    </div>
                                                </td>
                                                <td className="px-6 py-4 text-center">
                                                    <span className={`px-2 py-0.5 rounded-md text-[10px] font-black uppercase border 
                                                        ${lpn.status === 9 ? 'bg-yellow-500/10 text-yellow-500 border-yellow-500/20' :
                                                            lpn.status === 4 ? 'bg-blue-500/10 text-blue-500 border-blue-500/20' :
                                                                lpn.status === 3 || lpn.status === 2 ? 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20' :
                                                                    'bg-slate-500/10 text-slate-500 border-slate-500/20'}`}>
                                                        {lpn.status === 9 ? 'Counting' :
                                                            lpn.status === 4 ? 'Allocated' :
                                                                lpn.status === 3 || lpn.status === 2 ? 'Available' : 'Other'}
                                                    </span>
                                                </td>
                                                <td className="px-6 py-4 text-right">
                                                    <div className="flex flex-col items-end">
                                                        <div className="flex items-center gap-1.5 text-xs text-slate-400 font-medium">
                                                            <Calendar className="w-3 h-3" />
                                                            {new Date(lpn.createdAt).toLocaleDateString()}
                                                        </div>
                                                        <div className="flex items-center gap-1.5 text-[10px] text-slate-500 font-bold uppercase tracking-wider">
                                                            <Clock className="w-3 h-3" />
                                                            {new Date(lpn.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                                                        </div>
                                                    </div>
                                                </td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>

                <BulkActionsBar
                    selectedCount={selectedIds.length}
                    onGenerateTask={handleGenerateTask}
                    onClear={() => setSelectedIds([])}
                    isProcessing={isGenerating}
                />
            </div>
        </div>
    );
};
