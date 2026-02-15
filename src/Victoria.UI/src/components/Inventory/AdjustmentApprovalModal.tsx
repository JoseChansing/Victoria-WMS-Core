import React, { useState } from 'react';
import { type InventoryTask } from '../../services/inventory';

interface AdjustmentApprovalModalProps {
    task: InventoryTask | null;
    isOpen: boolean;
    onClose: () => void;
    onApprove: (taskId: string) => Promise<void>;
    onReject: (taskId: string, reason: string) => Promise<void>;
}

export const AdjustmentApprovalModal: React.FC<AdjustmentApprovalModalProps> = ({
    task, isOpen, onClose, onApprove, onReject
}) => {
    const [reason, setReason] = useState('');
    const [isProcessing, setIsProcessing] = useState(false);
    const [actionType, setActionType] = useState<'approve' | 'reject' | null>(null);

    if (!isOpen || !task) return null;

    const diff = task.countedQuantity - task.expectedQuantity;
    const isLoss = diff < 0;

    const handleApprove = async () => {
        setIsProcessing(true);
        setActionType('approve');
        try {
            await onApprove(task.id);
            onClose();
        } catch (error) {
            console.error(error);
            alert("Error approving task");
        } finally {
            setIsProcessing(false);
            setActionType(null);
        }
    };

    const handleReject = async () => {
        setIsProcessing(true);
        setActionType('reject');
        try {
            await onReject(task.id, reason || "Rechazado por supervisor");
            onClose();
        } catch (error) {
            console.error(error);
            alert("Error rejecting task");
        } finally {
            setIsProcessing(false);
            setActionType(null);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm animate-in fade-in duration-300">
            <div className="bg-corp-base border border-corp-secondary w-full max-w-lg rounded-3xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
                {/* Header */}
                <div className="bg-amber-500/10 px-6 py-4 border-b border-amber-500/20 flex justify-between items-center">
                    <div className="flex items-center gap-3">
                        <div className="bg-amber-500/20 p-2 rounded-xl text-amber-500 border border-amber-500/30">
                            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                        </div>
                        <h2 className="text-lg font-black text-white tracking-tight">Discrepancy Resolution</h2>
                    </div>
                    <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors">✕</button>
                </div>

                <div className="p-6">
                    <div className="mb-6">
                        <div className="flex justify-between text-sm text-slate-400 mb-2 font-medium">
                            <span>Location: <strong className="text-white bg-white/10 px-2 py-0.5 rounded">{task.locationCode}</strong></span>
                            <span>SKU: <strong className="text-corp-accent">{task.productSku}</strong></span>
                        </div>
                        <div className="bg-corp-nav/50 rounded-2xl p-4 grid grid-cols-3 gap-4 text-center border border-corp-secondary/50">
                            <div>
                                <p className="text-[10px] text-slate-500 uppercase font-black tracking-widest">System</p>
                                <p className="text-xl font-black text-white">{task.expectedQuantity}</p>
                            </div>
                            <div className="border-x border-corp-secondary/30">
                                <p className="text-[10px] text-slate-500 uppercase font-black tracking-widest">Counted</p>
                                <p className="text-xl font-black text-corp-accent">{task.countedQuantity}</p>
                            </div>
                            <div>
                                <p className="text-[10px] text-slate-500 uppercase font-black tracking-widest">Variance</p>
                                <p className={`text-xl font-black ${isLoss ? 'text-red-500' : 'text-emerald-500'}`}>
                                    {diff > 0 ? '+' : ''}{diff}
                                </p>
                            </div>
                        </div>
                    </div>

                    <div className="mb-4">
                        <label className="block text-xs font-bold text-slate-400 uppercase tracking-widest mb-2">Rejection Reason (Optional)</label>
                        <textarea
                            className="w-full bg-corp-base border border-corp-secondary rounded-xl p-3 text-sm text-white focus:ring-2 focus:ring-amber-500 outline-none placeholder:text-slate-600 transition-all"
                            rows={2}
                            placeholder="Ex: Suspicious count, please verify..."
                            value={reason}
                            onChange={(e) => setReason(e.target.value)}
                        />
                    </div>

                    <div className="flex gap-3 mt-8">
                        <button
                            onClick={handleReject}
                            disabled={isProcessing}
                            className="flex-1 px-4 py-2.5 border border-corp-secondary text-slate-300 rounded-xl hover:bg-white/5 font-bold transition-all uppercase tracking-wide text-xs"
                        >
                            {isProcessing && actionType === 'reject' ? 'Processing...' : 'Reject (Re-count)'}
                        </button>
                        <button
                            onClick={handleApprove}
                            disabled={isProcessing}
                            className="flex-1 px-4 py-2.5 bg-emerald-600 text-white rounded-xl hover:bg-emerald-500 font-bold shadow-lg shadow-emerald-900/20 transition-all uppercase tracking-wide text-xs"
                        >
                            {isProcessing && actionType === 'approve' ? 'Approving...' : 'Confirm Adjustment'}
                        </button>
                    </div>
                    <p className="text-xs text-center text-gray-400 mt-4">
                        Upon confirmation, adjustment will be sent to Odoo for final review.
                    </p>
                </div>
            </div>
        </div>
    );
};
