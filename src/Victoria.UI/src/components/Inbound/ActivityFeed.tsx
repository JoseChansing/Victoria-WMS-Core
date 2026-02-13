import React, { useState } from 'react';
import { Trash2, RotateCcw, AlertTriangle } from 'lucide-react';

interface ScannedLpn {
    id: string;
    sku: string;
    quantity: number;
    timestamp: Date | string; // Handle both for safety
    stationId: string;
}

interface ActivityFeedProps {
    items: ScannedLpn[];
    onVoid: (lpnId: string) => Promise<void>;
    isReadOnly?: boolean;
}

const ActivityFeed: React.FC<ActivityFeedProps> = ({ items, onVoid, isReadOnly }) => {
    const [voidingId, setVoidingId] = useState<string | null>(null);
    const [showConfirm, setShowConfirm] = useState<string | null>(null);

    const handleVoidClick = async (lpnId: string) => {
        setVoidingId(lpnId);
        try {
            await onVoid(lpnId);
            setShowConfirm(null);
        } catch (error) {
            console.error("Void failed", error);
        } finally {
            setVoidingId(null);
        }
    };

    return (
        <div className="bg-corp-nav/60 backdrop-blur-md rounded-3xl border border-corp-secondary/30 overflow-hidden flex flex-col h-full shadow-[0_8px_32px_rgba(0,0,0,0.3)]">
            <div className="p-4 border-b border-corp-secondary/20 bg-corp-accent/10 flex items-center justify-between">
                <h3 className="font-black text-white text-[11px] uppercase tracking-widest flex items-center gap-2">
                    <RotateCcw className="w-4 h-4 text-blue-400" />
                    Recent Activity
                </h3>
                <span className="text-[9px] font-black text-slate-500 uppercase px-2 py-0.5 bg-corp-base/50 rounded-full border border-corp-secondary/30">
                    Local Session
                </span>
            </div>

            <div className="flex-1 overflow-y-auto custom-scrollbar">
                {items.length === 0 ? (
                    <div className="p-8 text-center flex flex-col items-center justify-center h-full">
                        <div className="w-12 h-12 bg-corp-base/40 rounded-full flex items-center justify-center mb-3 border border-corp-secondary/20">
                            <RotateCcw className="w-6 h-6 text-slate-600" />
                        </div>
                        <p className="text-[10px] text-slate-500 font-bold uppercase tracking-wider">No recent scans</p>
                    </div>
                ) : (
                    <div className="divide-y divide-corp-secondary/10">
                        {items.slice(0, 10).map((item) => (
                            <div key={item.id} className="p-4 hover:bg-corp-accent/5 transition-colors group relative border-l-2 border-transparent hover:border-blue-500/50">
                                <div className="flex items-start justify-between gap-3">
                                    <div className="flex-1">
                                        <div className="flex items-center gap-2 mb-1.5">
                                            <span className="text-sm font-black text-white tracking-tight">{item.sku}</span>
                                            <span className="text-[9px] font-black px-1.5 py-0.5 bg-blue-500/10 text-blue-400 border border-blue-500/20 rounded-md uppercase">
                                                Qty: {item.quantity}
                                            </span>
                                        </div>
                                        <div className="flex items-center gap-2 text-[10px] font-bold text-slate-500 uppercase tracking-tighter">
                                            <span className="font-mono text-blue-600/60">#{item.id.substring(item.id.length - 8)}</span>
                                            <span>•</span>
                                            <span>{new Date(item.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                                            {item.stationId === 'PHOTO-STATION' && (
                                                <>
                                                    <span>•</span>
                                                    <span className="text-amber-500/80 font-black">Sample ✓</span>
                                                </>
                                            )}
                                        </div>
                                    </div>

                                    {!isReadOnly && (
                                        <button
                                            onClick={() => setShowConfirm(item.id)}
                                            className="p-2 text-slate-600 hover:text-red-400 hover:bg-red-400/10 rounded-xl transition-all opacity-0 group-hover:opacity-100 border border-transparent hover:border-red-400/20"
                                            title="Void LPN"
                                        >
                                            <Trash2 className="w-4 h-4" />
                                        </button>
                                    )}
                                </div>

                                {showConfirm === item.id && (
                                    <div className="mt-3 p-4 bg-red-950/20 rounded-2xl border border-red-500/20 animate-in fade-in slide-in-from-top-2 duration-200">
                                        <div className="flex gap-2 mb-4">
                                            <AlertTriangle className="w-4 h-4 text-red-500 shrink-0" />
                                            <p className="text-[10px] text-red-200/70 font-bold leading-relaxed uppercase tracking-wide">
                                                Void LPN <strong>{item.id}</strong>? This action will subtract units from the receipt.
                                            </p>
                                        </div>
                                        <div className="flex gap-2">
                                            <button
                                                onClick={() => handleVoidClick(item.id)}
                                                disabled={voidingId === item.id}
                                                className="flex-1 py-2 bg-red-600 hover:bg-red-500 text-white text-[10px] font-black rounded-lg shadow-lg shadow-red-900/20 transition-all disabled:opacity-50 uppercase tracking-widest"
                                            >
                                                {voidingId === item.id ? 'Voiding...' : 'Confirm Void'}
                                            </button>
                                            <button
                                                onClick={() => setShowConfirm(null)}
                                                className="px-4 py-2 bg-corp-base/60 border border-corp-secondary/30 text-slate-400 text-[10px] font-black rounded-lg hover:bg-corp-base transition-colors uppercase tracking-widest"
                                            >
                                                Cancel
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
};

export default ActivityFeed;
