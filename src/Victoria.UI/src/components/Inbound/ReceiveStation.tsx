import React, { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { ArrowLeft, Package, CheckCircle2, AlertCircle, ScanLine, Calculator, Printer, Radio, Camera, Zap } from 'lucide-react';
import { useInbound } from '../../hooks/useInbound';
import { zebraService } from '../../services/zebra.service';
import { PackagingUpdateModal } from './PackagingUpdateModal';
import api from '../../api/axiosConfig';

interface ReceiveStationProps {
    mode: 'rfid' | 'standard';
}

const ReceiveStation: React.FC<ReceiveStationProps> = ({ mode: rfidMode }) => {
    const { orderId, workingMode } = useParams<{ orderId: string, workingMode: 'standard' | 'crossdock' }>();
    const navigate = useNavigate();
    const { orders, receiveLpn, isReceiving } = useInbound();

    // State
    const [isPhotoSample, setIsPhotoSample] = useState(false);
    const [scanValue, setScanValue] = useState('');
    const [lpnCount, setLpnCount] = useState<number | string>(1);
    const [unitsPerLpn, setUnitsPerLpn] = useState<number | string>(1);
    const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
    const [lastReceivedLpnIds, setLastReceivedLpnIds] = useState<string[]>([]);
    const [printUrl, setPrintUrl] = useState<string | null>(null);
    const [showPhotoWizard, setShowPhotoWizard] = useState(false);
    const [selectedSkuForPhoto, setSelectedSkuForPhoto] = useState<string | null>(null);

    // Editable Physical Attributes States (Modified to string to solve backspace bug)
    const [manualWeight, setManualWeight] = useState<number | string>(0);
    const [manualLength, setManualLength] = useState<number | string>(0);
    const [manualWidth, setManualWidth] = useState<number | string>(0);
    const [manualHeight, setManualHeight] = useState<number | string>(0);

    // Packaging Intelligence State
    const [selectedPkgId, setSelectedPkgId] = useState<number | 'manual'>('manual');
    const [showUpdateModal, setShowUpdateModal] = useState(false);
    const [pendingParams, setPendingParams] = useState<any>(null);

    const inputRef = useRef<HTMLInputElement>(null);

    // Find Order
    const order = orders?.find(o => o.id === orderId || o.orderNumber === orderId);

    // Calc Peso Estimado
    // Calc Peso Estimado
    const selectedLine = order?.lines.find((l: any) => l.sku === scanValue);

    // Sync manual states and photo sample logic
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

        // Auto-Detect Packaging (Odoo Bulk Logic)
        if (selectedLine?.packagings && selectedLine.packagings.length > 0) {
            // Prioritize the first packaging (usually the Bulk definition)
            const defaultPkg = selectedLine.packagings[0];
            setSelectedPkgId(defaultPkg.odooId);
        } else {
            setSelectedPkgId('manual');
        }

        // Auto-Detect Photo Requirement (FORCE TRUE if required and NOT yet received)
        if (selectedLine?.requiresSample && !selectedLine?.sampleReceived && workingMode === 'standard') {
            setIsPhotoSample(true);
            setUnitsPerLpn(1);
            setLpnCount(1);
            setSelectedPkgId('manual');
        } else {
            setIsPhotoSample(false);
        }
    }, [scanValue, selectedLine, workingMode]);

    // Packaging Auto-Fill logic
    useEffect(() => {
        if (selectedPkgId !== 'manual' && selectedLine?.packagings) {
            const pkg = selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId);
            if (pkg) {
                setUnitsPerLpn(pkg.qty);
                setManualWeight(pkg.weight);
                setManualLength(pkg.length);
                setManualWidth(pkg.width);
                setManualHeight(pkg.height);
            }
        }
    }, [selectedPkgId, selectedLine]);

    // Lock parameters when Photo Sample is active
    useEffect(() => {
        if (isPhotoSample) {
            setUnitsPerLpn(1);
            setLpnCount(1);
        }
    }, [isPhotoSample]);

    const totalEstimatedWeight = Number(lpnCount) * Number(unitsPerLpn) * Number(manualWeight);

    const singleVolume = (Number(manualLength) * Number(manualWidth) * Number(manualHeight)) / 1000000;
    const totalEstimatedVolume = Number(lpnCount) * Number(unitsPerLpn) * singleVolume;

    // Auto-focus logic
    useEffect(() => {
        if (inputRef.current) {
            inputRef.current.focus();
        }
    }, [feedback, order, workingMode]);

    // Derived State
    const totalUnits = order?.totalUnits || 0;
    const receivedUnits = order?.lines.reduce((acc: number, l: any) => acc + l.receivedQty, 0) || 0;
    const progress = totalUnits > 0 ? (receivedUnits / totalUnits) * 100 : 0;

    const handlePrintBatch = async (overrideIds?: string[]) => {
        const lpnIds = overrideIds || lastReceivedLpnIds;

        if (!lpnIds || lpnIds.length === 0) {
            console.warn("⚠️ Intento de impresión sin IDs de LPN.");
            setFeedback({ type: 'error', message: 'No hay LPNs recientes para imprimir.' });
            return;
        }

        try {
            if (rfidMode === 'rfid') {
                console.log("📡 Solicitando ZPL Batch para IDs:", lpnIds);
                const response = await axios.post('/api/v1/printing/rfid/batch', { ids: lpnIds });
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
                const url = `/api/v1/printing/batch?ids=${lpnIds.join(',')}&t=${Date.now()}`;
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

    const handleReceive = async (e: React.FormEvent, autoPrintOverride: boolean = true) => {
        e.preventDefault();
        if (!scanValue || !order) return;

        // Validation: Verify if SKU belongs to order
        const line = order.lines.find((l: any) => l.sku === scanValue);
        if (!line) {
            setFeedback({ type: 'error', message: `El SKU '${scanValue}' no pertenece a esta orden.` });
            setScanValue('');
            return;
        }

        const params: any = {
            orderId: order.id,
            rawScan: scanValue,
            quantity: Number(lpnCount) * Number(unitsPerLpn),
            lpnCount: Number(lpnCount),
            unitsPerLpn: Number(unitsPerLpn),
            weight: Number(manualWeight),
            length: Number(manualLength),
            width: Number(manualWidth),
            height: Number(manualHeight),
            isPhotoSample: isPhotoSample
        };

        if (workingMode === 'crossdock') {
            params.isCrossdock = true;
        }

        // SMART SAVE DETECTION
        if (selectedPkgId !== 'manual' && line.packagings) {
            const pkg = line.packagings.find((p: any) => p.odooId === selectedPkgId);
            if (pkg) {
                const hasDiff = pkg.qty !== Number(unitsPerLpn) ||
                    pkg.weight !== Number(manualWeight) ||
                    pkg.length !== Number(manualLength) ||
                    pkg.width !== Number(manualWidth) ||
                    pkg.height !== Number(manualHeight);

                if (hasDiff) {
                    setPendingParams({ ...params, autoPrintOverride });
                    setShowUpdateModal(true);
                    return; // Wait for modal decision
                }
            }
        }

        await executeReception(params, autoPrintOverride);
    };

    const executeReception = async (params: any, autoPrintOverride: boolean) => {
        try {
            const line = order?.lines.find((l: any) => l.sku === params.rawScan);

            const resetForm = () => {
                setScanValue('');
                setLpnCount(1);
                setUnitsPerLpn(1);
                setManualWeight(0);
                setManualLength(0);
                setManualWidth(0);
                setManualHeight(0);
                setSelectedPkgId('manual');
                if (inputRef.current) inputRef.current.focus();
            };

            const response = await receiveLpn(params);
            const lpnIds = response.lpnIds as string[];
            if (lpnIds && lpnIds.length > 0) {
                console.log("✅ Recepción exitosa. IDs:", lpnIds);
                setLastReceivedLpnIds(lpnIds);

                const shouldPrint = autoPrintOverride ?? false;

                if (shouldPrint) {
                    if (rfidMode === 'rfid') {
                        setTimeout(() => {
                            handlePrintBatch(lpnIds).catch((err) => {
                                console.warn("Auto-RFID print failed.", err);
                            });
                        }, 500);
                    } else {
                        setTimeout(() => {
                            const url = `/api/v1/printing/batch?ids=${lpnIds.join(',')}&t=${Date.now()}`;
                            setPrintUrl(url);
                        }, 800);
                    }
                }
            }

            setFeedback({ type: 'success', message: `Recibido: ${params.lpnCount} LPN(s) de ${line.productName}` });
            resetForm();
        } catch (error: any) {
            const errorData = error.response?.data;
            const errorMsg = (typeof errorData === 'string' ? errorData : errorData?.error) || error.message || '';

            if (errorMsg.includes('[GOLDEN-SAMPLE]')) {
                setSelectedSkuForPhoto(params.rawScan);
                setShowPhotoWizard(true);
            } else {
                setFeedback({ type: 'error', message: 'Reception failed. Please try again.' });
            }
        }
    };

    const handlePackagingUpdateConfirm = async (action: 'receive_only' | 'update_odoo' | 'create_new') => {
        if (!pendingParams) return;

        try {
            if (action === 'update_odoo') {
                await api.put(`products/${pendingParams.rawScan}/packaging/${selectedPkgId}`, {
                    name: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.name,
                    qty: Number(unitsPerLpn),
                    weight: Number(manualWeight),
                    length: Number(manualLength),
                    width: Number(manualWidth),
                    height: Number(manualHeight)
                });
            } else if (action === 'create_new') {
                await api.post(`products/${pendingParams.rawScan}/packaging`, {
                    name: `EMP ${unitsPerLpn} UN`, // Auto-naming
                    qty: Number(unitsPerLpn),
                    weight: Number(manualWeight),
                    length: Number(manualLength),
                    width: Number(manualWidth),
                    height: Number(manualHeight)
                });
            }

            // Proceed with reception
            await executeReception(pendingParams, pendingParams.autoPrintOverride);
        } catch (err) {
            console.error("Error in Smart Save:", err);
            alert("Error al procesar Smart Save. Se procederá solo con la recepción.");
            await executeReception(pendingParams, pendingParams.autoPrintOverride);
        } finally {
            setShowUpdateModal(false);
            setPendingParams(null);
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
                            <span className={`text-[10px] font-black ${rfidMode === 'rfid' ? 'bg-blue-600' : 'bg-emerald-600'} text-white px-3 py-1 rounded-full uppercase tracking-widest shadow-lg shadow-black/40`}>
                                {rfidMode === 'rfid' ? 'RFID' : 'STANDARD'} {workingMode?.toUpperCase()}
                            </span>
                            <h1 className="text-2xl font-black tracking-tighter uppercase whitespace-nowrap">{order.orderNumber}</h1>
                        </div>
                        <p className="text-xs text-slate-500 font-bold uppercase tracking-wider mt-1">SUPPLIER: <span className="text-slate-300">{order.supplier}</span></p>
                    </div>
                </div>

                <div className="flex items-center space-x-4">
                    <div className="flex items-center space-x-4 bg-corp-base/50 px-4 py-2 rounded-xl border border-corp-secondary/30 shadow-inner">
                        <div className="text-right">
                            <p className="text-[10px] text-slate-500 font-black uppercase tracking-widest">TOTAL PROGRESS</p>
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

                                {/* Mode Switch - Refactored to Standard/Crossdock Tabs */}
                                <div className="flex p-1.5 bg-corp-nav/60 rounded-2xl border border-corp-secondary/40 shadow-inner">
                                    <button
                                        onClick={() => navigate(`/inbound/receive/standard/${orderId}`)}
                                        className={`flex-1 py-4 text-[10px] font-black uppercase tracking-[0.2em] rounded-xl transition-all ${workingMode === 'standard' ? 'bg-corp-accent text-white shadow-xl shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                    >
                                        RECEPCIÓN ESTÁNDAR
                                    </button>
                                    <button
                                        onClick={() => navigate(`/inbound/receive/crossdock/${orderId}`)}
                                        className={`flex-1 py-4 text-[10px] font-black uppercase tracking-[0.2em] rounded-xl transition-all ${workingMode === 'crossdock' ? 'bg-blue-600 text-white shadow-xl shadow-black/40' : 'text-slate-500 hover:text-slate-300'}`}
                                    >
                                        PLANNED CROSSDOCK
                                    </button>
                                </div>

                                <form onSubmit={handleReceive} className="space-y-8">
                                    <div className="pt-4 border-t border-corp-secondary/30">
                                        {workingMode === 'crossdock' && (
                                            <div className="mb-6 p-4 bg-blue-900/20 border border-blue-500/30 rounded-2xl animate-in fade-in slide-in-from-top-2">
                                                <div className="flex items-center gap-3">
                                                    <Zap className="w-5 h-5 text-blue-400 fill-blue-400" />
                                                    <div>
                                                        <p className="text-[10px] font-black text-blue-400 uppercase tracking-widest">DESTINO CROSSDOCK</p>
                                                        <p className="text-lg font-black text-white uppercase tracking-tight">{order.targetOutboundOrder || 'SIN DESTINO'}</p>
                                                    </div>
                                                </div>
                                            </div>
                                        )}

                                        <p className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em] mb-4 text-center">
                                            CONTROL DE ESCANEO
                                        </p>
                                        <div className="grid grid-cols-1 gap-4">
                                            <div className="relative group col-span-full">
                                                <label className="block text-[10px] font-black text-slate-500 uppercase tracking-[0.3em] ml-2 mb-2">
                                                    SCAN SKU OR EAN
                                                </label>
                                                <div className="relative">
                                                    <input
                                                        ref={inputRef}
                                                        type="text"
                                                        value={scanValue}
                                                        onChange={(e) => setScanValue(e.target.value)}
                                                        className="w-full text-3xl pl-16 pr-6 py-6 bg-slate-900/60 border-2 border-corp-secondary rounded-3xl focus:ring-4 focus:ring-blue-500/20 focus:border-blue-500 transition-all font-mono uppercase text-white placeholder:text-slate-500 shadow-inner"
                                                        placeholder="WAITING FOR SCAN..."
                                                    />
                                                    <ScanLine className="absolute left-6 top-1/2 -translate-y-1/2 w-8 h-8 text-slate-500 group-focus-within:text-blue-400 transition-colors animate-pulse" />
                                                </div>

                                                {/* Compact Item Details - Moved from bottom card */}
                                                {selectedLine && (
                                                    <div className="mt-4 p-5 bg-corp-base/30 rounded-2xl border border-corp-secondary/20 animate-in fade-in slide-in-from-top-2 duration-300">
                                                        <div className="flex flex-col space-y-2">
                                                            <div className="flex items-baseline flex-wrap gap-x-2">
                                                                <span className="text-xl font-black text-corp-accent uppercase leading-tight">[{selectedLine.sku}]</span>
                                                                <span className="text-xl font-black text-white leading-tight opacity-90">{selectedLine.productName}</span>
                                                            </div>
                                                            <div className="flex items-center gap-3">
                                                                <div className="flex items-center gap-2 bg-blue-500/10 px-3 py-1.5 rounded-lg border border-blue-500/20">
                                                                    <span className="text-[10px] font-black text-blue-400 uppercase tracking-widest leading-none flex items-center gap-2">
                                                                        <span>{selectedLine.brand || 'NO BRAND'}</span>
                                                                        {(selectedLine.sides && selectedLine.sides !== 'N/A') && (
                                                                            <>
                                                                                <span className="text-blue-900/40">|</span>
                                                                                <span>{selectedLine.sides}</span>
                                                                            </>
                                                                        )}
                                                                        {(selectedLine.category && selectedLine.category !== 'SIN CATEGORÍA' && selectedLine.category !== '') && (
                                                                            <>
                                                                                <span className="text-blue-900/40">|</span>
                                                                                <span className="text-white/60">{selectedLine.category}</span>
                                                                            </>
                                                                        )}
                                                                    </span>
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </div>
                                                )}
                                            </div>

                                            {selectedLine?.packagings && selectedLine.packagings.length > 0 && (
                                                <div className="col-span-full animate-in slide-in-from-top-2 duration-300">
                                                    <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 tracking-[0.2em] ml-2">
                                                        Seleccionar Empaque (Odoo)
                                                    </label>
                                                    <div className="grid grid-cols-1 gap-2">
                                                        {selectedLine.packagings.map((pkg: any) => (
                                                            <button
                                                                key={pkg.odooId}
                                                                type="button"
                                                                onClick={() => setSelectedPkgId(pkg.odooId)}
                                                                className={`flex items-center justify-between p-4 rounded-2xl border-2 transition-all ${selectedPkgId === pkg.odooId ? 'bg-indigo-600/20 border-indigo-500 shadow-lg shadow-indigo-500/10' : 'bg-slate-900/40 border-corp-secondary/50 hover:border-slate-500'}`}
                                                            >
                                                                <div className="flex items-center gap-3">
                                                                    <Package className={`w-5 h-5 ${selectedPkgId === pkg.odooId ? 'text-indigo-400' : 'text-slate-600'}`} />
                                                                    <div className="text-left">
                                                                        <p className="text-sm font-black text-white uppercase tracking-tight">{pkg.name}</p>
                                                                        <p className="text-[10px] text-slate-500 font-bold uppercase tracking-widest">{pkg.qty} UNIDADES</p>
                                                                    </div>
                                                                </div>
                                                                {selectedPkgId === pkg.odooId && <CheckCircle2 className="w-5 h-5 text-indigo-400" />}
                                                            </button>
                                                        ))}
                                                        <button
                                                            type="button"
                                                            onClick={() => setSelectedPkgId('manual')}
                                                            className={`flex items-center justify-between p-4 rounded-2xl border-2 transition-all ${selectedPkgId === 'manual' ? 'bg-slate-700/40 border-slate-500' : 'bg-slate-900/40 border-corp-secondary/50 hover:border-slate-600'}`}
                                                        >
                                                            <div className="flex items-center gap-3">
                                                                <Calculator className={`w-5 h-5 ${selectedPkgId === 'manual' ? 'text-slate-300' : 'text-slate-600'}`} />
                                                                <span className="text-sm font-black text-slate-400 uppercase tracking-tight">Ingreso Manual / Genérico</span>
                                                            </div>
                                                            {selectedPkgId === 'manual' && <CheckCircle2 className="w-5 h-5 text-slate-400" />}
                                                        </button>
                                                    </div>
                                                </div>
                                            )}
                                        </div>
                                    </div>

                                    <div className="grid grid-cols-1 gap-6">
                                        <div className="grid grid-cols-2 gap-4 animate-in zoom-in duration-300">
                                            <div className="relative group">
                                                <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 tracking-[0.2em] ml-2">
                                                    QTY / LPN
                                                </label>
                                                <input
                                                    type="text"
                                                    value={unitsPerLpn}
                                                    onChange={(e) => setUnitsPerLpn(e.target.value)}
                                                    disabled={isPhotoSample}
                                                    className={`w-full text-4xl py-6 bg-slate-900/60 border-2 rounded-3xl focus:ring-4 focus:ring-blue-500/20 focus:border-blue-500 transition-all font-mono text-center font-black shadow-inner ${isPhotoSample ? 'border-amber-500/50 text-amber-500' : 'border-corp-secondary text-white'}`}
                                                    onFocus={(e) => e.target.select()}
                                                />
                                                {isPhotoSample && (
                                                    <div className="absolute right-4 top-1/2 -translate-y-1/2 -mt-1 p-2 bg-amber-500/10 rounded-lg border border-amber-500/30">
                                                        <Camera className="w-5 h-5 text-amber-500" />
                                                    </div>
                                                )}
                                            </div>

                                            <div className="relative group">
                                                <label className="block text-[10px] font-black text-slate-500 uppercase mb-2 tracking-[0.2em] ml-2">
                                                    LPNs (BULKS)
                                                </label>
                                                <input
                                                    type="text"
                                                    value={lpnCount}
                                                    onChange={(e) => setLpnCount(e.target.value)}
                                                    disabled={isPhotoSample}
                                                    className={`w-full text-4xl py-6 bg-slate-900/60 border-2 rounded-3xl focus:ring-4 focus:ring-blue-500/20 focus:border-blue-500 transition-all font-mono text-center font-black shadow-inner ${isPhotoSample ? 'border-amber-500/50 text-amber-500' : 'border-corp-secondary text-white'}`}
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                        </div>

                                        {workingMode === 'standard' && (
                                            <div
                                                className={`p-6 rounded-3xl border flex items-center justify-between group transition-all ${selectedLine?.sampleReceived ? 'bg-slate-800/50 border-slate-700/50 cursor-not-allowed grayscale' : 'bg-corp-base/50 border-corp-secondary/30 cursor-pointer hover:border-amber-500/40'}`}
                                                title={selectedLine?.sampleReceived ? "Muestra ya recibida" : ""}
                                                onClick={() => !selectedLine?.sampleReceived && setIsPhotoSample(!isPhotoSample)}>
                                                <div className="flex items-center gap-4">
                                                    <div className={`p-3 rounded-xl transition-all ${isPhotoSample ? 'bg-amber-500/20 text-amber-500' : 'bg-slate-800 text-slate-500'}`}>
                                                        <Camera className="w-5 h-5" />
                                                    </div>
                                                    <div>
                                                        <p className="text-xs font-black text-white uppercase tracking-wider">PHOTO-STATION (Muestra)</p>
                                                        <p className="text-[10px] text-slate-500 font-bold uppercase tracking-widest">
                                                            {selectedLine?.sampleReceived ? "Muestra ya en estación" : "Desviar unidad para control de calidad"}
                                                        </p>
                                                    </div>
                                                </div>
                                                <div className={`w-12 h-6 rounded-full transition-all relative ${isPhotoSample ? 'bg-amber-600' : 'bg-slate-700'}`}>
                                                    <div className={`absolute top-1 w-4 h-4 bg-white rounded-full transition-all ${isPhotoSample ? 'right-1' : 'left-1'}`} />
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {/* Editable Physical Attributes Section */}
                                    <div className="bg-corp-base/50 p-6 rounded-3xl border border-corp-secondary/30 space-y-4 animate-in fade-in duration-500">
                                        <div className="flex items-center justify-between mb-2">
                                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-widest ml-1">Physical Attributes (Editable)</label>
                                        </div>

                                        <div className="grid grid-cols-4 gap-3">
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Weight (Kg)</label>
                                                <input
                                                    type="text"
                                                    value={manualWeight}
                                                    onChange={(e) => setManualWeight(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-blue-400 font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Length</label>
                                                <input
                                                    type="text"
                                                    value={manualLength}
                                                    onChange={(e) => setManualLength(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-white font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Width</label>
                                                <input
                                                    type="text"
                                                    value={manualWidth}
                                                    onChange={(e) => setManualWidth(e.target.value)}
                                                    className="w-full p-2.5 bg-slate-900 border-2 border-corp-secondary rounded-xl text-lg font-black text-white font-mono text-center focus:border-blue-500 focus:ring-2 focus:ring-blue-500/20 transition-all shadow-2xl"
                                                    onFocus={(e) => e.target.select()}
                                                />
                                            </div>
                                            <div>
                                                <label className="block text-[8px] font-black text-slate-500 uppercase mb-1 text-center font-mono tracking-tighter">Height</label>
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

                                    <div className="flex flex-col">
                                        <button
                                            type="submit"
                                            disabled={isReceiving || !scanValue}
                                            className={`w-full py-6 rounded-3xl font-black text-xl shadow-2xl transform active:scale-95 transition-all flex items-center justify-center space-x-3 border
                                                    ${isReceiving ? 'bg-emerald-900/40 text-emerald-800 border-emerald-900/40 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-500 text-white shadow-emerald-900/20 border-emerald-400/20'}`}
                                        >
                                            {isReceiving ? (
                                                <div className="w-8 h-8 border-4 border-white/30 border-t-white rounded-full animate-spin" />
                                            ) : (
                                                <>
                                                    <CheckCircle2 className="w-6 h-6" />
                                                    <span>RECEIVE</span>
                                                </>
                                            )}
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
                                                    <span className="text-[9px] font-bold uppercase tracking-widest leading-tight">Total<br />Weight</span>
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
                                                    <span className="text-[9px] font-bold uppercase tracking-widest leading-tight">Total<br />Volume</span>
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
                                            className={`px-8 py-3 rounded-2xl font-black text-[10px] uppercase tracking-widest transition-all shadow-xl flex items-center space-x-3 border ${rfidMode === 'rfid'
                                                ? 'bg-blue-600 hover:bg-blue-500 text-white border-blue-400/30'
                                                : 'bg-emerald-600 hover:bg-emerald-500 text-white border-emerald-400/30'
                                                }`}
                                        >
                                            {rfidMode === 'rfid' ? <Radio className="w-4 h-4" /> : <Printer className="w-4 h-4" />}
                                            <span>{rfidMode === 'rfid' ? 'Program RFID Batch' : 'Print Batch'}</span>
                                        </button>
                                    )}
                                </div>
                            )}

                        </div>

                        {/* Right: Items List */}
                        <div className="lg:col-span-7 animate-in slide-in-from-right duration-500">
                            <div className="bg-corp-nav/40 border border-corp-secondary/50 rounded-[2.5rem] shadow-2xl overflow-hidden h-full flex flex-col">
                                <div className="p-8 bg-corp-accent/10 border-b border-corp-secondary/30 flex items-center justify-between">
                                    <div>
                                        <h3 className="font-black text-white text-md tracking-[0.2em] uppercase">REQUISITION LINES</h3>
                                        <p className="text-xs text-slate-500 font-bold mt-1 uppercase tracking-widest">REAL-TIME PROGRESS CONTROL</p>
                                    </div>
                                    <div className="bg-corp-base/50 px-5 py-2 rounded-xl border border-corp-secondary/30">
                                        <span className="text-[10px] font-black text-blue-400 uppercase tracking-widest">{order.lines.length} TOTAL SKUS</span>
                                    </div>
                                </div>
                                <ul className="divide-y divide-corp-secondary/10 flex-1 overflow-y-auto custom-scrollbar">
                                    {order.lines.map((line: any, index: number) => {
                                        const isComplete = line.receivedQty >= line.expectedQty;
                                        return (
                                            <li key={`${line.sku}-${index}`} className={`p-6 flex items-center justify-between transition-all ${isComplete ? 'bg-corp-base/20 opacity-40' : 'hover:bg-corp-accent/5'}`}>
                                                <div className="flex items-center space-x-6">
                                                    <div className={`p-4 rounded-2xl border transition-colors ${isComplete ? 'bg-slate-800 border-slate-700' : 'bg-corp-base border-corp-secondary text-blue-400'}`}>
                                                        <Package className="w-7 h-7" />
                                                    </div>
                                                    <div>
                                                        <div className="flex items-center gap-2">
                                                            <p className="font-black text-white text-lg tracking-tight">{line.sku}</p>
                                                            {line.requiresSample && !line.sampleReceived && (
                                                                <div className="flex items-center gap-1 px-2 py-0.5 bg-amber-500/10 border border-amber-500/20 rounded-md">
                                                                    <Camera className="w-3 h-3 text-amber-500" />
                                                                    <span className="text-[8px] font-black text-amber-500 uppercase tracking-tighter">Photo Required</span>
                                                                </div>
                                                            )}
                                                            {line.sampleReceived && (
                                                                <div className="flex items-center gap-1 px-2 py-0.5 bg-emerald-500/10 border border-emerald-500/20 rounded-md">
                                                                    <CheckCircle2 className="w-3 h-3 text-emerald-500" />
                                                                    <Camera className="w-3 h-3 text-emerald-500" />
                                                                    <span className="text-[8px] font-black text-emerald-500 uppercase tracking-tighter">✅📷 Muestra Recibida</span>
                                                                </div>
                                                            )}
                                                        </div>
                                                        <div className="text-[10px] text-slate-400 font-bold uppercase flex items-center gap-1.5 min-w-0 max-w-[400px]">
                                                            <div className="flex items-center gap-1 bg-corp-base/40 px-2 py-0.5 rounded border border-corp-secondary/30 text-blue-400 shrink-0">
                                                                <span>{line.brand || 'NO BRAND'}</span>
                                                                {(line.sides && line.sides !== 'N/A') && (
                                                                    <>
                                                                        <span className="text-blue-900/20">|</span>
                                                                        <span>{line.sides}</span>
                                                                    </>
                                                                )}
                                                                {(line.category && line.category !== '' && line.category !== 'N/A') && (
                                                                    <>
                                                                        <span className="text-blue-900/20">|</span>
                                                                        <span className="text-white/60">{line.category}</span>
                                                                    </>
                                                                )}
                                                            </div>
                                                            <span className="truncate opacity-80">
                                                                {(line.productName || '').replace(/^\[.*?\]\s*/, '')}
                                                            </span>
                                                        </div>
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
                                                        <div className="text-right">
                                                            <span className="text-[10px] font-bold text-slate-500 uppercase block">RECEIVED</span>
                                                            <span className={`text-sm font-black font-mono ${line.receivedQty >= line.expectedQty ? 'text-emerald-400' : 'text-white'}`}>
                                                                {line.receivedQty} <span className="text-slate-500 text-[10px]">/ {line.expectedQty}</span>
                                                            </span>
                                                        </div>
                                                    </div>
                                                    {isComplete && <CheckCircle2 className="w-6 h-6 text-emerald-500" />}
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

            {/* Photo Requirement Wizard */}
            {showPhotoWizard && (
                <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-300">
                    <div className="bg-corp-nav border-2 border-amber-500/50 rounded-[3rem] shadow-[0_0_100px_rgba(245,158,11,0.2)] max-w-xl w-full p-10 overflow-hidden relative group">
                        {/* Decorative Background Glow */}
                        <div className="absolute -top-24 -right-24 w-48 h-48 bg-amber-500/10 rounded-full blur-3xl group-hover:bg-amber-500/20 transition-all duration-700"></div>

                        <div className="relative flex flex-col items-center text-center space-y-8">
                            <div className="p-8 bg-amber-500/10 rounded-full border border-amber-500/20 shadow-inner group-hover:scale-110 transition-transform duration-500">
                                <Camera className="w-16 h-16 text-amber-500" />
                            </div>

                            <div className="space-y-4">
                                <h2 className="text-4xl font-black text-white tracking-tight uppercase">Photo Required</h2>
                                <div className="h-1.5 w-24 bg-amber-500 mx-auto rounded-full"></div>
                                <p className="text-slate-400 text-lg font-medium leading-relaxed px-4">
                                    Product <span className="text-amber-400 font-black">{selectedSkuForPhoto}</span> has no image in the system.
                                </p>
                            </div>

                            <div className="bg-amber-900/20 border border-amber-500/30 p-6 rounded-3xl w-full flex items-start space-x-4">
                                <AlertCircle className="w-6 h-6 text-amber-500 shrink-0 mt-1" />
                                <div className="text-left">
                                    <p className="text-amber-200 font-black text-sm uppercase tracking-wider mb-1">Mandatory Action</p>
                                    <p className="text-amber-100/70 text-xs leading-relaxed font-bold">
                                        You MUST receive <span className="text-amber-400">1 unit</span> at the <span className="text-white">PHOTO-STATION</span> before proceeding with stock reception.
                                    </p>
                                </div>
                            </div>

                            <div className="flex flex-col space-y-4 w-full">
                                <button
                                    onClick={() => {
                                        setShowPhotoWizard(false);
                                        setScanValue('');
                                        if (inputRef.current) inputRef.current.focus();
                                    }}
                                    className="w-full py-5 bg-amber-500 hover:bg-amber-400 text-black font-black text-xl rounded-2xl shadow-2xl transition-all transform active:scale-95 uppercase tracking-widest"
                                >
                                    Understood
                                </button>
                                <p className="text-[9px] text-slate-500 font-bold uppercase tracking-[0.2em]">Quality Control Protocol v2.5</p>
                            </div>
                        </div>
                    </div>
                </div>
            )}

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

            {showUpdateModal && selectedLine && selectedPkgId !== 'manual' && (
                <PackagingUpdateModal
                    isOpen={showUpdateModal}
                    onClose={() => setShowUpdateModal(false)}
                    packagingName={selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.name || ''}
                    currentData={{
                        qty: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.qty || 0,
                        weight: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.weight || 0,
                        length: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.length || 0,
                        width: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.width || 0,
                        height: selectedLine.packagings.find((p: any) => p.odooId === selectedPkgId)?.height || 0
                    }}
                    newData={{
                        qty: Number(unitsPerLpn),
                        weight: Number(manualWeight),
                        length: Number(manualLength),
                        width: Number(manualWidth),
                        height: Number(manualHeight)
                    }}
                    onConfirm={handlePackagingUpdateConfirm}
                />
            )}
        </div>
    );
};

export default ReceiveStation;
