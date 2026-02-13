import React from 'react';
import { AlertTriangle, Home, UserCheck } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

interface CriticalStopModalProps {
    orderNumber: string;
    message: string;
}

const CriticalStopModal: React.FC<CriticalStopModalProps> = ({ orderNumber, message }) => {
    const navigate = useNavigate();

    return (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-rose-950/90 backdrop-blur-xl transition-all duration-500 animate-in fade-in">
            <div className="max-w-lg w-full bg-corp-nav border-2 border-rose-500 shadow-[0_0_50px_rgba(244,63,94,0.3)] rounded-[2.5rem] overflow-hidden">
                <div className="bg-rose-500 p-8 flex flex-col items-center text-center">
                    <div className="w-20 h-20 bg-white/20 rounded-full flex items-center justify-center mb-4 animate-pulse">
                        <AlertTriangle className="text-white w-12 h-12" />
                    </div>
                    <h2 className="text-3xl font-black text-white uppercase tracking-tighter">
                        Operación Detenida
                    </h2>
                    <p className="text-rose-100 font-bold uppercase tracking-widest text-xs mt-1">
                        Inconsistencia Crítica Detectada
                    </p>
                </div>

                <div className="p-10 space-y-6">
                    <div className="bg-white/5 rounded-3xl p-6 border border-white/10">
                        <p className="text-slate-400 text-xs font-bold uppercase tracking-widest mb-2">Detalles del Error</p>
                        <p className="text-white font-medium leading-relaxed">
                            {message || `La orden ${orderNumber} ha sido cancelada o eliminada en el sistema central (Odoo).`}
                        </p>
                    </div>

                    <div className="space-y-4">
                        <div className="flex items-center space-x-3 text-rose-400 bg-rose-500/10 p-4 rounded-2xl border border-rose-500/20">
                            <UserCheck className="w-5 h-5 flex-shrink-0" />
                            <p className="text-xs font-bold leading-tight">
                                Por favor, deje de procesar esta mercancia inmediatamente y contacte a un SUPERVISOR.
                            </p>
                        </div>

                        <button
                            onClick={() => navigate('/inbound')}
                            className="w-full flex items-center justify-center space-x-3 bg-white text-rose-950 hover:bg-slate-200 py-4 rounded-2xl transition-all font-black uppercase tracking-widest text-sm shadow-xl"
                        >
                            <Home className="w-5 h-5" />
                            <span>Volver al Dashboard</span>
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default CriticalStopModal;
