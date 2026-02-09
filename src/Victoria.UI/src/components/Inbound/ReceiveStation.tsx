import React, { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { ArrowLeft, Package, CheckCircle2, AlertCircle, ScanLine, Calculator, Printer, Radio } from 'lucide-react';
import { useInbound } from '../../hooks/useInbound';
import { zebraService } from '../../services/zebra.service';

interface ReceiveStationProps {
    mode: 'rfid' | 'standard';
}

const ReceiveStation: React.FC<ReceiveStationProps> = ({ mode }) => {
    const { orderId } = useParams<{ orderId: string }>();
    const navigate = useNavigate();
    const { orders, receiveLpn, isReceiving } = useInbound();

    // State
    const [receiveMode, setReceiveMode] = useState<'UNIT' | 'BULK'>('UNIT');
    const [scanValue, setScanValue] = useState('');
    const [quantity, setQuantity] = useState<number | string>(1);
    const [lpnCount, setLpnCount] = useState<number | string>(1);
    const [unitsPerLpn, setUnitsPerLpn] = useState<number | string>(1);
    const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
    const [lastReceivedLpnIds, setLastReceivedLpnIds] = useState<string[]>([]);
    const [printUrl, setPrintUrl] = useState<string | null>(null);

    // Editable Physical Attributes States (Modified to string to solve backspace bug)
    const [manualWeight, setManualWeight] = useState<number | string>(0);
    const [manualLength, setManualLength] = useState<number | string>(0);
    const [manualWidth, setManualWidth] = useState<number | string>(0);
    const [manualHeight, setManualHeight] = useState<number | string>(0);

    const inputRef = useRef<HTMLInputElement>(null);

    // Find Order
    const order = orders?.find(o => o.id === orderId || o.orderNumber === orderId);

    // Calc Peso Estimado
    const selectedLine = order?.lines.find(l => l.sku === scanValue);

    // Sync manual states when SKU changes
    useEffect(() => {
        if (selectedLine?.dimensions) {
            setManualWeight(selectedLine.dimensions.weight || 0);
            setManualLength(selectedLine.dimensions.length || 0);
            setManualWidth(selectedLine.dimensions.width || 0);
            setManualHeight(selectedLine.dimensions.height || 0);
        } else {
            setManualWeight(0);
            setManualLength(0);
            setManualWidth(0);
            setManualHeight(0);
        }
    }, [scanValue, selectedLine]);

    const totalEstimatedWeight = receiveMode === 'UNIT'
        ? Number(quantity) * Number(manualWeight)
        : Number(lpnCount) * Number(manualWeight);

    const singleVolume = (Number(manualLength) * Number(manualWidth) * Number(manualHeight)) / 1000000;
    const totalEstimatedVolume = receiveMode === 'UNIT'
        ? Number(quantity) * singleVolume
        : Number(lpnCount) * singleVolume;

    // Auto-focus logic
    useEffect(() => {
        if (inputRef.current) {
            inputRef.current.focus();
        }
    }, [feedback, order, receiveMode]);

    // Derived State
    const totalUnits = order?.totalUnits || 0;
    const receivedUnits = order?.lines.reduce((acc, l) => acc + l.receivedQty, 0) || 0;
    const progress = totalUnits > 0 ? (receivedUnits / totalUnits) * 100 : 0;

    const handlePrintBatch = async (overrideIds?: string[]) => {
        const lpnIds = overrideIds || lastReceivedLpnIds;

        if (!lpnIds || lpnIds.length === 0) {
            console.warn("⚠️ Intento de impresión sin IDs de LPN.");
            setFeedback({ type: 'error', message: 'No hay LPNs recientes para imprimir.' });
            return;
        }

        try {
            if (mode === 'rfid') {
                console.log("📡 Solicitando ZPL Batch para IDs:", lpnIds);
                const response = await axios.post('http://localhost:5000/api/v1/printing/rfid/batch', { ids: lpnIds });
                const zpl = response.data;
                console.log("📝 ZPL Recibido (Longitud):", zpl.length);

                const targetPrinter = await zebraService.getDefaultPrinter();

                if (!targetPrinter) {
                    console.error("❌ No se encontró ninguna impresora Zebra.");
                    alert("⚠️ No se detectó ninguna impresora Zebra. Verifique que Browser Print esté corriendo.");
                    return;
                }

                console.log("🖨️ Enviando a:", targetPrinter.uid);
                await zebraService.printZpl(targetPrinter.uid, zpl);
                setFeedback({ type: 'success', message: 'Comando RFID enviado a la impresora.' });

                // STATE CLEANUP: Evitar impresiones fantasma
                setLastReceivedLpnIds([]);
                console.log("🧹 Lista de LPNs recientes vaciada tras impresión exitosa.");
            } else {
                console.log("📄 Generando etiquetas PDF (Standard)...");
                const url = `http://localhost:5000/api/v1/printing/batch?ids=${lpnIds.join(',')}&t=${Date.now()}`;
                setPrintUrl(url);
            }
        } catch (error: any) {
            console.error("❌ Error en flujo de impresión:", error);
            setFeedback({
                type: 'error',
                message: `Error al imprimir: ${error.message || 'Verifique la conexión.'}`
            });
        }
    };

    const handleReceive = async (e: React.FormEvent, autoPrintOverride?: boolean) => {
        e.preventDefault();
        if (!scanValue || !order) return;

        // Validation: Verify if SKU belongs to order
        const line = order.lines.find(l => l.sku === scanValue);
        if (!line) {
            setFeedback({ type: 'error', message: `El SKU '${scanValue}' no pertenece a esta orden.` });
            setScanValue('');
            return;
        }

        try {
            const params: any = {
                orderId: order.id,
                rawScan: scanValue,
                quantity: receiveMode === 'UNIT' ? Number(quantity) : (Number(lpnCount) * Number(unitsPerLpn)),
                weight: Number(manualWeight),
                length: Number(manualLength),
                width: Number(manualWidth),
                height: Number(manualHeight),
                isUnitMode: receiveMode === 'UNIT'
            };

            if (receiveMode === 'BULK') {
                params.lpnCount = Number(lpnCount);
                params.unitsPerLpn = Number(unitsPerLpn);
            }

            const response = await receiveLpn(params);
            const lpnIds = response.lpnIds as string[];
            if (lpnIds && lpnIds.length > 0) {
                console.log("✅ Recepción exitosa. IDs:", lpnIds);
                setLastReceivedLpnIds(lpnIds);

                // AUTO-TRIGGER LOGIC: Unified for Standard and RFID
                if (mode === 'rfid') {
                    console.log("🚀 Disparando impresión automática RFID...");
                    // SILENT AUTO-PROGRAMMING: Non-blocking attempt
                    setTimeout(() => {
                        handlePrintBatch(lpnIds).catch((err) => {
                            console.warn("Auto-RFID print failed - likely zebra service not ready.", err);
                        });
                    }, 500);
                } else {
                    // Standard PDF Printing (Legacy method)
                    const shouldPrint = autoPrintOverride ?? true;
                    if (shouldPrint) {
                        console.log("🚀 Generando PDF automático...");
                        setTimeout(() => {
                            const url = `http://localhost:5000/api/v1/printing/batch?ids=${lpnIds.join(',')}&t=${Date.now()}`;
                            setPrintUrl(url);
                        }, 800);
                    }
                }
            }

            const displayQty = receiveMode === 'UNIT' ? quantity : `${lpnCount} bultos x ${unitsPerLpn}`;
            setFeedback({ type: 'success', message: `Recibido: ${displayQty} de ${line.productName}` });
            setScanValue('');
            setQuantity(1);
        } catch (error) {
            setFeedback({ type: 'error', message: 'Error al recibir. Intente nuevamente.' });
        }
    };

    if (!order) {
        return (
            <div className="p-6 flex flex-col items-center justify-center min-h-screen bg-corp-base text-white">
                <p className="text-slate-500 mb-4 animate-pulse uppercase tracking-widest font-black">Cargando orden...</p>
                <button onClick={() => navigate('/inbound')} className="text-blue-400 font-bold hover:underline">Volver al Dashboard</button>
            </div>
        );
    }

    return (
        <div className="flex flex-col h-screen bg-corp-base text-white overflow-hidden selection:bg-blue-600/30">
            {/* Header Station */}
            <header className="bg-corp-nav/80 backdrop-blur-md border-b border-corp-secondary/50 p-5 shadow-2xl flex items-center justify-between z-10">
                <div className="flex items-center space-x-6">
                    <button
                        onClick={() => navigate('/inbound')}
                        className="p-3 hover:bg-corp-accent/40 rounded-2xl transition-all border border-corp-secondary/50 text-slate-400 hover:text-white group"
                    >
                        <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
                    </button>
                    <div>
                        <div className="flex items-center space-x-3">
                            <span className={`text-[10px] font-black ${mode === 'rfid' ? 'bg-blue-600' : 'bg-emerald-600'} text-white px-3 py-1 rounded-full uppercase tracking-widest shadow-lg shadow-black/40`}>
                                RECEPCIÓN {mode === 'rfid' ? 'RFID' : 'STANDARD'}
                            </span>
                            <h1 className="text-2xl font-black tracking-tighter uppercase whitespace-nowrap">{order.orderNumber}</h1>
                        </div>
                        <p className="text-xs text-slate-500 font-bold uppercase tracking-wider mt-1">PROVEEDOR: <span className="text-slate-300">{order.supplier}</span></p>
                    </div>
                </div>

                <div className="flex items-center space-x-4">
                    <div className="flex items-center space-x-4 bg-corp-base/50 px-4 py-2 rounded-xl border border-corp-secondary/30 shadow-inner">
                        <div className="text-right">
                            <p className="text-[10px] text-slate-500 font-black uppercase tracking-widest">PROGRESO TOTAL</p>
                            <p className="text-sm font-black text-emerald-400 font-mono">{Math.round(progress)}%</p>
                        </div>
                        <div className="w-40 h-2.5 bg-corp-nav rounded-full overflow-hidden border border-corp-secondary/50">
                            <div className="h-full bg-gradient-to-r from-blue-600 to-emerald-500 transition-all duration-1000" style={{ width: `${progress}%` }}></div>
                        </div>
                    </div>
                </div>
            </header>

            <main className="flex-1 overflow-y-auto p-8 custom-scrollbar bg-[radial-gradient(circle_at_top_right,_var(--tw-gradient-stops))] from-corp-accent/5 via-transparent to-transparent">
                <div className="max-w-7xl mx-auto">
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-10">

                        {/* Left: Input Section */}
                        <div className="lg:col-span-5 space-y-8 animate-in slide-in-from-left duration-500">
                            <div className="bg-corp-nav/40 backdrop-blur-md rounded-[2.5rem] p-10 border border-corp-secondary/50 shadow-2xl space-y-8">

                                {/* Mode Switch */}
                                <div className="flex p-1.5 bg-corp-nav/60 rounded-2xl border border-corp-secondary/40 shadow-inner">
                                    <button
                                        onClick={() => setReceiveMode('UNIT')}
                                        className={`flex-1 py-4 text-xs font-black uppercase tracking-[0.2em] rounded-xl transition-all ${receiveMode === 'UNIT' ? 'bg-corp-accent text-white shadow-xl shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                    >
                                        Recibo Suelto
                                    </button>
                                    <button
                                        onClick={() => setReceiveMode('BULK')}
                                        className={`flex-1 py-4 text-xs font-black uppercase tracking-[0.2em] rounded-xl transition-all ${receiveMode === 'BULK' ? 'bg-corp-accent text-white shadow-xl shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                    >
                                        Recibo Bulto
                                    </button>
                                </div>

                                <form onSubmit={handleReceive} className="space-y-8">
                                    <div className="space-y-4">
                                        <label className="block text-[10px] font-black text-slate-500 uppercase tracking-[0.3em] ml-2">
                                            ESCANEAR SKU O EAN
                                        </label>
                                        <div className="relative group">
                                            <input
                                                ref={inputRef}
                                                type="text"
                                                value={scanValue}
                                                onChange={(e) => setScanValue(e.target.value)}
                                                className="w-full text-3xl p-6 bg-corp-base/60 border-2 border-corp-secondary rounded-3xl focus:ring-4 focus:ring-corp-accent/20 focus:border-corp-accent transition-all font-mono uppercase text-center text-white placeholder:text-slate-700 shadow-inner"
                                                placeholder="WAITING FOR SCAN..."
                                            />
                                            <ScanLine className="absolute left-6 top-1/2 -translate-y-1/2 w-8 h-8 text-slate-600 group-focus-within:text-blue-400 transition-colors animate-pulse" />
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-1 gap-6">
                                        {receiveMode === 'UNIT' ? (
                                            <div className="animate-in zoom-in duration-300">
                                                <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 tracking-[0.2em] ml-2">
                                                    CANTIDAD
                                                </label>
                                                <input
                                                    type="text"
                                                    value={quantity}
                                                    onChange={(e) => setQuantity(e.target.value)}
                                                    className="w-full text-3xl p-5 bg-corp-base/40 border border-corp-secondary rounded-2xl focus:ring-2 focus:ring-corp-accent transition-all font-mono text-center text-white"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                        ) : (
                                            <div className="grid grid-cols-2 gap-4 animate-in zoom-in duration-300">
                                                <div>
                                                    <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 text-center tracking-widest">CANT. BULTOS</label>
                                                    <input
                                                        type="text"
                                                        value={lpnCount}
                                                        onChange={(e) => setLpnCount(e.target.value)}
                                                        className="w-full text-3xl p-5 bg-corp-base/40 border border-corp-secondary rounded-2xl focus:ring-2 focus:ring-corp-accent transition-all font-mono text-center text-white"
                                                        onFocus={(e) => e.target.select()}
                                                    />
                                                </div>
                                                <div>
                                                    <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 text-center tracking-widest">UNITS/BULTO</label>
                                                    <input
                                                        type="text"
                                                        value={unitsPerLpn}
                                                        onChange={(e) => setUnitsPerLpn(e.target.value)}
                                                        className="w-full text-3xl p-5 bg-corp-base/40 border border-corp-secondary rounded-2xl focus:ring-2 focus:ring-corp-accent transition-all font-mono text-center text-white"
                                                        onFocus={(e) => e.target.select()}
                                                    />
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {/* Editable Physical Attributes Section */}
                                    <div className="bg-corp-base/50 p-6 rounded-3xl border border-corp-secondary/30 space-y-4 animate-in fade-in duration-500">
                                        <div className="flex items-center justify-between mb-2">
                                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Atributos Físicos (Editables)</label>
                                        </div>

                                        <div className="grid grid-cols-4 gap-3">
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Peso (Kg)</label>
                                                <input
                                                    type="text"
                                                    value={manualWeight}
                                                    onChange={(e) => setManualWeight(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-blue-400 font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Largo</label>
                                                <input
                                                    type="text"
                                                    value={manualLength}
                                                    onChange={(e) => setManualLength(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-white font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Ancho</label>
                                                <input
                                                    type="text"
                                                    value={manualWidth}
                                                    onChange={(e) => setManualWidth(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-white font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Alto</label>
                                                <input
                                                    type="text"
                                                    value={manualHeight}
                                                    onChange={(e) => setManualHeight(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-white font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                        </div>
                                    </div>

                                    <div className="flex flex-col space-y-3">
                                        <button
                                            type="button"
                                            onClick={(e) => handleReceive(e, true)}
                                            disabled={isReceiving || !scanValue}
                                            className={`w-full py-6 rounded-3xl font-black text-xl shadow-2xl transform active:scale-95 transition-all flex items-center justify-center space-x-3 border
                                                    ${isReceiving ? 'bg-emerald-900/40 text-emerald-800 border-emerald-900/40 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-500 text-white shadow-emerald-900/20 border-emerald-400/20'}`}
                                        >
                                            {isReceiving && <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white"></div>}
                                            <Printer className="w-6 h-6" />
                                            <span>{isReceiving ? 'PROCESANDO...' : 'RECEPTAR E IMPRIMIR'}</span>
                                        </button>

                                        <button
                                            type="submit"
                                            disabled={isReceiving || !scanValue}
                                            className={`w-full py-4 rounded-2xl font-black text-sm shadow-xl transform active:scale-95 transition-all flex items-center justify-center space-x-3 border
                                                    ${isReceiving ? 'bg-slate-800 text-slate-600 border-slate-700 cursor-not-allowed' : 'bg-corp-base/60 hover:bg-slate-800 text-slate-400 shadow-black/40 border-corp-secondary'}`}
                                        >
                                            <span>SÓLO CONFIRMAR</span>
                                        </button>
                                    </div>

                                    {/* Weight & Volume Calculation Widgets */}
                                    <div className="grid grid-cols-2 gap-3">
                                        {Number(manualWeight) > 0 && (
                                            <div className="bg-blue-900/20 rounded-xl p-3 flex flex-col justify-between border border-blue-800/40 shadow-inner animate-in fade-in duration-700 h-24">
                                                <div className="flex items-center text-blue-300 mb-1">
                                                    <div className="p-1.5 bg-blue-900/40 rounded-lg mr-2 border border-blue-800/50">
                                                        <Calculator className="w-4 h-4" />
                                                    </div>
                                                    <span className="text-[9px] font-bold uppercase tracking-widest leading-tight">Peso<br />Total</span>
                                                </div>
                                                <div className="text-right self-end w-full truncate">
                                                    <span className="text-xl font-black text-white">{totalEstimatedWeight.toFixed(2)}</span>
                                                    <span className="text-[9px] font-black text-blue-400 ml-1 tracking-tighter">KG</span>
                                                </div>
                                            </div>
                                        )}

                                        {(Number(manualLength) > 0 && Number(manualWidth) > 0 && Number(manualHeight) > 0) && (
                                            <div className="bg-emerald-900/20 rounded-xl p-3 flex flex-col justify-between border border-emerald-800/40 shadow-inner animate-in fade-in duration-700 h-24">
                                                <div className="flex items-center text-emerald-300 mb-1">
                                                    <div className="p-1.5 bg-emerald-900/40 rounded-lg mr-2 border border-emerald-800/50">
                                                        <Package className="w-4 h-4" />
                                                    </div>
                                                    <span className="text-[9px] font-bold uppercase tracking-widest leading-tight">Vol.<br />Total</span>
                                                </div>
                                                <div className="text-right self-end w-full truncate">
                                                    <span className="text-xl font-black text-white">{totalEstimatedVolume.toFixed(4)}</span>
                                                    <span className="text-[9px] font-black text-emerald-400 ml-1 tracking-tighter">M³</span>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </form>
                            </div>

                            {/* Feedback Section */}
                            {feedback && (
                                <div className={`mt-6 p-6 rounded-3xl flex items-center justify-between animate-in fade-in slide-in-from-top-4 duration-500 border ${feedback.type === 'success'
                                    ? 'bg-emerald-950/30 border-emerald-500/30 text-emerald-400'
                                    : 'bg-rose-950/30 border-rose-500/30 text-rose-400'
                                    }`}>
                                    <div className="flex items-center space-x-4">
                                        {feedback.type === 'success' ? <CheckCircle2 className="w-6 h-6 shrink-0" /> : <AlertCircle className="w-6 h-6 shrink-0" />}
                                        <p className="text-xs font-black uppercase tracking-widest leading-relaxed">{feedback.message}</p>
                                    </div>

                                    {feedback.type === 'success' && lastReceivedLpnIds.length > 0 && (
                                        <button
                                            onClick={() => handlePrintBatch()}
                                            className={`px-8 py-3 rounded-2xl font-black text-[10px] uppercase tracking-widest transition-all shadow-xl flex items-center space-x-3 border ${mode === 'rfid'
                                                ? 'bg-blue-600 hover:bg-blue-500 text-white border-blue-400/30'
                                                : 'bg-emerald-600 hover:bg-emerald-500 text-white border-emerald-400/30'
                                                }`}
                                        >
                                            {mode === 'rfid' ? <Radio className="w-4 h-4" /> : <Printer className="w-4 h-4" />}
                                            <span>{mode === 'rfid' ? 'Programar Lote RFID' : 'Imprimir Lote'}</span>
                                        </button>
                                    )}
                                </div>
                            )}

                            {/* SKU Info Card (Moved inside left column to avoid grid break) */}
                            {selectedLine && (
                                <div className="bg-corp-nav/40 backdrop-blur-md rounded-[2.5rem] p-10 border border-corp-secondary/50 shadow-2xl space-y-8 animate-in zoom-in duration-500">
                                    <div className="flex items-center space-x-6">
                                        <div className="p-5 bg-corp-base rounded-[2rem] border border-corp-secondary/50 text-blue-400 shadow-inner">
                                            <Package className="w-10 h-10" />
                                        </div>
                                        <div>
                                            <h3 className="text-3xl font-black tracking-tighter text-white">{selectedLine.productName}</h3>
                                            <div className="flex items-center space-x-3 mt-2">
                                                <p className="text-[10px] font-black text-slate-500 uppercase tracking-[0.3em]">{selectedLine.sku}</p>
                                                <span className="text-slate-700">|</span>
                                                <div className="flex items-center space-x-2 bg-blue-500/10 px-3 py-1 rounded-lg border border-blue-500/20 shadow-inner group-hover:border-blue-500/40 transition-all">
                                                    <span className="text-[9px] font-black text-blue-400 uppercase tracking-widest">
                                                        PESO: {selectedLine.dimensions?.weight || 0}kg |
                                                        DIM: {selectedLine.dimensions?.length || 0}x{selectedLine.dimensions?.width || 0}x{selectedLine.dimensions?.height || 0}cm |
                                                        VOL: {((selectedLine.dimensions?.length || 0) * (selectedLine.dimensions?.width || 0) * (selectedLine.dimensions?.height || 0) / 1000000).toFixed(4)}m³
                                                    </span>
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-2 gap-6">
                                        <div className="bg-corp-base/40 p-6 rounded-3xl border border-corp-secondary/30 shadow-inner">
                                            <div className="flex items-center space-x-3 mb-3">
                                                <Calculator className="w-4 h-4 text-emerald-400" />
                                                <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">Peso Unitario</span>
                                            </div>
                                            <p className="text-2xl font-black text-white font-mono">{selectedLine.dimensions?.weight || 0} <span className="text-[10px] text-slate-500">kg</span></p>
                                        </div>
                                        <div className="bg-corp-base/40 p-6 rounded-3xl border border-corp-secondary/30 shadow-inner">
                                            <div className="flex items-center space-x-3 mb-3">
                                                <ScanLine className="w-4 h-4 text-blue-400" />
                                                <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">Dimensiones</span>
                                            </div>
                                            <p className="text-lg font-black text-white font-mono">
                                                {selectedLine.dimensions?.length || 0}x{selectedLine.dimensions?.width || 0}x{selectedLine.dimensions?.height || 0}
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>

                        {/* Right: Items List */}
                        <div className="lg:col-span-7 animate-in slide-in-from-right duration-500">
                            <div className="bg-corp-nav/40 border border-corp-secondary/50 rounded-[2.5rem] shadow-2xl overflow-hidden h-full flex flex-col">
                                <div className="p-8 bg-corp-accent/10 border-b border-corp-secondary/30 flex items-center justify-between">
                                    <div>
                                        <h3 className="font-black text-white text-md tracking-[0.2em] uppercase">LÍNEAS DE REQUISICIÓN</h3>
                                        <p className="text-xs text-slate-500 font-bold mt-1 uppercase tracking-widest">CONTROL DE AVANCE EN TIEMPO REAL</p>
                                    </div>
                                    <div className="bg-corp-base/50 px-5 py-2 rounded-xl border border-corp-secondary/30">
                                        <span className="text-[10px] font-black text-blue-400 uppercase tracking-widest">{order.lines.length} SKUS TOTALES</span>
                                    </div>
                                </div>
                                <ul className="divide-y divide-corp-secondary/10 flex-1 overflow-y-auto custom-scrollbar">
                                    {order.lines.map((line, index) => {
                                        const isComplete = line.receivedQty >= line.expectedQty;
                                        return (
                                            <li key={`${line.sku}-${index}`} className={`p-6 flex items-center justify-between transition-all ${isComplete ? 'bg-corp-base/20 opacity-40' : 'hover:bg-corp-accent/5'}`}>
                                                <div className="flex items-center space-x-6">
                                                    <div className={`p-4 rounded-2xl border transition-colors ${isComplete ? 'bg-slate-800 border-slate-700' : 'bg-corp-base border-corp-secondary text-blue-400'}`}>
                                                        <Package className="w-7 h-7" />
                                                    </div>
                                                    <div>
                                                        <p className="font-black text-white text-lg tracking-tight">{line.sku}</p>
                                                        <p className="text-xs text-slate-400 font-bold uppercase truncate max-w-[250px]">{line.productName}</p>
                                                    </div>
                                                </div>
                                                <div className="text-right space-y-2">
                                                    <div className="flex items-center justify-end space-x-4">
                                                        <div className="text-right">
                                                            <span className={`text-2xl font-black font-mono ${isComplete ? 'text-emerald-400' : 'text-white'}`}>
                                                                {line.receivedQty}
                                                            </span>
                                                            <span className="text-slate-600 font-black mx-2">/</span>
                                                            <span className="text-sm font-black text-slate-500">
                                                                {line.expectedQty}
                                                            </span>
                                                        </div>
                                                        {isComplete && <CheckCircle2 className="w-6 h-6 text-emerald-500" />}
                                                    </div>
                                                    <div className="w-32 h-1.5 bg-corp-base rounded-full overflow-hidden border border-corp-secondary/20">
                                                        <div
                                                            className={`h-full transition-all duration-500 ${isComplete ? 'bg-emerald-500' : 'bg-blue-600'}`}
                                                            style={{ width: `${Math.min(100, (line.receivedQty / line.expectedQty) * 100)}%` }}
                                                        ></div>
                                                    </div>
                                                </div>
                                            </li>
                                        );
                                    })}
                                </ul>
                            </div>
                        </div>
                    </div>
                </div>
            </main>

            {/* Hidden Iframe for direct printing */}
            {printUrl && (
                <iframe
                    src={printUrl}
                    style={{ display: 'none' }}
                    onLoad={() => {
                        // Reset the URL after loading so it can be re-triggered
                        setTimeout(() => setPrintUrl(null), 1000);
                    }}
                />
            )}
        </div>
    );
};

export default ReceiveStation;
