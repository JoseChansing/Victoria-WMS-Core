import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Search, Filter, Database, Trash2 } from 'lucide-react';
import api from '../../api/axiosConfig';

interface Product {
    sku: string;
    name: string;
    barcode: string;
    category: string;
    isArchived: boolean;
    physicalAttributes?: {
        weight: number;
        length: number;
        width: number;
        height: number;
    };
}

export const SkuMaster: React.FC = () => {
    const queryClient = useQueryClient();

    const { data: products = [], isLoading } = useQuery({
        queryKey: ['products'],
        queryFn: async () => {
            const { data } = await api.get<Product[]>('/products');
            return data;
        }
    });

    const deleteMutation = useMutation({
        mutationFn: async (sku: string) => {
            return await api.delete(`/products/${sku}`);
        },
        onSuccess: () => {
            alert("✅ Producto eliminado correctamente");
            queryClient.invalidateQueries({ queryKey: ['products'] });
        },
        onError: (error: any) => {
            if (error.response) {
                if (error.response.status === 400) {
                    alert("⛔ No se puede eliminar: El producto tiene inventario físico en bodega.");
                } else if (error.response.status === 409) {
                    alert("⚠️ El producto aún existe en Odoo. Por favor elimínelo o archívelo en el ERP primero.");
                } else {
                    alert(`❌ Error al eliminar: ${error.message}`);
                }
            } else {
                alert("❌ Error de conexión al servidor.");
            }
        }
    });

    const handleDelete = (sku: string) => {
        if (window.confirm(`¿Estás seguro de eliminar el producto ${sku}? Esta acción no se puede deshacer.`)) {
            deleteMutation.mutate(sku);
        }
    };

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-corp-accent"></div>
            </div>
        );
    }

    return (
        <div className="space-y-8 animate-in fade-in duration-500">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-black text-white tracking-tight">Maestro de SKUs</h1>
                    <p className="text-slate-400 font-medium">Catálogo consolidado de productos (Sincronizado con Odoo)</p>
                </div>
                <div className="bg-corp-nav/40 text-blue-300 px-5 py-2.5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10 flex items-center space-x-3">
                    <Database className="w-5 h-5" />
                    <span className="text-sm font-bold uppercase tracking-wider">{products.length} Productos</span>
                </div>
            </div>

            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-lg shadow-black/10 overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 flex items-center justify-between bg-corp-base/30">
                    <div className="relative w-96">
                        <Search className="w-4 h-4 absolute left-4 top-1/2 -translate-y-1/2 text-slate-500" />
                        <input
                            type="text"
                            placeholder="Filtrar por SKU o nombre..."
                            className="w-full pl-11 pr-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all placeholder:text-slate-600"
                        />
                    </div>
                    <button className="p-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl hover:bg-corp-accent/40 transition-all text-slate-400 hover:text-white">
                        <Filter className="w-4 h-4" />
                    </button>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="bg-corp-accent/10 border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Item (SKU)</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Descripción</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Barcode</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Categoría</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Peso Und</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Volumen m3</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right">Acciones</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {products.length === 0 ? (
                                <tr>
                                    <td colSpan={7} className="px-6 py-12 text-center text-slate-500 italic text-sm">
                                        No hay productos sincronizados para este tenant.
                                    </td>
                                </tr>
                            ) : (
                                products.map((product) => (
                                    <tr key={product.sku} className={`hover:bg-corp-accent/5 transition-colors group ${product.isArchived ? 'opacity-50' : ''}`}>
                                        <td className="px-6 py-4">
                                            <div className={`w-fit px-4 py-1.5 rounded-xl text-xs font-bold border break-all whitespace-normal max-w-[200px] leading-tight text-center transition-all duration-300 shadow-[0_0_15px_rgba(59,130,246,0.1)] group-hover:scale-105 ${product.isArchived ? 'bg-slate-900 text-slate-500 border-slate-800' : 'bg-gradient-to-br from-blue-600/20 to-corp-accent/10 text-blue-100 border-blue-500/30'}`}>
                                                {product.sku}
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className={`text-sm font-bold ${product.isArchived ? 'text-slate-600 line-through' : 'text-slate-300'}`}>{product.name}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="text-sm font-mono text-slate-400">{product.barcode || '-'}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="text-xs font-bold text-slate-500 uppercase tracking-wider bg-corp-base/50 px-2 py-1 rounded-md border border-corp-secondary/30">
                                                {product.category || 'Sin Categoría'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-sm font-bold text-slate-400">
                                                {product.physicalAttributes?.weight ? `${product.physicalAttributes.weight} kg` : 'N/A'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <span className="text-sm font-mono text-slate-500">
                                                {product.physicalAttributes
                                                    ? (product.physicalAttributes.length * product.physicalAttributes.width * product.physicalAttributes.height / 1000000).toFixed(4)
                                                    : '0.0000'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <button
                                                onClick={() => handleDelete(product.sku)}
                                                className="p-2 text-slate-500 hover:text-rose-400 hover:bg-rose-900/40 rounded-lg transition-all border border-transparent hover:border-rose-900/50"
                                                title="Eliminar producto"
                                            >
                                                <Trash2 className="w-4 h-4" />
                                            </button>
                                        </td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
};
