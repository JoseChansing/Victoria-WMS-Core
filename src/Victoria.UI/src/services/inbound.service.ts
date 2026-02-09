import api from '../api/axiosConfig';
import type { PurchaseOrder, InboundKPIs } from '../types/inbound';

export interface ReceiveParams {
    orderId: string;
    sku?: string;
    rawScan?: string;
    quantity: number;
    expectedQuantity?: number;
    lpnId?: string;
    lpnCount?: number;
    unitsPerLpn?: number;
}

export const inboundService = {
    getKPIs: async () => {
        const { data } = await api.get<InboundKPIs>('/inbound/kpis');
        return data;
    },

    getOrders: async () => {
        const { data } = await api.get<PurchaseOrder[]>('/inbound/orders');
        return data;
    },

    receiveLpn: async (params: ReceiveParams) => {
        const { data } = await api.post('/inbound/receive', params);
        return data;
    },

    closeOrder: async (orderId: string) => {
        const { data } = await api.post(`/inbound/${orderId}/close`);
        return data;
    },

    printRfid: async (lpnId: string) => {
        const { data } = await api.post(`/printing/lpn/${lpnId}/rfid`);
        return data;
    }
};
