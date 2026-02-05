// src/Victoria.UI/src/types/inbound.ts

export type ImageSource = 'variant' | 'master' | 'brand' | null;

export interface ReceiptLine {
    id: string;
    sku: string;
    productName: string;
    expectedQty: number;
    receivedQty: number;
    imageSource: ImageSource;
    dimensions?: {
        weight: number;
        length: number;
        width: number;
        height: number;
    };
}

export interface PurchaseOrder {
    id: string;
    poNumber: string;
    supplier: string;
    date: string;
    status: 'Pending' | 'In Progress' | 'Completed';
    totalUnits: number;
    priority: 'Low' | 'Medium' | 'High';
    lines: ReceiptLine[];
}

export interface InboundKPIs {
    pendingOrders: number;
    unitsToReceive: number;
    highPriorityCount: number;
}
