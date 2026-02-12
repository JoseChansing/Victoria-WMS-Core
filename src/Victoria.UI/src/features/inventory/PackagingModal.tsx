import React, { useState } from 'react';
import { X, Plus, Trash2, Save, Package } from 'lucide-react';
import api from '../../api/axiosConfig';

interface Packaging {
    odooId: number;
    name: string;
    qty: number;
    weight: number;
    length: number;
    width: number;
    height: number;
}

interface PackagingModalProps {
    sku: string;
    packagings: Packaging[];
    isOpen: boolean;
    onClose: () => void;
    onRefresh: () => void;
}

export const PackagingModal: React.FC<PackagingModalProps> = ({ sku, packagings, isOpen, onClose, onRefresh }) => {
    const [editingPkg, setEditingPkg] = useState<Partial<Packaging> | null>(null);
    const [isSaving, setIsSaving] = useState(false);

    if (!isOpen) return null;

    const handleSave = async (pkg: Partial<Packaging>) => {
        setIsSaving(true);
        try {
            if (pkg.odooId) {
                await api.put(`products/${sku}/packaging/${pkg.odooId}`, pkg);
            } else {
                await api.post(`products/${sku}/packaging`, pkg);
            }
            onRefresh();
            setEditingPkg(null);
        } catch (error) {
            console.error('Error saving packaging:', error);
            alert('Error al guardar el empaque.');
        } finally {
            setIsSaving(false);
        }
    };

    const handleDelete = async (odooId: number) => {
        if (!window.confirm('¿Estás seguro de eliminar este bulto en Odoo?')) return;
        try {
            await api.delete(`products/${sku}/packaging/${odooId}`);
            onRefresh();
        } catch (error) {
            console.error('Error deleting packaging:', error);
            alert('Error al eliminar el empaque.');
        }
    };

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-300">
            <div className="bg-corp-nav border border-corp-secondary w-full max-w-2xl rounded-3xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
                <div className="p-6 border-b border-corp-secondary/50 flex items-center justify-between bg-corp-base/30">
                    <div className="flex items-center gap-3">
                        <div className="p-2 bg-indigo-500/20 rounded-xl border border-indigo-500/30">
                            <Package className="w-5 h-5 text-indigo-400" />
                        </div>
                        <div>
                            <h2 className="text-xl font-black text-white leading-none">Bultos / Empaque</h2>
                            <p className="text-xs text-slate-500 mt-1 font-mono uppercase tracking-widest">{sku}</p>
                        </div>
                    </div>
                    <button onClick={onClose} className="p-2 hover:bg-white/10 rounded-full transition-colors text-slate-400 hover:text-white">
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="flex-1 overflow-auto p-6 space-y-4 no-scrollbar">
                    {packagings.length === 0 && !editingPkg && (
                        <div className="text-center py-12 bg-corp-base/20 rounded-2xl border border-dashed border-corp-secondary/30">
                            <Package className="w-12 h-12 text-slate-700 mx-auto mb-3 opacity-20" />
                            <p className="text-slate-500 italic text-sm">Este producto no tiene bultos configurados en Odoo.</p>
                        </div>
                    )}

                    <div className="grid gap-3">
                        {packagings.map((pkg) => (
                            <div key={pkg.odooId} className="group p-4 bg-corp-base/40 border border-corp-secondary/50 rounded-2xl hover:border-corp-accent/50 transition-all">
                                <div className="flex items-center justify-between mb-3">
                                    <span className="text-sm font-bold text-white">{pkg.name}</span>
                                    <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                                        <button onClick={() => setEditingPkg(pkg)} className="p-1.5 text-blue-400 hover:bg-blue-400/10 rounded-lg">
                                            <Save className="w-4 h-4" />
                                        </button>
                                        <button onClick={() => handleDelete(pkg.odooId)} className="p-1.5 text-rose-400 hover:bg-rose-400/10 rounded-lg">
                                            <Trash2 className="w-4 h-4" />
                                        </button>
                                    </div>
                                </div>
                                <div className="grid grid-cols-5 gap-2 text-[10px] font-bold uppercase tracking-wider">
                                    <div className="flex flex-col">
                                        <span className="text-slate-500 mb-0.5">Cantidad</span>
                                        <span className="text-indigo-400 text-xs">{pkg.qty} pz</span>
                                    </div>
                                    <div className="flex flex-col">
                                        <span className="text-slate-500 mb-0.5">Peso</span>
                                        <span className="text-emerald-400 text-xs">{pkg.weight} kg</span>
                                    </div>
                                    <div className="flex flex-col">
                                        <span className="text-slate-500 mb-0.5">Largo</span>
                                        <span className="text-slate-300 text-xs">{pkg.length} cm</span>
                                    </div>
                                    <div className="flex flex-col">
                                        <span className="text-slate-500 mb-0.5">Ancho</span>
                                        <span className="text-slate-300 text-xs">{pkg.width} cm</span>
                                    </div>
                                    <div className="flex flex-col">
                                        <span className="text-slate-500 mb-0.5">Alto</span>
                                        <span className="text-slate-300 text-xs">{pkg.height} cm</span>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>

                    {editingPkg && (
                        <div className="p-6 bg-corp-accent/10 border border-corp-accent/30 rounded-2xl space-y-4 animate-in slide-in-from-bottom-2 duration-300">
                            <h3 className="text-sm font-black text-corp-accent uppercase tracking-widest flex items-center gap-2">
                                <Plus className="w-4 h-4" /> {editingPkg.odooId ? 'Editar Bulto' : 'Nuevo Bulto'}
                            </h3>
                            <div className="grid grid-cols-2 gap-4">
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Nombre</label>
                                    <input
                                        type="text"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-4 py-2 text-sm text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.name || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, name: e.target.value })}
                                        placeholder="Ej: Caja x12"
                                    />
                                </div>
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Cant. p/ Bulto</label>
                                    <input
                                        type="number"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-4 py-2 text-sm text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.qty || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, qty: Number(e.target.value) })}
                                    />
                                </div>
                            </div>
                            <div className="grid grid-cols-4 gap-3">
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Peso (kg)</label>
                                    <input
                                        type="number"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-3 py-2 text-xs text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.weight || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, weight: Number(e.target.value) })}
                                    />
                                </div>
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Largo (cm)</label>
                                    <input
                                        type="number"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-3 py-2 text-xs text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.length || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, length: Number(e.target.value) })}
                                    />
                                </div>
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Ancho (cm)</label>
                                    <input
                                        type="number"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-3 py-2 text-xs text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.width || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, width: Number(e.target.value) })}
                                    />
                                </div>
                                <div className="space-y-1.5">
                                    <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Alto (cm)</label>
                                    <input
                                        type="number"
                                        className="w-full bg-corp-base/50 border border-corp-secondary/50 rounded-xl px-3 py-2 text-xs text-white focus:ring-2 focus:ring-corp-accent outline-none"
                                        value={editingPkg.height || ''}
                                        onChange={(e) => setEditingPkg({ ...editingPkg, height: Number(e.target.value) })}
                                    />
                                </div>
                            </div>
                            <div className="flex gap-2 pt-2">
                                <button
                                    onClick={() => handleSave(editingPkg)}
                                    disabled={isSaving}
                                    className="flex-1 bg-corp-accent hover:bg-corp-accent/80 text-white font-bold py-2 rounded-xl transition-all disabled:opacity-50 flex items-center justify-center gap-2"
                                >
                                    {isSaving ? <Package className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                                    Guardar
                                </button>
                                <button
                                    onClick={() => setEditingPkg(null)}
                                    className="flex-1 bg-corp-base/50 text-slate-400 font-bold py-2 rounded-xl hover:bg-corp-base hover:text-white transition-all border border-corp-secondary/50"
                                >
                                    Cancelar
                                </button>
                            </div>
                        </div>
                    )}
                </div>

                {!editingPkg && (
                    <div className="p-6 border-t border-corp-secondary/50 bg-corp-base/30">
                        <button
                            onClick={() => setEditingPkg({ name: '', qty: 1, weight: 0, length: 0, width: 0, height: 0 })}
                            className="w-full bg-indigo-600 hover:bg-indigo-500 text-white font-black py-3 rounded-2xl transition-all flex items-center justify-center gap-2 shadow-lg shadow-indigo-600/20 active:scale-[0.98]"
                        >
                            <Plus className="w-5 h-5" /> AGREGAR NUEVO BULTO
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};
