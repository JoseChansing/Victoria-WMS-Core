import React, { useState } from 'react';
import {
    ArrowLeft,
    Package,
    Scan,
    Settings2,
    Calculator,
    CheckCircle2,
    Info,
    Camera,
    Printer,
    ShieldCheck
} from 'lucide-react';
import { useVolumeCalc } from '../../hooks/useVolumeCalc';
import { useLpnPreview } from '../../hooks/useLpnPreview';
import { useInbound } from '../../hooks/useInbound';
import type { ImageSource, ReceiptLine } from '../../types/inbound';

const mockLines: ReceiptLine[] = [
    { id: '1', sku: 'LAP-M2-001', productName: 'Apple MacBook Air M2 13"', expectedQty: 50, receivedQty: 12, imageSource: 'master' },
    { id: '2', sku: 'MO-LG-32', productName: 'Monitor LG UltraFine 32" 4K', expectedQty: 30, receivedQty: 30, imageSource: 'variant' },
    { id: '3', sku: 'KB-MX-MECH', productName: 'Logitech MX Mechanical Mini', expectedQty: 100, receivedQty: 0, imageSource: 'brand' },
    { id: '4', sku: 'MS-AP-2', productName: 'Apple Magic Mouse 2', expectedQty: 80, receivedQty: 15, imageSource: null },
];

