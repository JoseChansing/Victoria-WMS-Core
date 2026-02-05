import { useQuery } from '@tanstack/react-query';
import api from '../api/axiosConfig';
import type { PurchaseOrder, InboundKPIs } from '../types/inbound';

export const useInbound = (tenantId: string | null) => {
    const kpisQuery = useQuery({
        queryKey: ['inbound-kpis', tenantId],
        queryFn: async () => {
            const { data } = await api.get<InboundKPIs>(`/inbound/kpis?tenantId=${tenantId}`);
            return data;
        },
        enabled: !!tenantId,
    });

    const ordersQuery = useQuery({
        queryKey: ['inbound-orders', tenantId],
        queryFn: async () => {
            const { data } = await api.get<PurchaseOrder[]>(`/inbound/orders?tenantId=${tenantId}`);
            return data;
        },
        enabled: !!tenantId,
    });

    return {
        kpis: kpisQuery.data,
        orders: ordersQuery.data || [],
        isLoading: kpisQuery.isLoading || ordersQuery.isLoading,
        error: kpisQuery.error || ordersQuery.error
    };
};
