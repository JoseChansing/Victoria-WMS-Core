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

export const useInventory = (tenantId: string | null) => {
    const queryClient = useQueryClient();

    const inventoryQuery = useQuery({
        queryKey: ['inventory', tenantId],
        queryFn: async () => {
            // Nota: En un backend real este endpoint filtraría por TenantId
            const { data } = await api.get<LpnItem[]>(`/inventory?tenantId=${tenantId}`);
            return data;
        },
        enabled: !!tenantId,
    });

    const approveAdjustment = useMutation({
        mutationFn: async (params: { lpnId: string, newQuantity: number, reason: string }) => {
            const { data } = await api.post('/inventory/adjust', {
                tenantId,
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
