import React, { useState } from 'react';
import { X, User, Clock, Hash, ChevronRight, Trash2, CheckCircle2, AlertCircle } from 'lucide-react';
import { type InventoryTask, TaskPriority, TaskStatus, TaskType, LineStatus } from '../../../services/inventory';

interface TaskDetailModalProps {
    task: InventoryTask | null;
    isOpen: boolean;
    onClose: () => void;
    onUpdatePriority: (taskId: string, priority: TaskPriority) => Promise<void>;
    onAssignTask: (taskId: string, userId: string) => Promise<void>;
    onCancelTask: (taskId: string) => Promise<void>;
    onReportLineCount: (taskId: string, lineId: string, count: number) => Promise<void>;
    onApproveAdjustments: (taskId: string) => Promise<void>;
    onRejectAdjustments: (taskId: string, reason: string) => Promise<void>;
    onRemoveLine: (taskId: string, lineId: string, reason?: string) => Promise<void>;
}

const PRIORITY_OPTIONS = [
    { id: TaskPriority.Low, label: 'Low', color: 'text-slate-400', bg: 'bg-slate-500/10', border: 'border-slate-500/20' },
    { id: TaskPriority.Normal, label: 'Normal', color: 'text-blue-400', bg: 'bg-blue-500/10', border: 'border-blue-500/20' },
    { id: TaskPriority.High, label: 'High', color: 'text-orange-400', bg: 'bg-orange-500/10', border: 'border-orange-500/20' },
    { id: TaskPriority.Critical, label: 'Critical', color: 'text-red-400', bg: 'bg-red-500/10', border: 'border-red-500/20' },
];

