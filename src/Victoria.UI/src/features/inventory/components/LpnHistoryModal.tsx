import React from 'react';
import { X, Clock, User, Info } from 'lucide-react';
import { useLpnHistory } from '../../../hooks/useInventory';
import { format } from 'date-fns';
import { es } from 'date-fns/locale';

interface LpnHistoryModalProps {
    lpnId: string;
    onClose: () => void;
}

export const LpnHistoryModal: React.FC<LpnHistoryModalProps> = ({ lpnId, onClose }) => {
    console.log("Rendering History Modal for LPN:", lpnId);
    const { data: history, isLoading } = useLpnHistory(lpnId);

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-300">
            <div className="bg-corp-nav border border-corp-secondary w-full max-w-2xl rounded-3xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
                {/* Header */}
                <div className="p-6 border-b border-corp-secondary/50 flex items-center justify-between bg-corp-accent/5">
                    <div className="flex items-center gap-3">
                        <div className="p-2.5 bg-corp-accent/10 rounded-xl border border-corp-accent/20">
                            <Clock className="w-5 h-5 text-corp-accent" />
                        </div>
                        <div>
                            <h3 className="text-xl font-black text-white tracking-tight">Historial de Trazabilidad</h3>
                            <p className="text-xs font-bold text-slate-500 uppercase tracking-widest leading-none">LPN ID: {lpnId}</p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 text-slate-400 hover:text-white hover:bg-white/10 rounded-xl transition-all"
                    >
                        <X className="w-6 h-6" />
                    </button>
                </div>

                {/* Content */}
                <div className="p-6 max-h-[60vh] overflow-y-auto custom-scrollbar">
                    {isLoading ? (
                        <div className="flex flex-col items-center justify-center py-12 gap-4">
                            <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-corp-accent"></div>
                            <p className="text-slate-500 font-bold animate-pulse">Cargando línea de tiempo...</p>
                        </div>
                    ) : history?.entries && history.entries.length > 0 ? (
                        <div className="relative space-y-8 before:absolute before:inset-0 before:ml-5 before:-translate-x-px before:h-full before:w-0.5 before:bg-gradient-to-b before:from-corp-accent/50 before:via-corp-secondary/30 before:to-transparent">
                            {history.entries.map((entry, idx) => (
                                <div key={idx} className="relative flex items-start gap-6 group">
                                    {/* Dot */}
                                    <div className="absolute left-0 w-10 flex justify-center mt-1">
                                        <div className="w-3 h-3 rounded-full bg-corp-accent ring-4 ring-corp-nav shadow-[0_0_10px_rgba(59,130,246,0.5)] group-hover:scale-125 transition-transform" />
                                    </div>

                                    {/* Content Card */}
                                    <div className="flex-1 bg-corp-base/50 border border-corp-secondary/30 rounded-2xl p-4 hover:border-corp-accent/30 transition-all shadow-lg hover:shadow-corp-accent/5">
                                        <div className="flex items-center justify-between mb-2">
                                            <span className="text-[10px] font-black text-corp-accent uppercase tracking-widest px-2 py-0.5 bg-corp-accent/10 rounded-md border border-corp-accent/20">
                                                {entry.eventType}
                                            </span>
                                            <span className="text-[10px] font-bold text-slate-500">
                                                {(() => {
                                                    try {
                                                        const date = new Date(entry.timestamp);
                                                        return isNaN(date.getTime())
                                                            ? 'Fecha inválida'
                                                            : format(date, "HH:mm:ss - d MMM yy", { locale: es });
                                                    } catch (e) {
                                                        console.error("Error formatting date:", e);
                                                        return "Error de fecha";
                                                    }
                                                })()}
                                            </span>
                                        </div>
                                        <p className="text-sm font-bold text-white mb-3">
                                            {entry.description}
                                        </p>
                                        <div className="flex items-center gap-2 text-xs font-medium text-slate-400">
                                            <User className="w-3.5 h-3.5" />
                                            <span>Usuario: <strong className="text-slate-300">{entry.user}</strong></span>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="text-center py-12">
                            <div className="p-4 bg-corp-secondary/20 rounded-full w-fit mx-auto mb-4">
                                <Info className="w-8 h-8 text-slate-600" />
                            </div>
                            <p className="text-slate-500 font-bold">No hay eventos registrados para este LPN.</p>
                        </div>
                    )}
                </div>

                {/* Footer */}
                <div className="p-4 bg-corp-base/50 border-t border-corp-secondary/50 flex justify-end">
                    <button
                        onClick={onClose}
                        className="px-6 py-2 bg-corp-accent text-white font-black text-sm rounded-xl hover:bg-blue-500 transition-all shadow-lg shadow-blue-900/20 uppercase tracking-wider"
                    >
                        Cerrar
                    </button>
                </div>
            </div>
        </div>
    );
};
