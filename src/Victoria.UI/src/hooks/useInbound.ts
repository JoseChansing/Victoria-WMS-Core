import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { inboundService, type ReceiveParams } from '../services/inbound.service';

export const useInbound = () => {
    const queryClient = useQueryClient();

    const kpisQuery = useQuery({
        queryKey: ['inbound-kpis'],
        queryFn: inboundService.getKPIs
    });

    const ordersQuery = useQuery({
        queryKey: ['inbound-orders'],
        queryFn: inboundService.getOrders
    });

    const receiveMutation = useMutation({
        mutationFn: (params: ReceiveParams) => inboundService.receiveLpn(params),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['inbound-kpis'] });
            queryClient.invalidateQueries({ queryKey: ['inbound-orders'] });
        }
    });

    const closeOrderMutation = useMutation({
        mutationFn: (orderId: string) => inboundService.closeOrder(orderId),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['inbound-kpis'] });
            queryClient.invalidateQueries({ queryKey: ['inbound-orders'] });
        }
    });

    const patchOrderMutation = useMutation({
        mutationFn: ({ orderId, params }: { orderId: string, params: any }) =>
            inboundService.patchOrder(orderId, params),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['inbound-orders'] });
        }
    });

    return {
        kpis: kpisQuery.data,
        orders: (ordersQuery.data || []) as any[],
        isLoading: kpisQuery.isLoading || ordersQuery.isLoading,
        error: kpisQuery.error || ordersQuery.error,
        receiveLpn: receiveMutation.mutateAsync,
        isReceiving: receiveMutation.isPending,
        closeOrder: closeOrderMutation.mutateAsync,
        isClosing: closeOrderMutation.isPending,
        patchOrder: patchOrderMutation.mutateAsync,
        isPatching: patchOrderMutation.isPending,
        printRfid: (lpnId: string) => inboundService.printRfid(lpnId)
    };
};

