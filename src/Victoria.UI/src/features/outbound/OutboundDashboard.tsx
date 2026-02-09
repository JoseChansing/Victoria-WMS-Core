import React, { useState, useEffect } from 'react';
import { outboundService } from '../../services/outboundService';
import type { OutboundTask } from '../../services/outboundService';

const OutboundDashboard: React.FC = () => {
    const [tasks, setTasks] = useState<OutboundTask[]>([]);
    const [loading, setLoading] = useState(false);
    const [message, setMessage] = useState('');
    const [orderIdsInput, setOrderIdsInput] = useState('');

    const handleCreateWave = async () => {
        if (!orderIdsInput) return;
        setLoading(true);
        try {
            const ids = orderIdsInput.split(',').map(s => s.trim());
            const res = await outboundService.createWave(ids);
            setMessage(`Wave Created: ${res.waveId}`);
            loadTasks();
        } catch (e: any) {
            setMessage(`Error: ${e.message}`);
        } finally {
            setLoading(false);
        }
    };

    const loadTasks = async () => {
        try {
            const data = await outboundService.getTasks();
            setTasks(data);
        } catch (e: any) {
            console.error(e);
        }
    };

    useEffect(() => {
        loadTasks();
    }, []);

    return (
        <div className="p-8 min-h-screen bg-corp-base text-white">
            <h1 className="text-3xl font-bold mb-8 tracking-tight">Outbound Dashboard (Testing)</h1>

            <div className="mb-8 border border-corp-secondary p-6 rounded-xl bg-corp-nav shadow-lg">
                <h2 className="text-xl font-semibold mb-2 text-white">Create Wave</h2>
                <p className="text-sm text-slate-400 mb-4">Enter Outbound Order GUIDs (comma separated). Ensure you synced first.</p>
                <div className="flex gap-4">
                    <input
                        type="text"
                        value={orderIdsInput}
                        onChange={e => setOrderIdsInput(e.target.value)}
                        placeholder="e.g. 550e8400-e29b-41d4-a716-446655440000"
                        className="flex-1 bg-corp-base border border-corp-secondary rounded-lg px-4 py-2.5 text-white placeholder-slate-500 focus:outline-none focus:border-corp-accent focus:ring-1 focus:ring-corp-accent transition-all"
                    />
                    <button
                        onClick={handleCreateWave}
                        disabled={loading || !orderIdsInput}
                        className="bg-violet-600 hover:bg-violet-500 text-white font-medium px-6 py-2.5 rounded-lg shadow-lg shadow-violet-600/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        Create Wave & Allocate
                    </button>
                </div>
            </div>

            {message && (
                <div className={`p-4 rounded-lg mb-6 text-sm font-medium border ${message.includes('Error') ? 'bg-rose-500/10 text-rose-400 border-rose-500/20' : 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
                    }`}>
                    {message}
                </div>
            )}

            <div className="mt-8">
                <div className="flex justify-between items-center mb-6">
                    <h2 className="text-2xl font-bold bg-clip-text text-transparent bg-gradient-to-r from-white to-slate-400">Generated Tasks</h2>
                    <button onClick={loadTasks} className="text-corp-accent hover:text-white transition-colors text-sm font-medium flex items-center gap-1">
                        Refresh List
                    </button>
                </div>

                <div className="rounded-xl border border-corp-secondary overflow-hidden shadow-2xl">
                    <table className="min-w-full bg-corp-nav">
                        <thead>
                            <tr className="bg-corp-base/50 text-left">
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Type</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Status</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Product</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">Qty</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">From</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">To</th>
                                <th className="px-6 py-4 text-xs font-bold text-slate-400 uppercase tracking-wider">LPN</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary">
                            {tasks.map(task => (
                                <tr key={task.id} className="hover:bg-corp-base/30 transition-colors">
                                    <td className="px-6 py-4 whitespace-nowrap">
                                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium border ${task.type === 'CycleCount' ? 'bg-rose-500/10 text-rose-400 border-rose-500/20' :
                                            task.type === 'FullPalletMove' ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20' :
                                                'bg-blue-500/10 text-blue-400 border-blue-500/20'
                                            }`}>
                                            {task.type}
                                        </span>
                                    </td>
                                    <td className="px-6 py-4 text-sm text-slate-300">{task.status}</td>
                                    <td className="px-6 py-4 text-sm text-white font-medium">{task.productId}</td>
                                    <td className="px-6 py-4 text-sm text-slate-300 font-mono">{task.quantity}</td>
                                    <td className="px-6 py-4 text-sm text-slate-300">{task.sourceLocation}</td>
                                    <td className="px-6 py-4 text-sm text-slate-300">{task.targetLocation}</td>
                                    <td className="px-6 py-4 text-sm text-slate-400 font-mono text-xs">{task.lpnId}</td>
                                </tr>
                            ))}
                            {tasks.length === 0 && (
                                <tr>
                                    <td colSpan={7} className="px-6 py-12 text-center text-slate-500">
                                        <div className="flex flex-col items-center justify-center space-y-2">
                                            <p className="text-lg font-medium text-slate-400">No tasks generated yet</p>
                                            <p className="text-sm">Create a wave to generate tasks</p>
                                        </div>
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};

export default OutboundDashboard;
