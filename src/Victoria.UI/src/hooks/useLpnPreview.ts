// src/Victoria.UI/src/hooks/useLpnPreview.ts
import { useMemo } from 'react';

type TenantName = 'PerfectPTY' | 'Natsuki' | 'PDM' | 'Filtros';

const PREFIX_MAP: Record<TenantName, string> = {
    'PerfectPTY': 'PTC',
    'Natsuki': 'NAT',
    'PDM': 'PDM',
    'Filtros': 'FLT',
};

export const useLpnPreview = (tenantName: string, sequence: number = 1) => {
    return useMemo(() => {
        const prefix = PREFIX_MAP[tenantName as TenantName] || 'LPN';
        const paddedSequence = sequence.toString().padStart(16, '0');
        return `${prefix}${paddedSequence}`;
    }, [tenantName, sequence]);
};
