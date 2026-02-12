import React from 'react';
import { AlertCircle, Save, Package, RefreshCw, CheckCircle2 } from 'lucide-react';

interface PackagingUpdateModalProps {
    isOpen: boolean;
    onClose: () => void;
    packagingName: string;
    currentData: {
        qty: number;
        weight: number;
        length: number;
        width: number;
        height: number;
    };
    newData: {
        qty: number;
        weight: number;
        length: number;
        width: number;
        height: number;
    };
    onConfirm: (action: 'receive_only' | 'update_odoo' | 'create_new') => void;
}

export const PackagingUpdateModal: React.FC<PackagingUpdateModalProps> = ({
    isOpen, onClose, packagingName, currentData, newData, onConfirm
}) => {
    if (!isOpen) return null;

    const hasDifferences = (
        currentData.qty !== newData.qty ||
        currentData.weight !== newData.weight ||
        currentData.length !== newData.length ||
        currentData.width !== newData.width ||
        currentData.height !== newData.height
    );

    if (!hasDifferences) {
        // This shouldn't happen if called correctly, but safety first
        onConfirm('receive_only');
        return null;
    }

    return (
        <div className="fixed inset-0 z-[110] flex items-center justify-center p-4 bg-black/80 backdrop-blur-md animate-in fade-in duration-300">
            <div className="bg-corp-nav border-2 border-corp-accent/50 rounded-[2.5rem] shadow-[0_0_80px_rgba(59,130,246,0.15)] max-w-2xl w-full overflow-hidden flex flex-col">
                <div className="p-8 border-b border-corp-secondary/30 bg-corp-base/30 text-center">
                    <div className="p-4 bg-corp-accent/20 rounded-full w-fit mx-auto mb-4 border border-corp-accent/30">
                        <RefreshCw className="w-8 h-8 text-corp-accent animate-spin-slow" />
                    </div>
                    <h2 className="text-3xl font-black text-white tracking-tight uppercase mb-2">Diferencia Detectada</h2>
                    <p className="text-slate-400 font-bold uppercase tracking-widest text-[10px]">OPERACIÓN VS ESTÁNDAR ODOO</p>
                </div>

                <div className="p-8 space-y-8">
                    <div className="bg-slate-900/60 rounded-3xl p-6 border border-corp-secondary/30">
                        <div className="flex items-center gap-3 mb-6">
                            <Package className="w-5 h-5 text-indigo-400" />
                            <span className="text-sm font-black text-white uppercase tracking-wider">{packagingName}</span>
                        </div>

                        <div className="grid grid-cols-5 gap-4">
                            <ComparisonItem label="CANT x BULT" oldVal={currentData.qty} newVal={newData.qty} />
                            <ComparisonItem label="PESO (KG)" oldVal={currentData.weight} newVal={newData.weight} />
                            <ComparisonItem label="LARGO" oldVal={currentData.length} newVal={newData.length} />
                            <ComparisonItem label="ANCHO" oldVal={currentData.width} newVal={newData.width} />
                            <ComparisonItem label="ALTO" oldVal={currentData.height} newVal={newData.height} />
                        </div>
                    </div>

                    <div className="grid grid-cols-1 gap-4">
                        <button
                            onClick={() => onConfirm('update_odoo')}
                            className="group flex items-center justify-between p-5 bg-corp-accent hover:bg-corp-accent/80 text-white rounded-[1.5rem] transition-all transform active:scale-[0.98] shadow-lg shadow-corp-accent/20"
                        >
                            <div className="flex items-center gap-4">
                                <div className="p-2 bg-white/20 rounded-xl">
                                    <Save className="w-6 h-6" />
                                </div>
                                <div className="text-left">
                                    <p className="font-black text-lg uppercase tracking-tight">Actualizar Odoo</p>
                                    <p className="text-[10px] opacity-80 font-bold uppercase tracking-widest">Normaliza el producto en Odoo con estos valores</p>
                                </div>
                            </div>
                            <CheckCircle2 className="w-6 h-6 opacity-0 group-hover:opacity-100 transition-opacity" />
                        </button>

                        <button
                            onClick={() => onConfirm('create_new')}
                            className="group flex items-center justify-between p-5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-[1.5rem] transition-all transform active:scale-[0.98] shadow-lg shadow-indigo-600/20"
                        >
                            <div className="flex items-center gap-4">
                                <div className="p-2 bg-white/20 rounded-xl">
                                    <Package className="w-6 h-6" />
                                </div>
                                <div className="text-left">
                                    <p className="font-black text-lg uppercase tracking-tight">Crear Nuevo Empaque</p>
                                    <p className="text-[10px] opacity-80 font-bold uppercase tracking-widest">Registra una nueva variante de empaque en Odoo</p>
                                </div>
                            </div>
                            <CheckCircle2 className="w-6 h-6 opacity-0 group-hover:opacity-100 transition-opacity" />
                        </button>

                        <button
                            onClick={() => onConfirm('receive_only')}
                            className="group flex items-center justify-between p-5 bg-corp-base/50 hover:bg-corp-base border border-corp-secondary text-slate-400 hover:text-white rounded-[1.5rem] transition-all"
                        >
                            <div className="flex items-center gap-4">
                                <div className="p-2 bg-slate-800 rounded-xl group-hover:bg-slate-700 transition-colors">
                                    <AlertCircle className="w-6 h-6" />
                                </div>
                                <div className="text-left">
                                    <p className="font-black text-lg uppercase tracking-tight">Solo Recibir</p>
                                    <p className="text-[10px] font-bold uppercase tracking-widest">Proceder sin actualizar Odoo (Solo este ingreso)</p>
                                </div>
                            </div>
                        </button>
                    </div>
                </div>

                <div className="p-4 bg-corp-base/50 border-t border-corp-secondary/30 text-center">
                    <button onClick={onClose} className="text-[10px] font-black text-slate-500 hover:text-slate-300 uppercase tracking-[0.2em] transition-colors">
                        CANCELAR Y REVISAR DATOS
                    </button>
                </div>
            </div>
        </div>
    );
};

const ComparisonItem = ({ label, oldVal, newVal }: { label: string, oldVal: number, newVal: number }) => {
    const isDifferent = oldVal !== newVal;
    return (
        <div className="flex flex-col items-center">
            <span className="text-[8px] font-black text-slate-500 uppercase tracking-tighter mb-1 font-mono">{label}</span>
            <div className={`flex flex-col items-center p-2 rounded-xl w-full ${isDifferent ? 'bg-amber-500/10 border border-amber-500/30' : 'bg-slate-800/40 border border-slate-700/50'}`}>
                <span className="text-[8px] font-bold text-slate-600 line-through mb-0.5">{oldVal}</span>
                <span className={`text-xs font-black ${isDifferent ? 'text-amber-400' : 'text-slate-400'}`}>{newVal}</span>
            </div>
        </div>
    );
};
