// src/Victoria.UI/src/components/Inbound/ReceiptStation.tsx
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
    Printer
} from 'lucide-react';
import { useVolumeCalc } from '../../hooks/useVolumeCalc';
import { useLpnPreview } from '../../hooks/useLpnPreview';
import type { ImageSource, ReceiptLine } from '../../types/inbound';

const mockLines: ReceiptLine[] = [
    { id: '1', sku: 'LAP-M2-001', productName: 'Apple MacBook Air M2 13"', expectedQty: 50, receivedQty: 12, imageSource: 'master' },
    { id: '2', sku: 'MO-LG-32', productName: 'Monitor LG UltraFine 32" 4K', expectedQty: 30, receivedQty: 30, imageSource: 'variant' },
    { id: '3', sku: 'KB-MX-MECH', productName: 'Logitech MX Mechanical Mini', expectedQty: 100, receivedQty: 0, imageSource: 'brand' },
    { id: '4', sku: 'MS-AP-2', productName: 'Apple Magic Mouse 2', expectedQty: 80, receivedQty: 15, imageSource: null },
];

const ReceiptStation: React.FC = () => {
    const [selectedLine, setSelectedLine] = useState<ReceiptLine | null>(mockLines[0]);
    const [tenant, setTenant] = useState('PerfectPTY');
    const [workMode, setWorkMode] = useState<'bulto' | 'unitario'>('bulto');
    const [isRfid, setIsRfid] = useState(false);

    const [dims, setDims] = useState({ length: 40, width: 30, height: 20 });
    const [qty, setQty] = useState({ items: 1, factor: 10 });

    const calculatedVolume = useVolumeCalc(dims);
    const lpnPreview = useLpnPreview(tenant);

    const getImageStatusIcon = (source: ImageSource) => {
        switch (source) {
            case 'variant': return <CheckCircle2 className="w-5 h-5 text-emerald-500" />;
            case 'master': return <CheckCircle2 className="w-5 h-5 text-emerald-500" />;
            case 'brand': return <Info className="w-5 h-5 text-blue-500" />;
            default: return <Camera className="w-5 h-5 text-rose-500" />;
        }
    };

    return (
        <div className="flex flex-col h-screen bg-slate-50 overflow-hidden">
            {/* Station Header */}
            <header className="bg-slate-900 text-white p-4 shadow-xl flex items-center justify-between z-10">
                <div className="flex items-center space-x-6">
                    <button className="p-2 hover:bg-slate-800 rounded-full transition-colors border border-slate-700">
                        <ArrowLeft className="w-5 h-5" />
                    </button>
                    <div>
                        <h1 className="text-lg font-bold flex items-center space-x-2">
                            <span className="text-blue-400 font-mono">PO-2024-001</span>
                            <span className="text-slate-500 mx-2">|</span>
                            <span className="uppercase tracking-widest text-sm opacity-80">Estación de Recibo #04</span>
                        </h1>
                        <p className="text-xs text-slate-400 font-medium">Proveedor: Tech Distribution Inc.</p>
                    </div>
                </div>

                <div className="flex items-center space-x-4">
                    <select
                        className="bg-slate-800 border border-slate-700 text-sm rounded-lg px-3 py-1.5 focus:ring-2 focus:ring-blue-500 outline-none"
                        value={tenant}
                        onChange={(e) => setTenant(e.target.value)}
                    >
                        <option value="PerfectPTY">PerfectPTY (PTC)</option>
                        <option value="Natsuki">Natsuki (NAT)</option>
                        <option value="PDM">PDM (PDM)</option>
                        <option value="Filtros">Filtros (FLT)</option>
                    </select>
                    <div className="h-8 w-px bg-slate-700 mx-2"></div>
                    <div className="flex items-center space-x-2">
                        <div className="w-3 h-3 bg-emerald-500 rounded-full animate-pulse shadow-[0_0_10px_rgba(16,185,129,0.5)]"></div>
                        <span className="text-xs font-bold uppercase tracking-widest">En Línea</span>
                    </div>
                </div>
            </header>

            {/* Main Content Areas */}
            <main className="flex-1 flex overflow-hidden">
                {/* Left Side: Picking List */}
                <section className="w-2/5 border-r border-slate-200 bg-white flex flex-col shadow-sm">
                    <div className="p-4 bg-slate-50 border-b border-slate-200 flex justify-between items-center">
                        <h3 className="font-bold text-slate-800 flex items-center space-x-2 grayscale">
                            <Package className="w-5 h-5 text-blue-600" />
                            <span>Items Pendientes</span>
                        </h3>
                        <span className="text-xs font-bold bg-slate-200 px-2 py-0.5 rounded text-slate-600">4 SKUs</span>
                    </div>
                    <div className="flex-1 overflow-y-auto divide-y divide-slate-100">
                        {mockLines.map((line) => (
                            <div
                                key={line.id}
                                onClick={() => setSelectedLine(line)}
                                className={`p-4 cursor-pointer transition-all border-l-4 ${selectedLine?.id === line.id
                                    ? 'bg-blue-50/50 border-blue-500'
                                    : 'hover:bg-slate-50 border-transparent'
                                    }`}
                            >
                                <div className="flex justify-between items-start mb-2">
                                    <div>
                                        <span className="text-xs font-mono font-bold text-blue-600 bg-blue-50 px-2 py-0.5 rounded uppercase tracking-tighter">
                                            {line.sku}
                                        </span>
                                        <h4 className="text-sm font-semibold text-slate-800 mt-1 line-clamp-1">{line.productName}</h4>
                                    </div>
                                    {getImageStatusIcon(line.imageSource)}
                                </div>
                                <div className="flex items-center justify-between text-xs font-medium text-slate-500">
                                    <div className="flex flex-col">
                                        <span>Avance: {line.receivedQty} / {line.expectedQty}</span>
                                        <div className="w-32 h-1.5 bg-slate-100 rounded-full mt-1.5 overflow-hidden">
                                            <div
                                                className={`h-full transition-all duration-1000 ${line.receivedQty === line.expectedQty ? 'bg-emerald-500' : 'bg-blue-500'}`}
                                                style={{ width: `${(line.receivedQty / line.expectedQty) * 100}%` }}
                                            ></div>
                                        </div>
                                    </div>
                                    {line.receivedQty === line.expectedQty && (
                                        <span className="text-emerald-600 font-bold uppercase tracking-tighter flex items-center">
                                            Listo <CheckCircle2 className="w-3 h-3 ml-1" />
                                        </span>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </section>

                {/* Right Side: Work Form */}
                <section className="flex-1 p-8 overflow-y-auto scrollbar-hide">
                    {selectedLine ? (
                        <div className="max-w-4xl mx-auto space-y-8">
                            {/* Product Info Banner */}
                            <div className="bg-slate-900 rounded-2xl p-6 text-white shadow-2xl relative overflow-hidden group">
                                <div className="absolute right-0 top-0 opacity-10 group-hover:scale-110 transition-transform -translate-y-4 translate-x-4">
                                    <Scan className="w-40 h-40" />
                                </div>
                                <div className="relative z-10 space-y-4">
                                    <div className="flex items-center space-x-3">
                                        <span className="bg-blue-500 text-[10px] font-bold px-2 py-0.5 rounded-full uppercase tracking-widest">En Proceso</span>
                                        <span className="text-slate-400 font-mono text-sm">{selectedLine.sku}</span>
                                    </div>
                                    <h2 className="text-3xl font-black tracking-tight leading-tight">{selectedLine.productName}</h2>
                                </div>
                            </div>

                            {/* Mode Tabs */}
                            <div className="flex p-1 bg-slate-200 rounded-xl max-w-sm shadow-inner">
                                <button
                                    onClick={() => setWorkMode('bulto')}
                                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${workMode === 'bulto' ? 'bg-white shadow-md text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}
                                >
                                    Recibir por Bulto
                                </button>
                                <button
                                    onClick={() => setWorkMode('unitario')}
                                    className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${workMode === 'unitario' ? 'bg-white shadow-md text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}
                                >
                                    Recibir Suelto
                                </button>
                            </div>

                            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
                                {/* Physical Data Card */}
                                <div className="bg-white p-8 rounded-3xl shadow-sm border border-slate-100 space-y-6">
                                    <div className="flex items-center space-x-2 border-b border-slate-50 pb-4">
                                        <Settings2 className="w-5 h-5 text-slate-400" />
                                        <h3 className="font-black text-slate-800 uppercase tracking-widest text-xs italic">Dimensiones y Peso</h3>
                                    </div>

                                    <div className="grid grid-cols-3 gap-4">
                                        <div className="space-y-2">
                                            <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Largo (cm)</label>
                                            <input type="number" value={dims.length} onChange={e => setDims({ ...dims, length: +e.target.value })} className="w-full bg-slate-50 border border-slate-200 p-3 rounded-xl focus:ring-4 focus:ring-blue-100 focus:border-blue-500 transition-all outline-none font-bold text-slate-700" />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Ancho (cm)</label>
                                            <input type="number" value={dims.width} onChange={e => setDims({ ...dims, width: +e.target.value })} className="w-full bg-slate-50 border border-slate-200 p-3 rounded-xl focus:ring-4 focus:ring-blue-100 focus:border-blue-500 transition-all outline-none font-bold text-slate-700" />
                                        </div>
                                        <div className="space-y-2">
                                            <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Alto (cm)</label>
                                            <input type="number" value={dims.height} onChange={e => setDims({ ...dims, height: +e.target.value })} className="w-full bg-slate-50 border border-slate-200 p-3 rounded-xl focus:ring-4 focus:ring-blue-100 focus:border-blue-500 transition-all outline-none font-bold text-slate-700" />
                                        </div>
                                    </div>

                                    <div className="p-4 bg-emerald-50 rounded-2xl flex items-center justify-between border border-emerald-100 group">
                                        <div className="flex items-center space-x-3">
                                            <Calculator className="w-5 h-5 text-emerald-600 transition-transform group-hover:rotate-12" />
                                            <span className="text-xs font-bold text-emerald-700 uppercase tracking-wider">Volumen Calculado</span>
                                        </div>
                                        <span className="text-xl font-black text-emerald-900 font-mono">{calculatedVolume} m³</span>
                                    </div>
                                </div>

                                {/* Tracking & Label Card */}
                                <div className="bg-white p-8 rounded-3xl shadow-sm border border-slate-100 space-y-6">
                                    <div className="flex items-center space-x-2 border-b border-slate-50 pb-4">
                                        <Scan className="w-5 h-5 text-slate-400" />
                                        <h3 className="font-black text-slate-800 uppercase tracking-widest text-xs italic">Etiquetado y Control</h3>
                                    </div>

                                    <div className="flex items-center justify-between p-4 bg-slate-50 rounded-2xl">
                                        <div className="space-y-1">
                                            <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Tecnología LPN</p>
                                            <p className={`text-sm font-black transition-colors ${isRfid ? 'text-blue-600' : 'text-slate-700'}`}>{isRfid ? 'CHIP RFID G2V' : 'CÓDIGO 128 (NORMAL)'}</p>
                                        </div>
                                        <label className="relative inline-flex items-center cursor-pointer">
                                            <input type="checkbox" checked={isRfid} onChange={e => setIsRfid(e.target.checked)} className="sr-only peer" />
                                            <div className="w-11 h-6 bg-slate-300 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-blue-600"></div>
                                        </label>
                                    </div>

                                    <div className="space-y-3">
                                        <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest block">Vista Previa Etiqueta</label>
                                        <div className="p-4 bg-slate-100 border-2 border-dashed border-slate-300 rounded-2xl flex flex-col items-center justify-center space-y-2 opacity-80 group hover:opacity-100 hover:border-blue-300 transition-all">
                                            <span className="text-xs font-bold text-slate-400 uppercase tracking-tighter">Muestra del Sistema</span>
                                            <span className="text-2xl font-black text-slate-900 font-mono uppercase group-hover:text-blue-600">{lpnPreview}</span>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Action Bar */}
                            <div className="bg-white p-6 rounded-3xl shadow-2xl border border-slate-100 flex items-center justify-between sticky bottom-4">
                                <div className="flex items-center space-x-6">
                                    <div className="space-y-1">
                                        <label className="text-[10px] font-black text-slate-400 uppercase tracking-widest">Cantidad a Recibir</label>
                                        <div className="flex items-center space-x-3">
                                            <input type="number" value={qty.items} onChange={e => setQty({ ...qty, items: +e.target.value })} className="w-24 bg-slate-900 text-white p-3 rounded-xl font-black text-center text-lg outline-none ring-blue-500/50 focus:ring-4" />
                                            <span className="text-slate-300 font-bold italic">x</span>
                                            <span className="text-lg font-black text-slate-700">{qty.factor} unds/bulto</span>
                                        </div>
                                    </div>
                                    <div className="h-12 w-px bg-slate-100"></div>
                                    <div>
                                        <p className="text-[10px] font-black text-slate-400 uppercase tracking-widest leading-none mb-1 text-right">Total Final</p>
                                        <p className="text-3xl font-black text-slate-900">{qty.items * qty.factor} <span className="text-sm font-bold opacity-40">UNITS</span></p>
                                    </div>
                                </div>

                                <button className="flex items-center space-x-3 px-10 py-5 bg-blue-600 text-white rounded-2xl font-black uppercase tracking-[2px] shadow-[0_20px_40px_rgba(37,99,235,0.3)] hover:bg-blue-700 hover:-translate-y-1 active:scale-95 transition-all group">
                                    <Printer className="w-6 h-6 group-hover:rotate-12 transition-transform" />
                                    <span>Confirmar y Etiquetar</span>
                                </button>
                            </div>
                        </div>
                    ) : (
                        <div className="h-full flex flex-col items-center justify-center text-slate-300 space-y-4">
                            <div className="p-8 bg-slate-100 rounded-full animate-pulse">
                                <Package className="w-20 h-20" />
                            </div>
                            <p className="text-xl font-black uppercase tracking-widest italic">Selecciona un SKU para iniciar</p>
                        </div>
                    )}
                </section>
            </main>
        </div>
    );
};

export default ReceiptStation;
