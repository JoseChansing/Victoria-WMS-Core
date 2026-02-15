import React, { useState, useEffect } from 'react';
import { X, Beaker, MapPin, Package, Hash, CheckCircle2, AlertCircle } from 'lucide-react';
import { inventoryService, type InventoryTask } from '../../../services/inventory';
import { toast } from 'sonner';

interface BatchSampleModalProps {
    task: InventoryTask | null;
    isOpen: boolean;
    onClose: () => void;
    onComplete: () => void;
}

export const BatchSampleModal: React.FC<BatchSampleModalProps> = ({
    task,
    isOpen,
    onClose,
    onComplete
}) => {
    const [sampleQuantities, setSampleQuantities] = useState<Record<string, number>>({});
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        if (task && isOpen) {
            const initialQtys: Record<string, number> = {};
            task.lines.forEach(line => {
                initialQtys[line.id] = 1; // Default requirement: 1
            });
            setSampleQuantities(initialQtys);
        }
    }, [task, isOpen]);

    if (!isOpen || !task) return null;

    const handleSetAllToOne = () => {
        const resetQtys: Record<string, number> = {};
        task.lines.forEach(line => {
            resetQtys[line.id] = 1;
        });
        setSampleQuantities(resetQtys);
    };

    const handleQtyChange = (lineId: string, val: number, max: number) => {
        const sanitized = Math.max(0, Math.min(val, max));
        setSampleQuantities(prev => ({ ...prev, [lineId]: sanitized }));
    };

    const handleConfirmSamples = async () => {
        setSubmitting(true);
        try {
            const reportingPromises = task.lines.map(line => {
                const qtyToRemove = sampleQuantities[line.id] || 0;
                return inventoryService.reportLineCount(task.id, line.id, qtyToRemove);
            });

            await Promise.all(reportingPromises);
            toast.success("All samples processed successfully");
            onComplete();
            onClose();
        } catch (error) {
            console.error("Failed to process samples", error);
            toast.error("Some samples could not be processed");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-slate-950/80 backdrop-blur-sm" onClick={onClose} />

            <div className="relative w-full max-w-4xl bg-slate-900 border border-white/10 rounded-3xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300 flex flex-col max-h-[90vh]">
                {/* Header */}
                <div className="p-6 border-b border-white/5 bg-slate-800/50 flex items-center justify-between">
                    <div className="flex items-center gap-4">
                        <div className="p-3 bg-rose-500/10 rounded-2xl border border-rose-500/20">
                            <Beaker className="w-6 h-6 text-rose-500" />
                        </div>
                        <div>
                            <h3 className="text-xl font-black text-white tracking-tight uppercase">Quality Sampling</h3>
                            <p className="text-slate-400 text-xs font-bold tracking-widest uppercase">Batch Adjustment: {task.taskNumber}</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="p-2 text-slate-400 hover:text-white hover:bg-white/5 rounded-xl transition-all">
                        <X className="w-6 h-6" />
                    </button>
                </div>

                {/* Table */}
                <div className="flex-1 overflow-auto p-6 scrollbar-thin scrollbar-thumb-white/10">
                    <table className="w-full text-left border-separate border-spacing-y-2">
                        <thead>
                            <tr className="text-[10px] font-black text-slate-500 uppercase tracking-widest">
                                <th className="px-4 py-2">LPN / SKU</th>
                                <th className="px-4 py-2">Location</th>
                                <th className="px-4 py-2 text-center">Available</th>
                                <th className="px-4 py-2 text-center min-w-[150px]">
                                    <div className="flex flex-col items-center gap-1">
                                        <span>Sample Qty</span>
                                        <button
                                            onClick={handleSetAllToOne}
                                            className="text-[9px] text-rose-400 hover:text-rose-300 transition-colors border-b border-rose-400/30 font-bold"
                                        >
                                            SET ALL TO 1
                                        </button>
                                    </div>
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {task.lines.map(line => (
                                <tr key={line.id} className="bg-white/5 hover:bg-white/[0.07] transition-colors rounded-xl overflow-hidden group">
                                    <td className="px-4 py-4 rounded-l-xl">
                                        <div className="flex items-center gap-3">
                                            <div className="w-8 h-8 bg-slate-800 rounded-lg flex items-center justify-center border border-white/5">
                                                <Package className="w-4 h-4 text-slate-500" />
                                            </div>
                                            <span className="font-bold text-white text-sm">{line.targetId}</span>
                                        </div>
                                    </td>
                                    <td className="px-4 py-4">
                                        <div className="flex items-center gap-2 text-slate-400 text-sm">
                                            <MapPin className="w-3.5 h-3.5" />
                                            <span className="font-medium">{line.targetDescription.split(' en ')[1] || 'N/A'}</span>
                                        </div>
                                    </td>
                                    <td className="px-4 py-4 text-center">
                                        <span className="bg-slate-800 px-3 py-1 rounded-lg border border-white/5 text-slate-300 font-bold text-sm">
                                            {line.expectedQty}
                                        </span>
                                    </td>
                                    <td className="px-4 py-4 rounded-r-xl">
                                        <div className="flex items-center justify-center gap-2">
                                            <input
                                                type="number"
                                                value={sampleQuantities[line.id] || 0}
                                                onChange={(e) => handleQtyChange(line.id, parseInt(e.target.value) || 0, line.expectedQty)}
                                                className="w-24 bg-slate-950 border border-white/10 rounded-lg py-1.5 px-3 text-center text-white font-black focus:ring-2 focus:ring-rose-500/50 outline-none transition-all"
                                            />
                                            {sampleQuantities[line.id] > 0 && sampleQuantities[line.id] >= line.expectedQty && (
                                                <div className="flex items-center gap-1 text-[9px] text-rose-500 font-black uppercase">
                                                    <AlertCircle className="w-3 h-3" />
                                                    Full Consume
                                                </div>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {/* Footer */}
                <div className="p-6 border-t border-white/5 bg-slate-800/30 flex items-center justify-between">
                    <div className="flex items-center gap-2 text-slate-500">
                        <Hash className="w-4 h-4" />
                        <span className="text-xs font-bold uppercase tracking-widest">{task.lines.length} Items to Sample</span>
                    </div>
                    <div className="flex items-center gap-3">
                        <button
                            onClick={onClose}
                            className="px-6 py-2.5 text-slate-400 hover:text-white font-bold transition-all"
                        >
                            Cancel
                        </button>
                        <button
                            onClick={handleConfirmSamples}
                            disabled={submitting}
                            className="px-8 py-2.5 bg-rose-600 hover:bg-rose-500 text-white rounded-xl font-black shadow-lg shadow-rose-900/20 transition-all active:scale-95 disabled:opacity-50 disabled:pointer-events-none flex items-center gap-2"
                        >
                            {submitting ? (
                                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                            ) : (
                                <>
                                    <CheckCircle2 className="w-5 h-5" />
                                    <span>Confirm Samples</span>
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};
