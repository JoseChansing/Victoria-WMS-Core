// src/Victoria.UI/src/hooks/useVolumeCalc.ts
import { useMemo } from 'react';

export interface BoxDimensions {
    length: number; // cm
    width: number;  // cm
    height: number; // cm
}

export const useVolumeCalc = (dimensions: BoxDimensions) => {
    return useMemo(() => {
        const { length, width, height } = dimensions;
        if (!length || !width || !height) return "0.0000";

        // Volume in m3: (L * W * H) / 1,000,000
        const volumeM3 = (length * width * height) / 1000000;
        return volumeM3.toFixed(4);
    }, [dimensions.length, dimensions.width, dimensions.height]);
};
