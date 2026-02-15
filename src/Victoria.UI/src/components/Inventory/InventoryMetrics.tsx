import React from 'react';
import { type InventoryTask, TaskPriority, TaskStatus } from '../../services/inventory';

interface InventoryMetricsProps {
    tasks: InventoryTask[];
}

export const InventoryMetrics: React.FC<InventoryMetricsProps> = ({ tasks }) => {
    const activeTasks = tasks.filter(t => t.status !== TaskStatus.Completed && t.status !== TaskStatus.Cancelled).length;
    const pendingApproval = tasks.filter(t => t.status === TaskStatus.PendingApproval).length;
    const criticalTasks = tasks.filter(t => t.priority === TaskPriority.Critical && t.status !== TaskStatus.Completed).length;

    return (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
            <div className="bg-corp-nav/40 rounded-3xl p-6 border border-corp-secondary shadow-lg shadow-black/10 flex items-center justify-between group hover:border-corp-accent/30 transition-all">
                <div>
                    <p className="text-slate-500 text-xs font-black uppercase tracking-widest">Active Tasks</p>
                    <h3 className="text-3xl font-black text-white mt-1 group-hover:text-corp-accent transition-colors">{activeTasks}</h3>
                </div>
                <div className="w-12 h-12 bg-blue-500/10 rounded-2xl flex items-center justify-center text-blue-500 border border-blue-500/20 group-hover:bg-blue-500/20 transition-all">
                    <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
                </div>
            </div>

            <div className={`rounded-3xl p-6 border shadow-lg shadow-black/10 flex items-center justify-between transition-all ${pendingApproval > 0 ? 'bg-amber-500/10 border-amber-500/30' : 'bg-corp-nav/40 border-corp-secondary'}`}>
                <div>
                    <p className={`text-xs font-black uppercase tracking-widest ${pendingApproval > 0 ? 'text-amber-500' : 'text-slate-500'}`}>Pending Approval</p>
                    <h3 className={`text-3xl font-black mt-1 ${pendingApproval > 0 ? 'text-white' : 'text-white'}`}>{pendingApproval}</h3>
                </div>
                <div className={`w-12 h-12 rounded-2xl flex items-center justify-center border ${pendingApproval > 0 ? 'bg-amber-500/20 text-amber-500 border-amber-500/30 animate-pulse' : 'bg-slate-500/10 text-slate-500 border-slate-500/20'}`}>
                    <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
                </div>
            </div>

            <div className="bg-corp-nav/40 rounded-3xl p-6 border border-corp-secondary shadow-lg shadow-black/10 flex items-center justify-between group hover:border-red-500/30 transition-all">
                <div>
                    <p className="text-slate-500 text-xs font-black uppercase tracking-widest">Critical Priority</p>
                    <h3 className="text-3xl font-black text-white mt-1 group-hover:text-red-500 transition-colors">{criticalTasks}</h3>
                </div>
                <div className="w-12 h-12 bg-red-500/10 rounded-2xl flex items-center justify-center text-red-500 border border-red-500/20 group-hover:bg-red-500/20 transition-all">
                    <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
                </div>
            </div>
        </div>
    );
};
