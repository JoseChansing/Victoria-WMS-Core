import { useQuery } from '@tanstack/react-query';
import api from '../api/axiosConfig';
import type { PurchaseOrder, InboundKPIs } from '../types/inbound';

export const useInbound = () => {
    const kpisQuery = useQuery({
        queryKey: ['inbound-kpis'],
        queryFn: async () => {
            const { data } = await api.get<InboundKPIs>('/inbound/kpis');
            return data;
        }
    });

    const ordersQuery = useQuery({
        queryKey: ['inbound-orders'],
        queryFn: async () => {
            const { data } = await api.get<PurchaseOrder[]>('/inbound/orders');
            return data;
        }
    });

    return {
        kpis: kpisQuery.data,
        orders: ordersQuery.data || [],
        isLoading: kpisQuery.isLoading || ordersQuery.isLoading,
        error: kpisQuery.error || ordersQuery.error
    };
};