const ReceiptStation: React.FC = () => {
    const { printRfid } = useInbound();
    const [selectedLine, setSelectedLine] = useState<ReceiptLine | null>(mockLines[0]);
    const [tenant, setTenant] = useState('PerfectPTY');
    const [workMode, setWorkMode] = useState<'bulto' | 'unitario'>('bulto');
    const [isRfid, setIsRfid] = useState(false);
    const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

    const [dims, setDims] = useState({ length: 40, width: 30, height: 20 });
    const [qty, setQty] = useState({ items: 1, factor: 10 });

    const calculatedVolume = useVolumeCalc(dims);
    const lpnPreview = useLpnPreview(tenant);

    const handlePrintRfid = async () => {
        if (!selectedLine) return;
        try {
            // Demo use: mapping selectedLine.id as lpnId
            const data = await printRfid(selectedLine.id);
            setFeedback({ type: 'success', message: `RFID Programado: ${data.epcHex}` });
        } catch (error) {
            setFeedback({ type: 'error', message: "Error al programar RFID." });
        }
    };

    const getImageStatusIcon = (source: ImageSource) => {
        switch (source) {
            case 'variant': return <CheckCircle2 className="w-5 h-5 text-emerald-400" />;
            case 'master': return <CheckCircle2 className="w-5 h-5 text-emerald-400" />;
            case 'brand': return <Info className="w-5 h-5 text-blue-400" />;
            default: return <Camera className="w-5 h-5 text-rose-400" />;
        }
    };

    return (
        <div className="flex flex-col h-screen bg-corp-base text-white overflow-hidden font-sans">
            {/* Station Header */}
            <header className="bg-corp-nav/90 backdrop-blur-md border-b border-corp-secondary p-4 shadow-2xl flex items-center justify-between z-10">
                <div className="flex items-center space-x-6">
                    <button className="p-2.5 hover:bg-corp-accent/40 rounded-xl transition-all border border-corp-secondary/50 text-slate-400 hover:text-white">
                        <ArrowLeft className="w-5 h-5" />
                    </button>
                    <div>
                        <h1 className="text-xl font-black flex items-center space-x-3 tracking-tight">
                            <span className="text-blue-400 font-mono">PO-2024-001</span>
                            <span className="text-corp-secondary mx-2">|</span>
                            <span className="uppercase tracking-[0.2em] text-xs font-black opacity-60">Estación de Recibo #04</span>
                        </h1>
                        <p className="text-xs text-slate-400 font-bold uppercase tracking-widest mt-1">Proveedor: Tech Distribution Inc.</p>
                    </div>
                </div>

                <div className="flex items-center space-x-6">
                    <div className="relative group">
                        <select
                            className="bg-corp-base border border-corp-secondary text-xs rounded-xl px-4 py-2.5 focus:ring-2 focus:ring-corp-accent outline-none appearance-none pr-10 font-black tracking-widest text-blue-300"
                            value={tenant}
                            onChange={(e) => setTenant(e.target.value)}
                        >
                            <option value="PerfectPTY">PERFECTPTY (PTC)</option>
                            <option value="Natsuki">NATSUKI (NAT)</option>
                            <option value="PDM">PDM (PDM)</option>
                            <option value="Filtros">FILTROS (FLT)</option>
                        </select>
                        <Settings2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500 pointer-events-none" />
                    </div>
                    <div className="h-8 w-px bg-corp-secondary/30"></div>
                    <div className="flex items-center space-x-3 bg-emerald-950/20 border border-emerald-800/30 px-4 py-2 rounded-xl">
                        <div className="w-2.5 h-2.5 bg-emerald-500 rounded-full animate-pulse shadow-[0_0_12px_rgba(16,185,129,0.8)]"></div>
                        <span className="text-[10px] font-black uppercase tracking-widest text-emerald-400">En Línea</span>
                    </div>
                </div>
            </header>

            {/* Main Content Areas */}
            <main className="flex-1 flex overflow-hidden">
                {/* Left Side: Picking List */}
                <section className="w-2/5 border-r border-corp-secondary/30 bg-corp-nav/20 flex flex-col shadow-inner">
                    <div className="p-5 bg-corp-accent/10 border-b border-corp-secondary/20 flex justify-between items-center">
                        <h3 className="font-black text-xs uppercase tracking-[0.2em] text-slate-400 flex items-center space-x-3">
                            <Package className="w-5 h-5 text-blue-400" />
                            <span>Items Pendientes</span>
                        </h3>
                        <span className="text-[10px] font-black bg-corp-accent text-blue-200 px-3 py-1 rounded-full border border-corp-secondary">4 SKUS AKTIVOS</span>
                    </div>
                    <div className="flex-1 overflow-y-auto divide-y divide-corp-secondary/10 custom-scrollbar">
                        {mockLines.map((line) => (
                            <div
                                key={line.id}
                                onClick={() => setSelectedLine(line)}
                                className={`p-6 cursor-pointer transition-all border-l-4 group ${selectedLine?.id === line.id
                                    ? 'bg-corp-accent/20 border-blue-500 shadow-inner'
                                    : 'hover:bg-corp-accent/5 border-transparent'
                                    }`}
                            >
                                <div className="flex justify-between items-start mb-4">
                                    <div className="space-y-2">
                                        <span className={`text-[10px] font-black px-3 py-1 rounded-lg uppercase tracking-widest border transition-colors ${selectedLine?.id === line.id ? 'bg-blue-600 text-white border-blue-400/50' : 'bg-corp-base text-blue-400 border-corp-secondary'}`}>
                                            {line.sku}
                                        </span>
                                        <h4 className={`text-md font-bold mt-2 transition-colors ${selectedLine?.id === line.id ? 'text-white' : 'text-slate-400 group-hover:text-slate-200'} line-clamp-1`}>{line.productName}</h4>
                                    </div>
                                    <div className="p-2 bg-corp-base/50 rounded-xl border border-corp-secondary/30">
                                        {getImageStatusIcon(line.imageSource)}
                                    </div>
                                </div>
                                <div className="space-y-2">
                                    <div className="flex items-center justify-between text-[10px] font-black uppercase tracking-widest">
                                        <span className="text-slate-500">Avance de Recibo</span>
                                        <span className={line.receivedQty === line.expectedQty ? 'text-emerald-400' : 'text-blue-400'}>
                                            {line.receivedQty} / {line.expectedQty}
                                        </span>
                                    </div>
                                    <div className="w-full h-2 bg-corp-base rounded-full overflow-hidden border border-corp-secondary/30 shadow-inner">
                                        <div
                                            className={`h-full transition-all duration-1000 ${line.receivedQty === line.expectedQty ? 'bg-emerald-500' : 'bg-gradient-to-r from-blue-600 to-corp-accent'}`}
                                            style={{ width: `${(line.receivedQty / line.expectedQty) * 100}%` }}
                                        ></div>
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </section>

                {/* Right Side: Work Form */}
                <section className="flex-1 p-10 overflow-y-auto custom-scrollbar bg-[radial-gradient(circle_at_top_right,_var(--tw-gradient-stops))] from-corp-accent/5 via-transparent to-transparent">
                    {selectedLine ? (
                        <div className="max-w-5xl mx-auto space-y-10 animate-in fade-in duration-700">
                            {/* Product Info Banner */}
                            <div className="bg-gradient-to-br from-corp-nav to-corp-base rounded-[2.5rem] p-10 text-white shadow-3xl border border-corp-secondary/50 relative overflow-hidden group">
                                <div className="absolute right-0 top-0 opacity-5 group-hover:opacity-10 transition-opacity -translate-y-10 translate-x-10">
                                    <Scan className="w-64 h-64" />
                                </div>
                                <div className="relative z-10 space-y-6">
                                    <div className="flex items-center space-x-4">
                                        <div className="bg-emerald-500 text-[10px] font-black px-4 py-1.5 rounded-full uppercase tracking-[0.2em] shadow-lg shadow-emerald-900/20">OPERANDO AKTIVO</div>
                                        <div className="text-blue-400 font-black font-mono text-sm tracking-widest border-l-2 border-corp-accent pl-4">{selectedLine.sku}</div>
                                    </div>
                                    <h2 className="text-5xl font-black tracking-tighter leading-none selection:bg-blue-600">{selectedLine.productName}</h2>
                                    <div className="flex items-center space-x-2 text-slate-500 font-bold">
                                        <ShieldCheck className="w-4 h-4 text-emerald-500" />
                                        <span className="text-xs uppercase tracking-widest">Protocolo de Integridad de Almacén Victoria</span>
                                    </div>
                                </div>
                            </div>

                            {/* Mode Tabs */}
                            <div className="flex p-1.5 bg-corp-nav/40 border border-corp-secondary/30 backdrop-blur-md rounded-2xl max-w-md shadow-2xl">
                                <button
                                    onClick={() => setWorkMode('bulto')}
                                    className={`flex-1 py-3 text-xs font-black uppercase tracking-widest rounded-xl transition-all ${workMode === 'bulto' ? 'bg-corp-accent text-white shadow-lg shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                >
                                    Recibir por Bulto
                                </button>
                                <button
                                    onClick={() => setWorkMode('unitario')}
                                    className={`flex-1 py-3 text-xs font-black uppercase tracking-widest rounded-xl transition-all ${workMode === 'unitario' ? 'bg-corp-accent text-white shadow-lg shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                >
                                    Recibir Suelto
                                </button>
                            </div>

                            <div className="grid grid-cols-1 xl:grid-cols-2 gap-10">
                                {/* Physical Data Card */}
                                <div className="bg-corp-nav/40 backdrop-blur-md p-10 rounded-[2.5rem] shadow-2xl border border-corp-secondary/50 space-y-8 animate-in slide-in-from-bottom-4 duration-500">
                                    <div className="flex items-center justify-between border-b border-corp-secondary/20 pb-6">
                                        <div className="flex items-center space-x-4">
                                            <div className="p-2.5 bg-corp-base rounded-xl border border-corp-secondary text-blue-400">
                                                <Settings2 className="w-5 h-5" />
                                            </div>
                                            <h3 className="font-black text-white uppercase tracking-[0.2em] text-xs">Dimensiones y Peso</h3>
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-3 gap-6">
                                        <div className="space-y-3">
                                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Largo (cm)</label>
                                            <input type="number" value={dims.length} onChange={e => setDims({ ...dims, length: +e.target.value })} className="w-full bg-corp-base/60 border border-corp-secondary p-4 rounded-2xl focus:ring-4 focus:ring-corp-accent/20 focus:border-corp-accent transition-all outline-none font-black text-white text-xl text-center shadow-inner" />
                                        </div>
                                        <div className="space-y-3">
                                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Ancho (cm)</label>
                                            <input type="number" value={dims.width} onChange={e => setDims({ ...dims, width: +e.target.value })} className="w-full bg-corp-base/60 border border-corp-secondary p-4 rounded-2xl focus:ring-4 focus:ring-corp-accent/20 focus:border-corp-accent transition-all outline-none font-black text-white text-xl text-center shadow-inner" />
                                        </div>
                                        <div className="space-y-3">
                                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Alto (cm)</label>
                                            <input type="number" value={dims.height} onChange={e => setDims({ ...dims, height: +e.target.value })} className="w-full bg-corp-base/60 border border-corp-secondary p-4 rounded-2xl focus:ring-4 focus:ring-corp-accent/20 focus:border-corp-accent transition-all outline-none font-black text-white text-xl text-center shadow-inner" />
                                        </div>
                                    </div>

                                    <div className="p-6 bg-emerald-950/20 rounded-[2rem] flex items-center justify-between border border-emerald-800/30 group shadow-inner">
                                        <div className="flex items-center space-x-5">
                                            <div className="p-3 bg-emerald-900/40 rounded-2xl border border-emerald-800/50">
                                                <Calculator className="w-6 h-6 text-emerald-400 transition-transform group-hover:rotate-12" />
                                            </div>
                                            <span className="text-[10px] font-black text-emerald-400 uppercase tracking-[0.2em]">Volumen Calculado</span>
                                        </div>
                                        <span className="text-3xl font-black text-white font-mono">{calculatedVolume} <span className="text-sm text-emerald-600">m³</span></span>
                                    </div>
                                </div>

                                {/* Tracking & Label Card */}
                                <div className="bg-corp-nav/40 backdrop-blur-md p-10 rounded-[2.5rem] shadow-2xl border border-corp-secondary/50 space-y-8 animate-in slide-in-from-bottom-6 duration-700">
                                    <div className="flex items-center justify-between border-b border-corp-secondary/20 pb-6">
                                        <div className="flex items-center space-x-4">
                                            <div className="p-2.5 bg-corp-base rounded-xl border border-corp-secondary text-blue-400">
                                                <Scan className="w-5 h-5" />
                                            </div>
                                            <h3 className="font-black text-white uppercase tracking-[0.2em] text-xs">Etiquetado y Control</h3>
                                        </div>
                                    </div>

                                    <div className="flex items-center justify-between p-6 bg-corp-base/40 rounded-[2rem] border border-corp-secondary/30 shadow-inner group">
                                        <div className="space-y-2">
                                            <p className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Tecnología LPN</p>
                                            <p className={`text-md font-black transition-colors ${isRfid ? 'text-blue-400 underline decoration-blue-500/30' : 'text-slate-400'}`}>{isRfid ? 'CHIP RFID G2V AKTIVO' : 'CÓDIGO 128 (ESTÁNDAR)'}</p>
                                        </div>
                                        <div className="flex items-center space-x-4">
                                            {isRfid && feedback && (
                                                <div className={`text-[9px] font-black uppercase px-3 py-1 rounded-lg ${feedback.type === 'success' ? 'text-emerald-400 bg-emerald-900/20' : 'text-rose-400 bg-rose-900/20'}`}>
                                                    {feedback.message}
                                                </div>
                                            )}
                                            <label className="relative inline-flex items-center cursor-pointer">
                                                <input type="checkbox" checked={isRfid} onChange={e => setIsRfid(e.target.checked)} className="sr-only peer" />
                                                <div className="w-16 h-8 bg-corp-base border-2 border-corp-secondary rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-blue-400 after:content-[''] after:absolute after:top-[4px] after:left-[4px] after:bg-slate-700 after:rounded-full after:h-6 after:w-7 after:transition-all peer-checked:bg-blue-900/40 peer-checked:border-blue-700"></div>
                                            </label>
                                        </div>
                                    </div>

                                    <div className="space-y-4">
                                        <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest block ml-2">Vista Previa Etiqueta Logística</label>
                                        <div className="p-8 bg-corp-base/60 border-2 border-dashed border-corp-secondary rounded-[2.5rem] flex flex-col items-center justify-center space-y-4 opacity-80 group hover:opacity-100 hover:border-blue-500/50 transition-all shadow-inner">
                                            <div className="flex space-x-2">
                                                {[1, 2, 3, 4, 5, 6].map(i => <div key={i} className="w-1.5 h-12 bg-slate-800 rounded-full"></div>)}
                                                <div className="w-3 h-12 bg-slate-800 rounded-full"></div>
                                                {[1, 2, 3].map(i => <div key={i} className="w-1.5 h-12 bg-slate-800 rounded-full"></div>)}
                                            </div>
                                            <span className="text-3xl font-black text-white font-mono uppercase tracking-[0.2em] group-hover:text-blue-400 transition-colors">{lpnPreview}</span>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Action Bar */}
                            <div className="bg-corp-nav/80 backdrop-blur-xl p-8 rounded-[2.5rem] shadow-3xl border border-corp-secondary/50 flex items-center justify-between sticky bottom-6 z-20 animate-in slide-in-from-bottom-10 duration-700">
                                <div className="flex items-center space-x-10">
                                    <div className="space-y-2">
                                        <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Cant. a Recibir</label>
                                        <div className="flex items-center space-x-5">
                                            <input type="number" value={qty.items} onChange={e => setQty({ ...qty, items: +e.target.value })} className="w-28 bg-corp-base text-white p-4 rounded-2xl font-black text-center text-2xl outline-none ring-corp-accent/40 focus:ring-4 border border-corp-secondary shadow-inner" />
                                            <span className="text-corp-secondary font-black italic text-xl">x</span>
                                            <span className="text-xl font-black text-slate-300 tracking-tight">{qty.factor} unds/bulto</span>
                                        </div>
                                    </div>
                                    <div className="h-16 w-px bg-corp-secondary/20"></div>
                                    <div className="text-right">
                                        <p className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em] leading-none mb-3">Total Consolidado</p>
                                        <p className="text-5xl font-black text-white tracking-tighter leading-none">{qty.items * qty.factor} <span className="text-xs font-black text-blue-500/60 ml-2 tracking-widest">UNITS</span></p>
                                    </div>
                                </div>

                                <button
                                    onClick={isRfid ? handlePrintRfid : undefined}
                                    className="flex items-center space-x-4 px-12 py-6 bg-corp-accent text-white rounded-[2rem] font-black uppercase tracking-[3px] shadow-3xl shadow-blue-900/40 hover:bg-blue-600 hover:-translate-y-2 active:scale-95 transition-all group border border-blue-400/20"
                                >
                                    <Printer className="w-8 h-8 group-hover:rotate-12 transition-transform" />
                                    <span className="text-lg">Confirmar y Etiquetar</span>
                                </button>
                            </div>
                        </div>
                    ) : (
                        <div className="h-full flex flex-col items-center justify-center text-slate-700 space-y-8">
                            <div className="p-12 bg-corp-nav/40 rounded-full animate-pulse border border-corp-secondary/30 shadow-2xl">
                                <Package className="w-24 h-24 text-corp-accent/20" />
                            </div>
                            <div className="text-center space-y-3">
                                <p className="text-2xl font-black uppercase tracking-[0.4em] text-slate-600 animate-pulse">Esperando Selección</p>
                                <p className="text-[10px] font-black text-slate-800 uppercase tracking-widest">Selecciona un SKU de la lista izquierda para operar</p>
                            </div>
                        </div>
                    )}
                </section>
            </main>
        </div>
    );
};

export default ReceiptStation;
