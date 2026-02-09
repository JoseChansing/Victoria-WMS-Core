import api from '../api/axiosConfig';

const API_ROOT = '/outbound';

export interface OutboundOrder {
    id: string;
    orderId: string;
    odooId: number;
    partnerId: string;
    scheduledDate: string;
    priority: string;
    extensionWaveId?: string;
}

export interface OutboundTask {
    id: string;
    type: string;
    status: string;
    sourceLocation: string;
    targetLocation: string;
    lpnId: string;
    productId: string;
    quantity: number;
    pickedQuantity: number;
}

export const outboundService = {
    syncOrders: async () => {
        const { data } = await api.post(`${API_ROOT}/sync`);
        return data;
    },

    getOrders: async () => {
        // Mock for now or assuming an endpoint exists, or we infer from tasks?
        // Actually we need an endpoint to list orders to select for wave.
        // For now we will assume we can fetch them or just sync returns count.
        // Let's rely on Sync message or just hardcode for testing if no endpoint.
        // Wait, we need to SELECT orders.
        // I should probably add GET /api/outbound/orders to Controller or just use tasks.
        // Let's stick to what we have. API only has sync, wave, tasks.
        // We can't query Orders yet. 
        // I will add a simple GET /orders to controller in next step if needed, or just prompt user to enter IDs manually for now?
        // Better: Add GET /orders to Controller.
        return [];
    },

    createWave: async (orderIds: string[]) => {
        const { data } = await api.post(`${API_ROOT}/wave`, orderIds);
        return data;
    },

    getTasks: async (waveId?: string) => {
        const query = waveId ? `?waveId=${waveId}` : '';
        const { data } = await api.get<OutboundTask[]>(`${API_ROOT}/tasks${query}`);
        return data;
    }
};
