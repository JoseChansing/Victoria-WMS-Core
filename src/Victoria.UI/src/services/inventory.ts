import api from '../api/axiosConfig';

export interface InventoryTaskLine {
    id: string;
    targetId: string;
    targetDescription: string;
    expectedQty: number;
    countedQty: number;
    status: number; // Enum: Pending, Counted, Verified
    completedAt?: string;
}

export interface InventoryTask {
    id: string;
    taskNumber: string;
    type: number; // Enum: Putaway, CycleCount, Replenishment, Investigation
    priority: number; // Enum: Low, Normal, High, Critical
    status: number; // Enum: Pending, Assigned, InProgress, PendingApproval...
    lines: InventoryTaskLine[];
    assignedUserId?: string;
    createdBy: string;
    createdAt: string;
    approvedBy?: string;
    approvedAt?: string;
    rejectionReason?: string;
}

export const TaskType = {
    Putaway: 0,
    CycleCount: 1,
    Replenishment: 2,
    Investigation: 3,
    TakeSample: 4
} as const;

export type TaskType = typeof TaskType[keyof typeof TaskType];

export const TaskPriority = {
    Low: 0,
    Normal: 1,
    High: 2,
    Critical: 3
} as const;

export type TaskPriority = typeof TaskPriority[keyof typeof TaskPriority];

export const TaskStatus = {
    Pending: 0,
    Assigned: 1,
    InProgress: 2,
    PendingApproval: 3,
    Syncing: 4,
    Completed: 5,
    Cancelled: 6
} as const;

export type TaskStatus = typeof TaskStatus[keyof typeof TaskStatus];

export const LineStatus = {
    Pending: 0,
    Counted: 1,
    Verified: 2
} as const;

export type LineStatus = typeof LineStatus[keyof typeof LineStatus];

export interface CreateTaskDto {
    locationCode?: string;
    productSku?: string;
    type: TaskType;
    priority: TaskPriority;
}

export const inventoryService = {
    async getTasks(status?: TaskStatus, priority?: TaskPriority): Promise<InventoryTask[]> {
        const params: any = {};
        if (status !== undefined) params.status = status;
        if (priority !== undefined) params.priority = priority;

        const response = await api.get('/inventory/tasks', { params });
        return response.data;
    },

    async createTask(data: CreateTaskDto): Promise<{ taskId: string }> {
        const response = await api.post('/inventory/tasks/generate', data);
        return response.data;
    },

    async createBatchTasks(data: { taskType: string; priority: string; targetType: string; targetIds: string[] }): Promise<{ taskId: string, warnings: string[] }> {
        const response = await api.post('/inventory/tasks/batch', data);
        return response.data;
    },

    async reportLineCount(taskId: string, lineId: string, countedQuantity: number): Promise<void> {
        await api.post(`/inventory/tasks/${taskId}/report`, { lineId, countedQuantity });
    },

    async approveAdjustment(taskId: string): Promise<void> {
        await api.post(`/inventory/tasks/${taskId}/approve`);
    },

    async rejectAdjustment(taskId: string, reason: string): Promise<void> {
        await api.post(`/inventory/tasks/${taskId}/reject`, { reason });
    },

    async updateTaskPriority(taskId: string, priority: TaskPriority): Promise<void> {
        await api.patch(`/inventory/tasks/${taskId}/priority`, { priority });
    },

    async assignTask(taskId: string, userId: string): Promise<void> {
        await api.post(`/inventory/tasks/${taskId}/assign`, { userId });
    },

    async cancelTask(taskId: string, reason: string): Promise<void> {
        await api.post(`/inventory/tasks/${taskId}/cancel`, { reason });
    },

    async removeTaskLine(taskId: string, lineId: string, reason?: string): Promise<void> {
        await api.delete(`/inventory/tasks/${taskId}/lines/${lineId}`, {
            data: { reason: reason || 'Manual removal' }
        });
    },

    async getItemLpns(sku: string): Promise<any[]> {
        const response = await api.get(`/inventory/items/${sku}/lpns`);
        return response.data;
    },

    async purgeTasks(): Promise<void> {
        await api.delete('/system/purge-tasks');
    }
};
