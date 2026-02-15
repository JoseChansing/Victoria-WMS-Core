import React, { useEffect, useState } from 'react';

import {
    inventoryService,
    type InventoryTask,
    TaskPriority,
    TaskStatus,
    TaskType,
    LineStatus,
    type CreateTaskDto
} from '../../services/inventory';
import { InventoryMetrics } from '../../components/Inventory/InventoryMetrics';
import { CreateTaskModal } from '../../components/Inventory/CreateTaskModal';
import { AdjustmentApprovalModal } from '../../components/Inventory/AdjustmentApprovalModal';
import { TaskDetailModal } from './components/TaskDetailModal';
import { toast } from 'sonner';

export const InventoryDashboard: React.FC = () => {
    const [tasks, setTasks] = useState<InventoryTask[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
    const [selectedTaskForApproval, setSelectedTaskForApproval] = useState<InventoryTask | null>(null);
    const [selectedTaskForDetails, setSelectedTaskForDetails] = useState<InventoryTask | null>(null);

    const fetchTasks = async () => {
        try {
            const data = await inventoryService.getTasks();
            setTasks(data);
            setError(null);
        } catch (err) {
            console.error(err);
            setError("Error al cargar tareas de inventario.");
        } finally {
            setIsLoading(false);
        }
    };

    useEffect(() => {
        fetchTasks();
        const interval = setInterval(fetchTasks, 15000); // Poll every 15s
        return () => clearInterval(interval);
    }, []);

    const handleCreateTask = async (data: CreateTaskDto) => {
        await inventoryService.createTask(data);
        fetchTasks();
    };

    const handleApprove = async (taskId: string) => {
        await inventoryService.approveAdjustment(taskId);
        fetchTasks();
    };

    const handleReject = async (taskId: string, reason: string) => {
        await inventoryService.rejectAdjustment(taskId, reason);
        fetchTasks();
    };

    const handleUpdatePriority = async (taskId: string, priority: TaskPriority) => {
        try {
            await inventoryService.updateTaskPriority(taskId, priority);
            toast.success("Prioridad actualizada");

            // Optimistic update for the modal
            if (selectedTaskForDetails && selectedTaskForDetails.id === taskId) {
                setSelectedTaskForDetails({ ...selectedTaskForDetails, priority });
            }
            fetchTasks();
        } catch (error) {
            toast.error("Error al actualizar prioridad");
        }
    };

    const handleAssignTask = async (taskId: string, userId: string) => {
        try {
            await inventoryService.assignTask(taskId, userId);
            toast.success(`Tarea asignada a ${userId}`);

            // Optimistic update for the modal
            if (selectedTaskForDetails && selectedTaskForDetails.id === taskId) {
                setSelectedTaskForDetails({
                    ...selectedTaskForDetails,
                    assignedUserId: userId,
                    status: TaskStatus.Assigned
                });
            }
            fetchTasks();
        } catch (error) {
            toast.error("Error al asignar tarea");
        }
    };

    const handleCancelTask = async (taskId: string) => {
        try {
            await inventoryService.cancelTask(taskId, "Cancelled by supervisor");
            toast.success("Tarea cancelada");
            fetchTasks();
        } catch (error) {
            toast.error("Error al cancelar tarea");
        }
    };

    const handleRemoveLine = async (taskId: string, lineId: string, reason?: string) => {
        try {
            await inventoryService.removeTaskLine(taskId, lineId, reason);
            toast.success("Línea removida. LPN liberado.");
            fetchTasks();

            // Optimistic update for modal
            if (selectedTaskForDetails && selectedTaskForDetails.id === taskId) {
                const updatedLines = selectedTaskForDetails.lines.filter(l => l.id !== lineId);
                if (updatedLines.length === 0) {
                    setSelectedTaskForDetails(null); // Task auto-cancelled
                } else {
                    setSelectedTaskForDetails({ ...selectedTaskForDetails, lines: updatedLines });
                }
            }
        } catch (error) {
            toast.error("Error al remover línea");
        }
    };

    const handleReportLineCount = async (taskId: string, lineId: string, count: number) => {
        try {
            await inventoryService.reportLineCount(taskId, lineId, count);
            toast.success("Count reported");

            // Refresh local state for the modal if open
            if (selectedTaskForDetails && selectedTaskForDetails.id === taskId) {
                const refreshedTask = await inventoryService.getTasks().then(tasks => tasks.find(t => t.id === taskId));
                if (refreshedTask) setSelectedTaskForDetails(refreshedTask);
            }
            fetchTasks();
        } catch (error) {
            toast.error("Error reporting count");
        }
    };

    const getStatusBadge = (status: TaskStatus) => {
        switch (status) {
            case TaskStatus.Pending: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-gray-100 text-gray-600">Pending</span>;
            case TaskStatus.Assigned: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-blue-100 text-blue-600">Assigned</span>;
            case TaskStatus.InProgress: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-yellow-100 text-yellow-600">In Progress</span>;
            case TaskStatus.PendingApproval: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-orange-100 text-orange-600 animate-pulse">Approval Required</span>;
            case TaskStatus.Completed: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-green-100 text-green-600">Completed</span>;
            case TaskStatus.Cancelled: return <span className="px-2 py-1 text-xs font-semibold rounded-full bg-red-100 text-red-600">Cancelled</span>;
            default: return null;
        }
    };

    const getTypeIcon = (type: TaskType) => {
        switch (type) {
            case TaskType.CycleCount: return "🔄";
            case TaskType.Putaway: return "📥";
            case TaskType.Replenishment: return "📦";
            default: return "📋";
        }
    };

    const getPriorityBadge = (priority: TaskPriority) => {
        switch (priority) {
            case TaskPriority.Critical: return <span className="text-red-600 font-bold text-xs uppercase bg-red-50 px-2 py-0.5 rounded border border-red-100">Critical</span>;
            case TaskPriority.High: return <span className="text-orange-600 font-bold text-xs uppercase">High</span>;
            case TaskPriority.Low: return <span className="text-gray-400 text-xs uppercase">Low</span>;
            default: return <span className="text-blue-600 text-xs uppercase">Normal</span>;
        }
    };

    return (
        <div className="flex flex-col h-[calc(100vh-160px)] animate-in fade-in duration-500">
            <div className="shrink-0 flex justify-between items-center mb-8">
                <div>
                    <h1 className="text-3xl font-black text-white tracking-tight flex items-center gap-2">
                        Inventory Supervisor Dashboard
                        <span className="px-3 py-1 rounded-full bg-corp-accent/20 text-corp-accent text-xs font-bold uppercase tracking-widest border border-corp-accent/30">
                            Inventory
                        </span>
                    </h1>
                    <p className="text-slate-400 font-medium mt-1">Real-time task management and control</p>
                </div>
                <button
                    onClick={() => setIsCreateModalOpen(true)}
                    className="flex items-center gap-2 bg-corp-accent hover:bg-blue-600 text-white px-5 py-2.5 rounded-xl font-bold shadow-lg shadow-blue-900/20 transition-all hover:scale-[1.02] active:scale-[0.98]"
                >
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" /></svg>
                    New Task
                </button>
            </div>

            <div className="shrink-0 mb-6">
                <InventoryMetrics tasks={tasks} />
            </div>

            {isLoading ? (
                <div className="flex flex-col items-center justify-center py-20">
                    <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-corp-accent"></div>
                    <p className="mt-4 text-slate-400 font-bold animate-pulse">Syncing tasks...</p>
                </div>
            ) : error ? (
                <div className="bg-red-500/10 border border-red-500/20 p-6 rounded-2xl text-center">
                    <p className="text-red-400 font-bold mb-2">Synchronization Error</p>
                    <p className="text-sm text-red-300/80">{error}</p>
                </div>
            ) : (
                <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-xl flex flex-col flex-1 overflow-hidden min-h-0">
                    <div className="flex-1 overflow-auto no-scrollbar">
                        <table className="w-full text-left border-collapse">
                            <thead className="sticky top-0 z-10 bg-corp-base/90 backdrop-blur-md border-b border-corp-secondary/50">
                                <tr>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Task</th>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Location / SKU</th>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Priority</th>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Status</th>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Progress</th>
                                    <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Actions</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-corp-secondary/20">
                                {tasks.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} className="px-6 py-12 text-center">
                                            <div className="flex flex-col items-center gap-3">
                                                <div className="p-4 bg-corp-secondary/20 rounded-full">
                                                    <svg className="w-8 h-8 text-slate-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" /></svg>
                                                </div>
                                                <p className="text-slate-500 font-bold">No active tasks</p>
                                            </div>
                                        </td>
                                    </tr>
                                ) : (
                                    tasks.map((task) => (
                                        <tr
                                            key={task.id}
                                            onClick={() => setSelectedTaskForDetails(task)}
                                            className="hover:bg-corp-accent/5 transition-colors group cursor-pointer"
                                        >
                                            <td className="px-6 py-5">
                                                <div className="flex items-center gap-3">
                                                    <span className="text-xl bg-corp-base w-10 h-10 flex items-center justify-center rounded-xl border border-corp-secondary/50 shadow-sm group-hover:border-corp-accent/50 transition-colors">
                                                        {getTypeIcon(task.type as TaskType)}
                                                    </span>
                                                    <div>
                                                        <p className="font-bold text-white group-hover:text-corp-accent transition-colors">{task.taskNumber}</p>
                                                        <p className="text-[10px] font-bold text-slate-500 uppercase tracking-wider">
                                                            {task.createdAt && !isNaN(new Date(task.createdAt).getTime())
                                                                ? new Date(task.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                                                                : 'N/A'}
                                                        </p>
                                                    </div>
                                                </div>
                                            </td>
                                            <td className="px-6 py-5">
                                                <div className="flex flex-col">
                                                    <span className="font-bold text-white text-sm">
                                                        {(task.lines?.length ?? 0)} {(task.lines?.length ?? 0) === 1 ? 'LPN' : 'LPNs'}
                                                    </span>
                                                    <span className="text-[10px] text-slate-500 font-bold uppercase tracking-tighter truncate max-w-[150px]">
                                                        {task.lines?.[0]?.targetId ?? '---'}{task.lines?.length > 1 ? ', ...' : ''}
                                                    </span>
                                                </div>
                                            </td>
                                            <td className="px-6 py-5">
                                                {getPriorityBadge(task.priority as TaskPriority)}
                                            </td>
                                            <td className="px-6 py-5">
                                                {getStatusBadge(task.status as TaskStatus)}
                                            </td>
                                            <td className="px-6 py-5">
                                                <div className="space-y-1.5">
                                                    <div className="flex justify-between items-center text-[10px] font-black uppercase tracking-widest text-slate-500">
                                                        <span>Progress</span>
                                                        <span className="text-white">{task.lines?.filter(l => l.status !== LineStatus.Pending).length ?? 0} / {task.lines?.length ?? 0}</span>
                                                    </div>
                                                    <div className="w-full bg-corp-secondary/30 h-1.5 rounded-full overflow-hidden border border-corp-secondary/20">
                                                        <div
                                                            className={`h-full transition-all duration-1000 ${task.status === TaskStatus.PendingApproval ? 'bg-orange-500' : 'bg-corp-accent'}`}
                                                            style={{ width: `${((task.lines?.filter(l => l.status !== LineStatus.Pending).length ?? 0) / (task.lines?.length || 1)) * 100}%` }}
                                                        />
                                                    </div>
                                                </div>
                                            </td>
                                            <td className="px-6 py-5 text-right">
                                                {task.status === TaskStatus.PendingApproval && (
                                                    <button
                                                        onClick={() => setSelectedTaskForApproval(task)}
                                                        className="bg-amber-500/10 hover:bg-amber-500/20 text-amber-500 font-bold px-4 py-2 rounded-lg text-xs border border-amber-500/20 hover:border-amber-500/40 transition-all uppercase tracking-wider"
                                                    >
                                                        ⚖️ Resolve
                                                    </button>
                                                )}
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            <CreateTaskModal
                isOpen={isCreateModalOpen}
                onClose={() => setIsCreateModalOpen(false)}
                onSubmit={handleCreateTask}
            />

            <AdjustmentApprovalModal
                isOpen={!!selectedTaskForApproval}
                task={selectedTaskForApproval}
                onClose={() => setSelectedTaskForApproval(null)}
                onApprove={handleApprove}
                onReject={handleReject}
            />

            <TaskDetailModal
                isOpen={!!selectedTaskForDetails}
                task={selectedTaskForDetails}
                onClose={() => setSelectedTaskForDetails(null)}
                onUpdatePriority={handleUpdatePriority}
                onAssignTask={handleAssignTask}
                onCancelTask={handleCancelTask}
                onReportLineCount={handleReportLineCount}
                onApproveAdjustments={handleApprove}
                onRejectAdjustments={handleReject}
                onRemoveLine={handleRemoveLine}
            />
        </div>
    );
};