export const TaskDetailModal: React.FC<TaskDetailModalProps> = ({
    task,
    isOpen,
    onClose,
    onUpdatePriority,
    onAssignTask,
    onCancelTask,
    onReportLineCount,
    onApproveAdjustments,
    onRejectAdjustments,
    onRemoveLine
}) => {
    const [isUpdating, setIsUpdating] = useState(false);
    const [isConfirmingCancel, setIsConfirmingCancel] = useState(false);
    const [assignedUser, setAssignedUser] = useState(task?.assignedUserId || '');
    const [reportingLineId, setReportingLineId] = useState<string | null>(null);
    const [lineCount, setLineCount] = useState<string>('');
    const [removingLineId, setRemovingLineId] = useState<string | null>(null);

    if (!isOpen || !task) return null;

    const handlePriorityChange = async (newPriority: TaskPriority) => {
        setIsUpdating(true);
        try {
            await onUpdatePriority(task.id, newPriority);
        } finally {
            setIsUpdating(false);
        }
    };

    const handleAssign = async () => {
        if (!assignedUser.trim()) return;
        setIsUpdating(true);
        try {
            await onAssignTask(task.id, assignedUser);
        } finally {
            setIsUpdating(false);
        }
    };

    const handleCancel = async () => {
        if (!isConfirmingCancel) {
            setIsConfirmingCancel(true);
            return;
        }
        setIsUpdating(true);
        try {
            await onCancelTask(task.id);
            onClose();
        } finally {
            setIsUpdating(false);
            setIsConfirmingCancel(false);
        }
    };

    const handleReportLine = async (lineId: string) => {
        const qty = parseInt(lineCount);
        if (isNaN(qty)) return;

        setIsUpdating(true);
        try {
            await onReportLineCount(task.id, lineId, qty);
            setReportingLineId(null);
            setLineCount('');
        } finally {
            setIsUpdating(false);
        }
    };

    const handleRemoveLine = async (lineId: string) => {
        if (removingLineId !== lineId) {
            setRemovingLineId(lineId);
            return;
        }

        setIsUpdating(true);
        try {
            await onRemoveLine(task.id, lineId, 'Released by supervisor');
            setRemovingLineId(null);
        } finally {
            setIsUpdating(false);
        }
    };

    const getTypeLabel = (type: TaskType) => {
        switch (type) {
            case TaskType.CycleCount: return 'Cycle Count';
            case TaskType.Putaway: return 'Putaway Check';
            case TaskType.Replenishment: return 'Replenishment';
            case TaskType.Investigation: return 'Investigation';
            default: return 'Task';
        }
    };

    return (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
            <div className="absolute inset-0 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-300" onClick={onClose} />

            <div className="relative w-full max-w-4xl bg-slate-900 border border-white/10 rounded-[2.5rem] shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
                {/* Header */}
                <div className="p-8 border-b border-white/10 flex items-center justify-between bg-gradient-to-r from-corp-accent/10 to-transparent">
                    <div className="flex items-center gap-4">
                        <div className="bg-corp-accent/20 p-3 rounded-2xl border border-corp-accent/30 shadow-inner">
                            <Clock className="w-6 h-6 text-corp-accent" />
                        </div>
                        <div>
                            <h2 className="text-2xl font-black text-white tracking-tight">{task.taskNumber}</h2>
                            <p className="text-slate-500 font-bold uppercase tracking-widest text-xs mt-0.5">
                                {getTypeLabel(task.type as TaskType)} • {(task.lines?.length ?? 0)} Lines
                            </p>
                        </div>
                    </div>
                    <button
                        onClick={onClose}
                        className="p-2 hover:bg-white/5 rounded-full transition-colors group"
                    >
                        <X className="w-6 h-6 text-slate-500 group-hover:text-white transition-colors" />
                    </button>
                </div>

                <div className="p-8 overflow-y-auto max-h-[75vh] no-scrollbar">
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
                        {/* Lines Section */}
                        <div className="lg:col-span-2 space-y-6">
                            <h3 className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em]">Inventory Lines</h3>

                            <div className="bg-slate-800/20 border border-white/5 rounded-3xl overflow-hidden">
                                <table className="w-full text-left">
                                    <thead>
                                        <tr className="bg-white/5 border-b border-white/5">
                                            <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Target Asset</th>
                                            <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Expected</th>
                                            <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Counted</th>
                                            <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Status</th>
                                            <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-white/5">
                                        {(task.lines ?? []).map((line) => (
                                            <tr key={line.id} className="group hover:bg-white/5 transition-colors">
                                                <td className="px-6 py-4">
                                                    <div className="flex items-center gap-3">
                                                        <div className="bg-corp-accent/10 p-2 rounded-lg">
                                                            <Hash className="w-4 h-4 text-corp-accent" />
                                                        </div>
                                                        <div>
                                                            <span className="font-bold text-white block text-sm">{line.targetId}</span>
                                                            <span className="text-[10px] text-slate-500 font-black uppercase tracking-tighter line-clamp-1">{line.targetDescription}</span>
                                                        </div>
                                                    </div>
                                                </td>
                                                <td className="px-6 py-4 text-center">
                                                    <span className="font-black text-slate-300">{line.expectedQty}</span>
                                                </td>
                                                <td className="px-6 py-4 text-center">
                                                    {reportingLineId === line.id ? (
                                                        <div className="flex items-center gap-2 justify-center">
                                                            <input
                                                                type="number"
                                                                autoFocus
                                                                value={lineCount}
                                                                onChange={(e) => setLineCount(e.target.value)}
                                                                className="w-16 bg-slate-900 border border-corp-accent/30 rounded-lg px-2 py-1 text-xs text-white font-black text-center"
                                                            />
                                                            <button
                                                                onClick={() => handleReportLine(line.id)}
                                                                className="text-emerald-500 hover:text-emerald-400"
                                                            >
                                                                <CheckCircle2 className="w-4 h-4" />
                                                            </button>
                                                        </div>
                                                    ) : (
                                                        <button
                                                            disabled={task.status === TaskStatus.Completed || task.status === TaskStatus.Cancelled}
                                                            onClick={() => { setReportingLineId(line.id); setLineCount(line.countedQty.toString()); }}
                                                            className={`font-black text-sm transition-colors ${line.status === LineStatus.Pending ? 'text-slate-600 hover:text-white underline decoration-dotted' : 'text-white'}`}
                                                        >
                                                            {line.status === LineStatus.Pending ? 'Report' : line.countedQty}
                                                        </button>
                                                    )}
                                                </td>
                                                <td className="px-6 py-4 text-right">
                                                    <span className={`px-2 py-1 rounded-md text-[9px] font-black uppercase tracking-widest 
                                                        ${line.status === LineStatus.Verified ? 'bg-emerald-500/10 text-emerald-500' :
                                                            line.status === LineStatus.Counted ? (line.countedQty === line.expectedQty ? 'bg-blue-500/10 text-blue-500' : 'bg-orange-500/10 text-orange-400') :
                                                                'bg-slate-500/10 text-slate-500'}`}>
                                                        {LineStatus.Verified === line.status ? 'Verified' : LineStatus.Counted === line.status ? 'Counted' : 'Pending'}
                                                    </span>
                                                </td>
                                                <td className="px-6 py-4">
                                                    {(task.status === TaskStatus.Pending || task.status === TaskStatus.InProgress || task.status === TaskStatus.Assigned) && (
                                                        <div className="flex justify-center">
                                                            <button
                                                                onClick={() => handleRemoveLine(line.id)}
                                                                disabled={isUpdating}
                                                                className={`p-2 rounded-lg transition-all ${removingLineId === line.id
                                                                        ? 'bg-red-500/20 text-red-400 border border-red-500/30'
                                                                        : 'bg-white/5 text-slate-400 hover:bg-red-500/10 hover:text-red-400'
                                                                    }`}
                                                                title={removingLineId === line.id ? 'Click again to confirm' : 'Remove from task'}
                                                            >
                                                                <Trash2 className="w-4 h-4" />
                                                            </button>
                                                        </div>
                                                    )}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        {/* Actions Section */}
                        <div className="space-y-8">
                            {/* Management Section */}
                            <div className="bg-slate-800/30 p-6 rounded-3xl border border-white/5 space-y-6">
                                {/* Priority Update */}
                                <div className="space-y-4">
                                    <h3 className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em]">Update Priority</h3>
                                    <div className="grid grid-cols-2 gap-2">
                                        {PRIORITY_OPTIONS.map((opt) => (
                                            <button
                                                key={opt.id}
                                                disabled={isUpdating}
                                                onClick={() => handlePriorityChange(opt.id as TaskPriority)}
                                                className={`p-3 rounded-xl border text-sm font-bold transition-all flex items-center justify-center gap-2
                                                    ${task.priority === opt.id
                                                        ? `${opt.bg} ${opt.border} ${opt.color} ring-2 ring-white/10`
                                                        : 'bg-white/5 border-transparent text-slate-500 hover:bg-white/10'}`}
                                            >
                                                <div className={`w-1.5 h-1.5 rounded-full ${task.priority === opt.id ? 'bg-current animate-pulse' : 'bg-slate-600'}`} />
                                                {opt.label}
                                            </button>
                                        ))}
                                    </div>
                                </div>

                                {/* Assignment */}
                                <div className="space-y-4">
                                    <h3 className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em]">Assignment</h3>
                                    <div className="space-y-3">
                                        <div className="relative group">
                                            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
                                                <User className="h-5 w-5 text-slate-500 group-focus-within:text-corp-accent transition-colors" />
                                            </div>
                                            <input
                                                type="text"
                                                value={assignedUser}
                                                onChange={(e) => setAssignedUser(e.target.value)}
                                                placeholder="Operator ID"
                                                className="block w-full pl-11 pr-4 py-3 bg-slate-800/50 border border-white/5 rounded-xl text-white placeholder-slate-600 focus:outline-none focus:ring-2 focus:ring-corp-accent/30 focus:border-corp-accent/50 transition-all font-bold text-sm"
                                            />
                                        </div>
                                        <button
                                            onClick={handleAssign}
                                            disabled={isUpdating || !assignedUser.trim()}
                                            className="w-full bg-corp-accent hover:bg-corp-accent/80 disabled:opacity-50 text-white font-black py-3 rounded-xl shadow-lg shadow-corp-accent/20 transition-all flex items-center justify-center gap-2 group"
                                        >
                                            <span>Assign Operator</span>
                                            <ChevronRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                                        </button>
                                    </div>
                                </div>
                            </div>

                            {/* Supervisor Approval (Only if pending) */}
                            {task.status === TaskStatus.PendingApproval && (
                                <div className="bg-orange-500/10 border border-orange-500/20 p-6 rounded-3xl space-y-4 animate-in slide-in-from-bottom-4">
                                    <div className="flex items-center gap-3 text-orange-500">
                                        <AlertCircle className="w-5 h-5" />
                                        <h3 className="text-xs font-black uppercase tracking-widest">Pending Discrepancies</h3>
                                    </div>
                                    <p className="text-xs text-orange-400/80 font-bold leading-relaxed">
                                        The counted quantities do not match the expected values on some lines. Supervisor approval required.
                                    </p>
                                    <button
                                        onClick={() => onApproveAdjustments(task.id)}
                                        className="w-full bg-orange-500 hover:bg-orange-400 text-white font-black py-3 rounded-xl transition-all shadow-lg shadow-orange-900/40"
                                    >
                                        Approve & Sync Odoo
                                    </button>
                                    <button
                                        onClick={() => onRejectAdjustments(task.id, "Discrepancy too high, please re-count.")}
                                        className="w-full bg-white/5 hover:bg-white/10 text-orange-400 font-bold py-2 rounded-xl text-xs transition-all"
                                    >
                                        Reject & Request Re-count
                                    </button>
                                </div>
                            )}

                            {/* Cancellation */}
                            <div className="space-y-4 pt-4 border-t border-white/5">
                                <h3 className="text-[10px] font-black text-slate-500 uppercase tracking-[0.2em]">Destructive Actions</h3>
                                <button
                                    onClick={handleCancel}
                                    disabled={isUpdating || task.status === TaskStatus.Completed || task.status === TaskStatus.Cancelled}
                                    className={`w-full font-black py-3 rounded-xl transition-all flex items-center justify-center gap-2 group
                                        ${isConfirmingCancel
                                            ? 'bg-red-500 text-white shadow-lg shadow-red-900/40 animate-pulse'
                                            : 'bg-red-500/10 hover:bg-red-500/20 text-red-500 border border-red-500/20 hover:border-red-500/40'}`}
                                >
                                    <Trash2 className="w-4 h-4" />
                                    <span>{isConfirmingCancel ? 'Are you sure? Click to confirm' : 'Cancel Task'}</span>
                                </button>
                                {isConfirmingCancel && (
                                    <button
                                        onClick={() => setIsConfirmingCancel(false)}
                                        className="w-full text-[10px] font-black text-slate-500 uppercase tracking-widest hover:text-white transition-colors"
                                    >
                                        Nevermind, keep it
                                    </button>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Footer Info */}
                    <div className="mt-8 pt-8 border-t border-white/5 flex items-center justify-between">
                        <div className="flex items-center gap-6">
                            <DetailSimple label="Created By" value={task.createdBy} />
                            <DetailSimple label="Assigned To" value={task.assignedUserId || 'Unassigned'} />
                            <DetailSimple label="Started At" value={task.createdAt ? new Date(task.createdAt).toLocaleTimeString() : '---'} />
                        </div>
                        <div className="bg-slate-800/30 px-6 py-3 rounded-full flex items-center gap-3 border border-white/5 shadow-inner">
                            <div className={`w-2 h-2 rounded-full shadow-[0_0_10px_rgba(255,255,255,0.3)] 
                                ${task.status === TaskStatus.Completed ? 'bg-emerald-500' :
                                    task.status === TaskStatus.PendingApproval ? 'bg-orange-500' :
                                        task.status === TaskStatus.Cancelled ? 'bg-red-500' : 'bg-blue-500'}`}
                            />
                            <span className="text-[10px] font-black text-slate-500 uppercase tracking-widest">Status:</span>
                            <span className="font-black text-white text-sm">
                                {Object.keys(TaskStatus).find(key => (TaskStatus as any)[key] === task.status)}
                            </span>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

const DetailSimple: React.FC<{ label: string, value: string }> = ({ label, value }) => (
    <div>
        <p className="text-[9px] font-black text-slate-600 uppercase tracking-widest leading-none mb-1">{label}</p>
        <p className="text-xs font-bold text-slate-300">{value}</p>
    </div>
);
