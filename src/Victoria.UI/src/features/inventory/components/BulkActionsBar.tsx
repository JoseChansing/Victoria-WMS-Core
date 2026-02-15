import React, { useState, useRef, useEffect } from 'react';
import { CheckSquare, X, ChevronUp, Box, Repeat, Search, Beaker } from 'lucide-react';

interface BulkActionsBarProps {
    selectedCount: number;
    onGenerateTask: (taskType: string) => void;
    onClear: () => void;
    isProcessing?: boolean;
}

const TASK_OPTIONS = [
    { id: 'CycleCount', label: 'Cycle Count', icon: CheckSquare, color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
    { id: 'Putaway', label: 'Putaway Check', icon: Box, color: 'text-blue-400', bg: 'bg-blue-500/10' },
    { id: 'Replenishment', label: 'Replenishment', icon: Repeat, color: 'text-purple-400', bg: 'bg-purple-500/10' },
    { id: 'Investigation', label: 'Investigation', icon: Search, color: 'text-amber-400', bg: 'bg-amber-500/10' },
    { id: 'TakeSample', label: 'Take Sample', icon: Beaker, color: 'text-rose-400', bg: 'bg-rose-500/10' },
];

export const BulkActionsBar: React.FC<BulkActionsBarProps> = ({
    selectedCount,
    onGenerateTask,
    onClear,
    isProcessing = false
}) => {
    const [isMenuOpen, setIsMenuOpen] = useState(false);
    const menuRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
                setIsMenuOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    if (selectedCount === 0) return null;

    return (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 animate-in slide-in-from-bottom-8 duration-500 w-full max-w-4xl px-4">
            <div className="bg-slate-900/90 backdrop-blur-xl border border-white/10 shadow-[0_20px_50px_rgba(0,0,0,0.5)] rounded-2xl p-4 flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <div className="bg-corp-accent/20 p-2.5 rounded-xl border border-corp-accent/30 shadow-inner">
                        <CheckSquare className="w-5 h-5 text-corp-accent" />
                    </div>
                    <div>
                        <div className="flex items-center gap-2">
                            <span className="text-white font-black text-xl leading-none">{selectedCount}</span>
                            <span className="text-slate-400 font-bold text-sm uppercase tracking-wider">Selected</span>
                        </div>
                        <p className="text-[10px] text-slate-500 font-black uppercase tracking-widest mt-0.5">Bulk Inventory Action</p>
                    </div>
                </div>

                <div className="flex items-center gap-3">
                    <button
                        onClick={onClear}
                        disabled={isProcessing}
                        className="px-4 py-2 text-slate-400 hover:text-white hover:bg-white/5 rounded-xl font-bold transition-all flex items-center gap-2 text-sm"
                    >
                        <X className="w-4 h-4" />
                        Cancel
                    </button>

                    <div className="relative" ref={menuRef}>
                        <button
                            onClick={() => setIsMenuOpen(!isMenuOpen)}
                            disabled={isProcessing}
                            className="px-6 py-2.5 bg-corp-accent hover:bg-corp-accent/80 text-white rounded-xl font-black shadow-lg shadow-corp-accent/20 transition-all hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-3 text-sm"
                        >
                            {isProcessing ? (
                                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                            ) : (
                                <>
                                    <span>Generate Task</span>
                                    <ChevronUp className={`w-4 h-4 transition-transform duration-300 ${isMenuOpen ? 'rotate-180' : ''}`} />
                                </>
                            )}
                        </button>

                        {isMenuOpen && (
                            <div className="absolute bottom-full right-0 mb-3 w-64 bg-slate-800 border border-white/10 rounded-2xl shadow-2xl overflow-hidden animate-in fade-in slide-in-from-bottom-2 duration-200">
                                <div className="p-2 grid gap-1">
                                    {TASK_OPTIONS.map((option) => (
                                        <button
                                            key={option.id}
                                            onClick={() => {
                                                if (isProcessing) return;
                                                onGenerateTask(option.id);
                                                setIsMenuOpen(false);
                                            }}
                                            disabled={isProcessing}
                                            className="w-full flex items-center gap-3 p-3 hover:bg-white/5 rounded-xl transition-all group text-left disabled:opacity-50"
                                        >
                                            <div className={`${option.bg} p-2 rounded-lg border border-white/5 group-hover:border-white/10 transition-colors`}>
                                                <option.icon className={`w-4 h-4 ${option.color}`} />
                                            </div>
                                            <div>
                                                <p className="text-sm font-bold text-white leading-none">{option.label}</p>
                                                <p className="text-[10px] text-slate-500 font-medium mt-1">Create batch {option.label.toLowerCase()} tasks</p>
                                            </div>
                                        </button>
                                    ))}
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};
