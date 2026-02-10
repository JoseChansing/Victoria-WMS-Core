import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from '../api/axiosConfig';

export interface LpnItem {
    id: string;
    code: string;
    sku: string;
    quantity: number;
    status: 'Created' | 'Received' | 'Putaway' | 'Allocated' | 'Picked' | 'Dispatched' | 'Quarantine';
    location: string;
    tenantId: string;
}

export interface LpnHistoryEntry {
    eventType: string;
    description: string;
    user: string;
    timestamp: string;
}

export interface LpnHistory {
    id: string;
    entries: LpnHistoryEntry[];
}

export const useInventory = () => {
    const queryClient = useQueryClient();

    const inventoryQuery = useQuery({
        queryKey: ['inventory'],
        queryFn: async () => {
            const { data } = await api.get<LpnItem[]>('/inventory/lpns');
            return data;
        }
    });

    const approveAdjustment = useMutation({
        mutationFn: async (params: { lpnId: string, newQuantity: number, reason: string }) => {
            const { data } = await api.post('/inventory/adjust', {
                lpnId: params.lpnId,
                newQuantity: params.newQuantity,
                reason: params.reason,
                supervisorId: 'SUPER-UI-01'
            });
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['inventory'] });
        }
    });

    return {
        inventory: inventoryQuery.data || [],
        isLoading: inventoryQuery.isLoading,
        approveAdjustment
    };
};

export const useLpnHistory = (lpnId: string | null) => {
    return useQuery({
        queryKey: ['lpn-history', lpnId],
        queryFn: async () => {
            if (!lpnId) return null;
            const { data } = await api.get<LpnHistory>(`/inventory/lpns/${lpnId}/history`);
            return data;
        },
        enabled: !!lpnId
    });
};
