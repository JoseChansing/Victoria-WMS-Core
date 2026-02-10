import React from 'react';
import { useAuth } from '../../context/AuthContext';
import { useNavigate } from 'react-router-dom';
import { Lock, User, Building2, ShieldCheck, Box as BoxIcon } from 'lucide-react';

export const LoginPage: React.FC = () => {
    const { login } = useAuth();
    const navigate = useNavigate();

    const handleLogin = (e: React.FormEvent) => {
        e.preventDefault();
        // Simulación de Login exitoso y obtención de JWT
        const mockToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.s2v_";
        login(mockToken, 'PERFECTPTY');
        navigate('/');
    };

    return (
        <div className="min-h-screen bg-corp-base flex items-center justify-center p-6 relative overflow-hidden font-sans selection:bg-corp-accent selection:text-white">
            {/* Background Decorative Elements */}
            <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] bg-corp-accent/10 rounded-full blur-[120px] animate-pulse"></div>
            <div className="absolute bottom-[-10%] right-[-10%] w-[30%] h-[30%] bg-corp-green/10 rounded-full blur-[100px] animate-pulse delay-700"></div>

            <div className="w-full max-w-md z-10">
                {/* Branding Section */}
                <div className="text-center mb-10 animate-in fade-in slide-in-from-bottom-4 duration-700">
                    <div className="inline-flex items-center justify-center w-20 h-20 bg-corp-nav border border-corp-secondary/50 rounded-3xl shadow-2xl mb-6 group transition-transform hover:scale-105">
                        <BoxIcon className="w-10 h-10 text-blue-400 group-hover:text-blue-300 transition-colors" />
                    </div>
                    <h1 className="text-4xl font-black text-white tracking-tighter mb-2">
                        Victoria <span className="text-blue-400">WMS</span>
                    </h1>
                    <div className="flex items-center justify-center space-x-2 text-slate-400 font-medium">
                        <ShieldCheck className="w-4 h-4 text-emerald-500" />
                        <span className="text-sm tracking-wide uppercase">Operations Command Center</span>
                    </div>
                </div>

                {/* Login Card */}
                <div className="bg-corp-nav/60 backdrop-blur-2xl border border-corp-secondary/50 rounded-[2.5rem] p-10 shadow-3xl shadow-black/40 animate-in fade-in zoom-in duration-500">
                    <div className="mb-8">
                        <h2 className="text-xl font-bold text-white mb-2">Welcome</h2>
                        <p className="text-slate-400 text-sm">Enter your credentials to access the system.</p>
                    </div>

                    <form onSubmit={handleLogin} className="space-y-5">
                        <div className="space-y-2">
                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em] ml-1">Company / Tenant</label>
                            <div className="relative group">
                                <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                                    <Building2 className="h-5 w-5 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                                </div>
                                <input
                                    type="text"
                                    value="PERFECTPTY"
                                    disabled
                                    className="block w-full pl-12 pr-4 py-4 bg-corp-base/40 border border-corp-secondary text-slate-400 rounded-2xl text-sm focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all cursor-not-allowed font-bold"
                                />
                            </div>
                        </div>

                        <div className="space-y-2">
                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em] ml-1">User</label>
                            <div className="relative group">
                                <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                                    <User className="h-5 w-5 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                                </div>
                                <input
                                    type="text"
                                    defaultValue="admin_supervisor"
                                    disabled
                                    className="block w-full pl-12 pr-4 py-4 bg-corp-base/40 border border-corp-secondary text-slate-400 rounded-2xl text-sm focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all cursor-not-allowed font-bold"
                                />
                            </div>
                        </div>

                        <div className="space-y-2">
                            <label className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em] ml-1">Password</label>
                            <div className="relative group">
                                <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                                    <Lock className="h-5 w-5 text-slate-500 group-focus-within:text-blue-400 transition-colors" />
                                </div>
                                <input
                                    type="password"
                                    defaultValue="********"
                                    disabled
                                    className="block w-full pl-12 pr-4 py-4 bg-corp-base/40 border border-corp-secondary text-slate-400 rounded-2xl text-sm focus:outline-none focus:ring-2 focus:ring-corp-accent transition-all cursor-not-allowed font-bold"
                                />
                            </div>
                        </div>

                        <button
                            type="submit"
                            className="w-full mt-6 bg-corp-accent hover:bg-blue-600 text-white font-black py-4 px-6 rounded-2xl shadow-xl shadow-blue-900/20 transition-all transform hover:-translate-y-1 active:scale-95 flex items-center justify-center space-x-2 border border-blue-400/20"
                        >
                            <span>ENTER SYSTEM</span>
                            <Lock className="w-4 h-4 ml-2" />
                        </button>
                    </form>

                    <p className="mt-8 text-center text-slate-500 text-xs font-medium">
                        &copy; {new Date().getFullYear()} Victoria Warehouse Core v2.0
                    </p>
                </div>
            </div>
        </div>
    );
};
