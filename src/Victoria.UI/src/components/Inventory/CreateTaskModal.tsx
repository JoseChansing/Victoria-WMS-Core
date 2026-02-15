import React, { useState } from 'react';
import { type CreateTaskDto, TaskPriority, TaskType } from '../../services/inventory';

interface CreateTaskModalProps {
    isOpen: boolean;
    onClose: () => void;
    onSubmit: (data: CreateTaskDto) => Promise<void>;
}

export const CreateTaskModal: React.FC<CreateTaskModalProps> = ({ isOpen, onClose, onSubmit }) => {
    const [locationCode, setLocationCode] = useState('');
    const [productSku, setProductSku] = useState('');
    const [type, setType] = useState<TaskType>(TaskType.CycleCount);
    const [priority, setPriority] = useState<TaskPriority>(TaskPriority.Normal);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    if (!isOpen) return null;

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsLoading(true);
        setError(null);
        try {
            await onSubmit({ locationCode, productSku, type, priority });
            onClose();
        } catch (err: any) {
            setError(err.message || "Failed to create task");
        } finally {
            setIsLoading(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm animate-in fade-in duration-300">
            <div className="bg-corp-base border border-corp-secondary rounded-3xl shadow-2xl w-full max-w-md p-0 overflow-hidden animate-in zoom-in-95 duration-300">
                <div className="px-6 py-4 border-b border-corp-secondary/50 flex justify-between items-center bg-corp-nav/30">
                    <h2 className="text-lg font-black text-white tracking-tight">New Inventory Task</h2>
                    <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors">
                        <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
                    </button>
                </div>

                <div className="p-6">
                    {error && <div className="mb-4 p-3 bg-red-500/10 border border-red-500/20 text-red-400 rounded-xl text-sm font-bold text-center">{error}</div>}

                    <form onSubmit={handleSubmit}>
                        <div className="mb-4">
                            <label className="block text-slate-400 text-xs font-bold uppercase tracking-widest mb-2">Task Type</label>
                            <select
                                value={type}
                                onChange={(e) => setType(Number(e.target.value) as TaskType)}
                                className="w-full px-4 py-3 bg-corp-base border border-corp-secondary rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all appearance-none"
                            >
                                <option value={TaskType.CycleCount}>Cycle Count</option>
                                <option value={TaskType.Putaway}>Putaway Check</option>
                                <option value={TaskType.Replenishment}>Replenishment</option>
                                <option value={TaskType.Investigation}>Investigation</option>
                            </select>
                        </div>

                        <div className="mb-4">
                            <label className="block text-slate-400 text-xs font-bold uppercase tracking-widest mb-2">Priority</label>
                            <select
                                value={priority}
                                onChange={(e) => setPriority(Number(e.target.value) as TaskPriority)}
                                className="w-full px-4 py-3 bg-corp-base border border-corp-secondary rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all appearance-none"
                            >
                                <option value={TaskPriority.Low}>Low</option>
                                <option value={TaskPriority.Normal}>Normal</option>
                                <option value={TaskPriority.High}>High</option>
                                <option value={TaskPriority.Critical}>Critical</option>
                            </select>
                        </div>

                        <div className="mb-4">
                            <label className="block text-slate-400 text-xs font-bold uppercase tracking-widest mb-2">Location</label>
                            <input
                                type="text"
                                value={locationCode}
                                onChange={(e) => setLocationCode(e.target.value.toUpperCase())}
                                placeholder="Ex: A-01-01"
                                required
                                className="w-full px-4 py-3 bg-corp-base border border-corp-secondary rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all placeholder:text-slate-600 font-mono"
                            />
                        </div>

                        <div className="mb-8">
                            <label className="block text-slate-400 text-xs font-bold uppercase tracking-widest mb-2">Product SKU (Optional)</label>
                            <input
                                type="text"
                                value={productSku}
                                onChange={(e) => setProductSku(e.target.value)}
                                placeholder="Ex: 12345"
                                className="w-full px-4 py-3 bg-corp-base border border-corp-secondary rounded-xl text-white focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all placeholder:text-slate-600 font-mono"
                            />
                        </div>

                        <div className="flex justify-end gap-3 pt-2 border-t border-corp-secondary/30">
                            <button
                                type="button"
                                onClick={onClose}
                                className="px-5 py-2.5 text-slate-400 hover:text-white hover:bg-white/5 rounded-xl font-bold transition-colors uppercase tracking-wide text-xs"
                            >
                                Cancel
                            </button>
                            <button
                                type="submit"
                                disabled={isLoading}
                                className="px-6 py-2.5 text-white bg-corp-accent hover:bg-blue-600 rounded-xl disabled:opacity-50 font-bold shadow-lg shadow-blue-900/20 transition-all uppercase tracking-wide text-xs"
                            >
                                {isLoading ? 'Creating...' : 'Create Task'}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    );
};
