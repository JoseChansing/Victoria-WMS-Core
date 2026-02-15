import { useState, useEffect } from 'react';
import {
    MapPin,
    Plus,
    Download,
    Upload,
    Search,
    Layers,
    BarChart3,
    Trash2,
    Edit,
    CheckCircle2,
    X,
    FileSpreadsheet,
    Package
} from 'lucide-react';
import api from '../../api/axiosConfig';

interface Location {
    value: string;
    zone: string;
    profile: 'Reserve' | 'Picking';
    status: string;
    isPickable: boolean;
    pickingSequence: number;
    maxWeight: number;
    maxVolume: number;
    currentWeight: number;
    currentVolume: number;
    barcode: string;
    occupancyStatus: 'Empty' | 'Partial' | 'Full';
    lpnCount: number;
}

export const LocationMaster = () => {
    const [locations, setLocations] = useState<Location[]>([]);
    const [loading, setLoading] = useState(true);
    const [search, setSearch] = useState('');
    const [showModal, setShowModal] = useState(false);
    const [showImportModal, setShowImportModal] = useState(false);
    const [importData, setImportData] = useState('');
    const [editingLocation, setEditingLocation] = useState<Partial<Location> | null>(null);

    const fetchData = async () => {
        setLoading(true);
        try {
            const { data } = await api.get('/locations');
            setLocations(data);
        } catch (error) {
            console.error('Error fetching locations:', error);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, []);

    const filteredLocations = locations.filter(loc =>
        loc.value?.toLowerCase().includes(search.toLowerCase()) ||
        loc.zone?.toLowerCase().includes(search.toLowerCase()) ||
        loc.barcode?.toLowerCase().includes(search.toLowerCase())
    );

    const handleImport = async () => {
        try {
            const json = JSON.parse(importData);
            await api.post('/locations/import', json);
            setShowImportModal(false);
            setImportData('');
            fetchData();
        } catch (error) {
            alert('Error al importar. Verifique el formato JSON.');
        }
    };

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            if (editingLocation?.status) { // Edit mode (simplified check)
                // If value exists in the object, we use it for the URL
                const code = editingLocation.value;
                await api.put(`/locations/${code}`, {
                    pickingSequence: Number(editingLocation.pickingSequence),
                    maxWeight: Number(editingLocation.maxWeight),
                    maxVolume: Number(editingLocation.maxVolume),
                    barcode: editingLocation.barcode
                });
            } else {
                // Create mode
                await api.post('/locations', {
                    code: editingLocation?.value,
                    profile: editingLocation?.profile || 'Picking',
                    isPickable: editingLocation?.isPickable ?? true,
                    pickingSequence: Number(editingLocation?.pickingSequence || 0),
                    maxWeight: Number(editingLocation?.maxWeight || 0),
                    maxVolume: Number(editingLocation?.maxVolume || 0),
                    barcode: editingLocation?.barcode || editingLocation?.value
                });
            }
            setShowModal(false);
            setEditingLocation(null);
            fetchData();
        } catch (error: any) {
            alert(error.response?.data?.Error || 'Error al guardar');
        }
    };

    const handleEdit = (loc: Location) => {
        setEditingLocation(loc);
        setShowModal(true);
    };

    const handleDelete = async (code: string) => {
        if (!window.confirm(`¿Seguro que desea eliminar la ubicación ${code}?`)) return;
        try {
            await api.delete(`/locations/${code}`);
            fetchData();
        } catch (error: any) {
            alert(error.response?.data?.error || 'Error al eliminar');
        }
    };

    const exportToCsv = () => {
        const headers = ['Código', 'Zona', 'Perfil', 'Pickable', 'Secuencia', 'Peso Máx', 'Volumen Máx', 'Código de Barras', 'Ocupación'];
        const rows = filteredLocations.map(l => [
            l.value, l.zone, l.profile, l.isPickable, l.pickingSequence, l.maxWeight, l.maxVolume, l.barcode, l.occupancyStatus
        ]);

        const csvContent = "data:text/csv;charset=utf-8,"
            + headers.join(",") + "\n"
            + rows.map(e => e.join(",")).join("\n");

        const encodedUri = encodeURI(csvContent);
        const link = document.createElement("a");
        link.setAttribute("href", encodedUri);
        link.setAttribute("download", "ubicactions_export.csv");
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };

    return (
        <div className="space-y-6 animate-in fade-in duration-500">
            {/* Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h2 className="text-2xl font-black text-white tracking-tight flex items-center gap-2">
                        <MapPin className="text-corp-accent w-8 h-8" />
                        Maestro de Ubicaciones
                    </h2>
                    <p className="text-slate-400 font-medium">Gestión total de infraestructura y capacidad del almacén</p>
                </div>
                <div className="flex items-center gap-3">
                    <button
                        onClick={() => setShowImportModal(true)}
                        className="flex items-center gap-2 px-4 py-2.5 bg-corp-nav/40 border border-corp-secondary text-slate-300 rounded-xl font-bold hover:bg-corp-accent/40 hover:text-white transition-all text-sm shadow-sm"
                    >
                        <Upload className="w-4 h-4" />
                        Importar Excel
                    </button>
                    <button
                        onClick={exportToCsv}
                        className="flex items-center gap-2 px-4 py-2.5 bg-corp-nav/40 border border-corp-secondary text-slate-300 rounded-xl font-bold hover:bg-corp-accent/40 hover:text-white transition-all text-sm shadow-sm"
                    >
                        <Download className="w-4 h-4" />
                        Exportar
                    </button>
                    <button
                        onClick={() => { setEditingLocation({}); setShowModal(true); }}
                        className="flex items-center gap-2 px-5 py-2.5 bg-corp-accent text-white rounded-xl font-black hover:bg-corp-accent/80 transition-all text-sm shadow-lg shadow-black/20"
                    >
                        <Plus className="w-4 h-4" />
                        Nueva Ubicación
                    </button>
                </div>
            </div>

            {/* Stats Summary */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div className="bg-corp-nav/40 p-5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10">
                    <div className="flex items-center justify-between mb-2">
                        <div className="p-2 bg-corp-accent/30 rounded-lg"><MapPin className="w-5 h-5 text-blue-300" /></div>
                        <span className="text-xs font-black text-slate-400 uppercase">Total</span>
                    </div>
                    <p className="text-2xl font-black text-white">{locations.length}</p>
                    <p className="text-xs text-slate-500 font-medium">Ubicaciones activas</p>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10">
                    <div className="flex items-center justify-between mb-2">
                        <div className="p-2 bg-emerald-900/30 rounded-lg"><CheckCircle2 className="w-5 h-5 text-emerald-500" /></div>
                        <span className="text-xs font-black text-slate-400 uppercase">Vaciás</span>
                    </div>
                    <p className="text-2xl font-black text-white">{locations.filter(l => l.occupancyStatus === 'Empty').length}</p>
                    <p className="text-xs text-slate-500 font-medium">Listas para recibir</p>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10">
                    <div className="flex items-center justify-between mb-2">
                        <div className="p-2 bg-amber-900/30 rounded-lg"><BarChart3 className="w-5 h-5 text-amber-500" /></div>
                        <span className="text-xs font-black text-slate-400 uppercase">Ocupación</span>
                    </div>
                    <p className="text-2xl font-black text-white">
                        {locations.length > 0 ? Math.round((locations.filter(l => l.occupancyStatus !== 'Empty').length / locations.length) * 100) : 0}%
                    </p>
                    <p className="text-xs text-slate-500 font-medium">Uso actual de bodega</p>
                </div>
                <div className="bg-corp-nav/40 p-5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10">
                    <div className="flex items-center justify-between mb-2">
                        <div className="p-2 bg-corp-accent/30 rounded-lg"><Layers className="w-5 h-5 text-blue-300" /></div>
                        <span className="text-xs font-black text-slate-400 uppercase">Reserva</span>
                    </div>
                    <p className="text-2xl font-black text-white">{locations.filter(l => l.profile === 'Reserve').length}</p>
                    <p className="text-xs text-slate-500 font-medium">Posiciones de Pallets</p>
                </div>
            </div>

            {/* Table Area */}
            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-lg shadow-black/10 overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 flex items-center justify-between bg-corp-base/30">
                    <div className="relative w-96">
                        <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Buscar por código, zona o barcode..."
                            className="w-full pl-11 pr-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all font-medium placeholder:text-slate-600"
                            value={search}
                            onChange={(e) => setSearch(e.target.value)}
                        />
                    </div>
                    <div className="flex items-center gap-2">
                        <span className="text-xs font-bold text-slate-500 uppercase mr-2 tracking-wide">Filtrar por Perfil:</span>
                        <select className="px-3 py-1.5 bg-corp-base border border-corp-secondary rounded-lg text-xs font-bold outline-none focus:ring-2 focus:ring-corp-accent/30 text-slate-300 [color-scheme:dark]">
                            <option>Todos</option>
                            <option>Reserva</option>
                            <option>Picking</option>
                        </select>
                    </div>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                        <thead>
                            <tr className="bg-corp-accent/10 border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Ubicación</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Perfil</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Pick</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Secuencia</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Capacidad (W/V)</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Ocupación</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {loading ? (
                                Array(5).fill(0).map((_, i) => (
                                    <tr key={i} className="animate-pulse">
                                        <td colSpan={7} className="px-6 py-4"><div className="h-4 bg-slate-100 rounded"></div></td>
                                    </tr>
                                ))
                            ) : filteredLocations.map(loc => (
                                <tr key={loc.value} className="hover:bg-corp-accent/5 transition-colors group">
                                    <td className="px-6 py-4">
                                        <div className="flex flex-col">
                                            <span className="font-bold text-white leading-none mb-1">{loc.value}</span>
                                            <span className="text-[10px] font-bold text-slate-400 bg-corp-base/50 px-1.5 py-1 rounded self-start uppercase tracking-wider border border-corp-secondary/30">
                                                Zona: {loc.zone} | BC: {loc.barcode}
                                            </span>
                                        </div>
                                    </td>
                                    <td className="px-6 py-4">
                                        <div className="flex items-center gap-2">
                                            {loc.profile === 'Reserve' ? (
                                                <div className="flex items-center gap-2 text-blue-300 font-bold text-sm">
                                                    <Layers className="w-4 h-4" /> Reserva
                                                </div>
                                            ) : (
                                                <div className="flex items-center gap-2 text-blue-400 font-bold text-sm">
                                                    <Package className="w-4 h-4" /> Picking
                                                </div>
                                            )}
                                        </div>
                                    </td>
                                    <td className="px-6 py-4 text-center">
                                        {loc.isPickable ? (
                                            <span className="text-emerald-400 font-black text-[10px] uppercase bg-emerald-900/30 px-2 py-1 rounded-lg border border-emerald-800/50">SI</span>
                                        ) : (
                                            <span className="text-slate-500 font-black text-[10px] uppercase bg-slate-800/50 px-2 py-1 rounded-lg border border-slate-700/50">NO</span>
                                        )}
                                    </td>
                                    <td className="px-6 py-4 text-center">
                                        <span className="font-mono font-bold text-slate-400 tracking-wider">#{loc.pickingSequence}</span>
                                    </td>
                                    <td className="px-6 py-4">
                                        <div className="flex flex-col gap-1.5">
                                            <div className="flex items-center gap-2">
                                                <span className="text-[10px] font-bold text-blue-400 w-12 text-right">
                                                    {(loc.currentWeight ?? 0).toFixed(1)}
                                                </span>
                                                <div className="w-16 h-1.5 bg-corp-base rounded-full overflow-hidden border border-corp-secondary/30">
                                                    <div
                                                        className="h-full bg-blue-500/70 shadow-[0_0_8px_rgba(59,130,246,0.5)] transition-all duration-300"
                                                        style={{ width: `${Math.min(((loc.currentWeight ?? 0) / (loc.maxWeight || 1)) * 100, 100)}%` }}
                                                    ></div>
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-500">{loc.maxWeight}kg</span>
                                            </div>
                                            <div className="flex items-center gap-2">
                                                <span className="text-[10px] font-bold text-cyan-400 w-12 text-right">
                                                    {(loc.currentVolume ?? 0).toFixed(3)}
                                                </span>
                                                <div className="w-16 h-1.5 bg-corp-base rounded-full overflow-hidden border border-corp-secondary/30">
                                                    <div
                                                        className="h-full bg-corp-accent transition-all duration-300"
                                                        style={{ width: `${Math.min(((loc.currentVolume ?? 0) / (loc.maxVolume || 1)) * 100, 100)}%` }}
                                                    ></div>
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-500">{loc.maxVolume}m³</span>
                                            </div>
                                        </div>
                                    </td>
                                    <td className="px-6 py-4">
                                        <div className="flex items-center gap-2">
                                            <div className={`w-2.5 h-2.5 rounded-full ring-4 ring-offset-0 ring-opacity-20 ${loc.occupancyStatus === 'Empty' ? 'bg-emerald-500 animate-pulse ring-emerald-500/20' :
                                                loc.occupancyStatus === 'Partial' ? 'bg-amber-500 ring-amber-500/20' : 'bg-rose-500 ring-rose-500/20'
                                                }`} />
                                            <span className="text-sm font-bold text-slate-300 capitalize">
                                                {loc.occupancyStatus}
                                                <span className="text-[10px] text-slate-500 ml-1">
                                                    ({loc.lpnCount} {loc.profile === 'Picking' ? 'Units' : 'LPNs'})
                                                </span>
                                            </span>
                                        </div>
                                    </td>
                                    <td className="px-6 py-4 text-right">
                                        <div className="flex items-center justify-end gap-1 opacity-0 group-hover:opacity-100 transition-all">
                                            <button
                                                onClick={() => handleEdit(loc)}
                                                className="p-2 text-slate-500 hover:text-white hover:bg-corp-accent/40 rounded-lg transition-all border border-transparent hover:border-corp-secondary/50"
                                            >
                                                <Edit className="w-4 h-4" />
                                            </button>
                                            <button
                                                onClick={() => handleDelete(loc.value)}
                                                className="p-2 text-slate-500 hover:text-rose-400 hover:bg-rose-900/40 rounded-lg transition-all border border-transparent hover:border-rose-900/50"
                                            >
                                                <Trash2 className="w-4 h-4" />
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>

            {/* Create/Edit Modal */}
            {showModal && (
                <div className="fixed inset-0 bg-black/60 backdrop-blur-md z-50 flex items-center justify-center p-4">
                    <div className="bg-corp-nav border border-corp-secondary/50 rounded-3xl w-full max-w-lg shadow-2xl animate-in zoom-in duration-300">
                        <form onSubmit={handleSave} className="p-8">
                            <div className="flex items-center justify-between mb-6">
                                <h3 className="text-xl font-black text-white">
                                    {editingLocation?.status ? 'Editar Ubicación' : 'Nueva Ubicación'}
                                </h3>
                                <button type="button" onClick={() => setShowModal(false)} className="p-2 hover:bg-corp-accent/30 rounded-xl transition-colors">
                                    <X className="w-6 h-6 text-slate-500" />
                                </button>
                            </div>

                            <div className="space-y-4">
                                <div>
                                    <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Código de Ubicación</label>
                                    <input
                                        disabled={!!editingLocation?.status}
                                        type="text"
                                        className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium disabled:opacity-50"
                                        value={editingLocation?.value || ''}
                                        onChange={(e) => setEditingLocation({ ...editingLocation, value: e.target.value })}
                                        placeholder="Z01-P01-R01-N1-01"
                                        required
                                    />
                                </div>

                                <div className="grid grid-cols-2 gap-4">
                                    <div>
                                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Perfil</label>
                                        <select
                                            disabled={!!editingLocation?.status}
                                            className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium"
                                            value={editingLocation?.profile || 'Picking'}
                                            onChange={(e) => setEditingLocation({ ...editingLocation, profile: e.target.value as any })}
                                        >
                                            <option value="Picking">Picking</option>
                                            <option value="Reserve">Reserva</option>
                                        </select>
                                    </div>
                                    <div className="flex items-center pt-6 ml-2">
                                        <label className="flex items-center gap-2 cursor-pointer group">
                                            <input
                                                type="checkbox"
                                                className="w-4 h-4 rounded border-corp-secondary bg-corp-base text-corp-accent focus:ring-corp-accent"
                                                checked={editingLocation?.isPickable !== false}
                                                onChange={(e) => setEditingLocation({ ...editingLocation, isPickable: e.target.checked })}
                                            />
                                            <span className="text-sm font-bold text-slate-300 group-hover:text-white transition-colors">¿Es Pickable?</span>
                                        </label>
                                    </div>
                                </div>

                                <div className="grid grid-cols-3 gap-4">
                                    <div>
                                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Secuencia</label>
                                        <input
                                            type="number"
                                            className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium"
                                            value={editingLocation?.pickingSequence || 0}
                                            onChange={(e) => setEditingLocation({ ...editingLocation, pickingSequence: parseInt(e.target.value) })}
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Peso Máx (kg)</label>
                                        <input
                                            type="number"
                                            className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium"
                                            value={editingLocation?.maxWeight || 0}
                                            onChange={(e) => setEditingLocation({ ...editingLocation, maxWeight: parseFloat(e.target.value) })}
                                        />
                                    </div>
                                    <div>
                                        <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Volumen Máx</label>
                                        <input
                                            type="number"
                                            step="0.01"
                                            className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium"
                                            value={editingLocation?.maxVolume || 0}
                                            onChange={(e) => setEditingLocation({ ...editingLocation, maxVolume: parseFloat(e.target.value) })}
                                        />
                                    </div>
                                </div>

                                <div>
                                    <label className="block text-xs font-bold text-slate-500 uppercase mb-1.5 ml-1">Código de Barras</label>
                                    <input
                                        type="text"
                                        className="w-full px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-white outline-none focus:ring-2 focus:ring-corp-accent/50 transition-all font-medium"
                                        value={editingLocation?.barcode || ''}
                                        onChange={(e) => setEditingLocation({ ...editingLocation, barcode: e.target.value })}
                                        placeholder="Escanee o escriba"
                                    />
                                </div>
                            </div>

                            <div className="mt-8 flex gap-3">
                                <button
                                    type="button"
                                    onClick={() => setShowModal(false)}
                                    className="flex-1 py-3 bg-corp-base/50 text-slate-400 rounded-2xl font-black hover:bg-corp-secondary/30 transition-all uppercase tracking-widest text-xs border border-corp-secondary/30"
                                >
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    className="flex-1 py-3 bg-corp-accent text-white rounded-2xl font-black hover:bg-corp-accent/80 transition-all shadow-lg shadow-black/20 uppercase tracking-widest text-xs"
                                >
                                    Guardar Cambios
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}

            {/* Import Modal */}
            {showImportModal && (
                <div className="fixed inset-0 bg-black/60 backdrop-blur-md z-50 flex items-center justify-center p-4">
                    <div className="bg-corp-nav border border-corp-secondary/50 rounded-3xl w-full max-w-2xl shadow-2xl animate-in zoom-in duration-300">
                        <div className="p-8">
                            <div className="flex items-center justify-between mb-6">
                                <div className="flex items-center gap-3">
                                    <div className="p-3 bg-corp-accent/40 rounded-2xl"><FileSpreadsheet className="text-corp-accent w-6 h-6 shadow-[0_0_10px_rgba(30,66,88,0.5)]" /></div>
                                    <div>
                                        <h3 className="text-xl font-black text-white">Importación Masiva</h3>
                                        <p className="text-slate-400 text-sm font-medium">Pegue su JSON de ubicaciones aquí</p>
                                    </div>
                                </div>
                                <button onClick={() => setShowImportModal(false)} className="p-2 hover:bg-corp-accent/30 rounded-xl transition-colors">
                                    <X className="w-6 h-6 text-slate-500" />
                                </button>
                            </div>

                            <textarea
                                className="w-full h-64 p-4 font-mono text-xs bg-corp-base/50 border border-corp-secondary/50 rounded-2xl outline-none focus:ring-4 focus:ring-corp-accent/10 focus:border-corp-accent transition-all text-blue-100 placeholder:text-slate-700"
                                placeholder={`[ \n  { "Code": "Z01-P01-R01-N1-01", "Profile": "Picking", "IsPickable": true, "PickingSequence": 1, "MaxWeight": 500, "MaxVolume": 1.5 }\n]`}
                                value={importData}
                                onChange={(e) => setImportData(e.target.value)}
                            />

                            <div className="mt-8 flex gap-3">
                                <button
                                    onClick={() => setShowImportModal(false)}
                                    className="flex-1 py-3 bg-corp-base/50 text-slate-400 rounded-2xl font-black hover:bg-corp-secondary/30 transition-all uppercase tracking-widest text-xs border border-corp-secondary/30"
                                >
                                    Cancelar
                                </button>
                                <button
                                    onClick={handleImport}
                                    className="flex-1 py-3 bg-corp-accent text-white rounded-2xl font-black hover:bg-corp-accent/80 transition-all shadow-lg shadow-black/20 uppercase tracking-widest text-xs"
                                >
                                    Procesar Importación
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
