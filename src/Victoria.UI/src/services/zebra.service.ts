import axios from 'axios';

// Interfaces for Zebra Browser Print API
export interface ZebraPrinter {
    name: string;
    deviceType: string;
    connection: string;
    uid: string;
    provider: string;
    manufacturer: string;
}



export const zebraService = {
    // Default Zebra Browser Print URL
    baseUrl: 'http://localhost:9100',

    /**
     * Check if the service is available
     */
    isAvailable: async (): Promise<boolean> => {
        try {
            await axios.get('http://localhost:9100/available');
            return true;
        } catch (error) {
            return false;
        }
    },

    /**
     * Get the default printer from Zebra Browser Print, with fallback to first available.
     */
    getDefaultPrinter: async (): Promise<ZebraPrinter | null> => {
        try {
            console.log("🔍 Intentando obtener impresora predeterminada...");
            const response = await axios.get('http://localhost:9100/default');

            if (response.data && response.data.uid) {
                console.log("✅ Impresora predeterminada encontrada:", response.data.uid);
                return response.data as ZebraPrinter;
            }

            console.log("⚠️ No hay impresora predeterminada. Buscando disponibles...");
            const available = await zebraService.getPrinters();
            if (available.length > 0) {
                console.log("✅ Usando primera impresora disponible:", available[0].uid);
                return available[0];
            }

            console.warn("❌ No se detectaron impresoras Zebra.");
            return null;
        } catch (error) {
            console.error("Error getting default Zebra printer:", error);
            return null;
        }
    },

    /**
     * Discover all available printers
     */
    getPrinters: async (): Promise<ZebraPrinter[]> => {
        try {
            // "available" endpoint returns all discoverable printers
            const response = await axios.get('http://localhost:9100/available');
            // Check structure, typically response.data.printer is the array
            if (response.data && response.data.printer) {
                return response.data.printer;
            }
            // Fallback if it returns just an array (depends on version)
            if (Array.isArray(response.data)) {
                return response.data;
            }
            return [];
        } catch (error) {
            console.error("Error discovering Zebra printers:", error);
            return [];
        }
    },

    /**
     * Print ZPL code to a specific printer (Atomic Sending - Maximum Compatibility)
     */
    printZpl: async (_: string, zpl: string): Promise<void> => {
        try {
            // 1. Get the current active default printer object (Full info)
            const printer = await zebraService.getDefaultPrinter();

            if (!printer || !printer.uid) {
                const printers = await zebraService.getPrinters();
                if (printers.length === 0) {
                    throw new Error("No Zebra printers found. Ensure the printer is connected and selected as Default.");
                }
                // Fallback to first available with full object
                return await zebraService.executeWrite(printers[0], zpl);
            }

            return await zebraService.executeWrite(printer, zpl);

        } catch (error: any) {
            console.error("Zebra Communication Error:", error);
            throw new Error(error.message || "Failed to communicate with Zebra. Check local Browser Print app.");
        }
    },

    /**
     * Internal write logic with text/plain payload (Zebra Native Format)
     */
    executeWrite: async (printer: ZebraPrinter, zpl: string): Promise<void> => {
        const payload = {
            device: printer, // Sending the FULL object
            data: zpl
        };

        try {
            // Pure JSON object and application/json for modern Zebra Browser Print versions
            await axios.post('http://localhost:9100/write', payload, {
                headers: {
                    'Content-Type': 'application/json'
                }
            });
        } catch (err) {
            console.error("Error in executeWrite:", err);
            throw new Error("⚠️ Error de comunicación con Zebra. Verifique que la impresora esté conectada y seleccionada como Default en la app local.");
        }
    }
};
