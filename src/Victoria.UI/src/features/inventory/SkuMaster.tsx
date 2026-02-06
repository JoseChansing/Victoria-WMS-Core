import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Filter, Database } from 'lucide-react';
import api from '../../api/axiosConfig';
import { useAuth } from '../../context/AuthContext';

interface Product {
    sku: string;
    name: string;
    odooId: number;
}

export const SkuMaster: React.FC = () => {
    const { tenant } = useAuth();

    const { data: products = [], isLoading } = useQuery({
        queryKey: ['products', tenant],
        queryFn: async () => {
            const { data } = await api.get<Product[]>(`/products?tenantId=${tenant}`);
            return data;
        },
        enabled: !!tenant
    });

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-[400px]">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
            </div>
        );
    }

    return (
        <div className="space-y-8 animate-fade-in">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-black text-slate-900 tracking-tight">Maestro de SKUs</h1>
                    <p className="text-slate-500 font-medium">Catálogo consolidado de productos (Sincronizado con Odoo)</p>
                </div>
                <div className="bg-blue-50 text-blue-700 px-4 py-2 rounded-xl border border-blue-100 flex items-center space-x-2">
                    <Database className="w-4 h-4" />
                    <span className="text-xs font-bold uppercase tracking-wider">{products.length} Productos</span>
                </div>
            </div>

            <div className="bg-white rounded-3xl border border-slate-100 shadow-sm overflow-hidden">
                <div className="p-6 border-b border-slate-50 flex items-center justify-between">
                    <div className="relative">
                        <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
                        <input
                            type="text"
                            placeholder="Filtrar por SKU o nombre..."
                            className="pl-10 pr-4 py-2 bg-slate-50 border-none rounded-xl text-sm focus:ring-2 focus:ring-blue-500 w-80 transition-all"
                        />
                    </div>
                    <button className="p-2 bg-slate-50 rounded-xl hover:bg-slate-100 transition-colors">
                        <Filter className="w-4 h-4 text-slate-600" />
                    </button>
                </div>

                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="border-b border-slate-50">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Odoo ID</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">SKU</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest">Nombre del Producto</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center">Estado Sinc</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-50">
                            {products.length === 0 ? (
                                <tr>
                                    <td colSpan={4} className="px-6 py-12 text-center text-slate-400 italic text-sm">
                                        No hay productos sincronizados para este tenant.
                                    </td>
                                </tr>
                            ) : (
                                products.map((product) => (
                                    <tr key={product.sku} className="hover:bg-slate-50/50 transition-colors group">
                                        <td className="px-6 py-4">
                                            <span className="font-mono font-bold text-xs text-slate-500">#{product.odooId}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="px-3 py-1 bg-slate-100 rounded-lg text-sm font-black text-slate-900 border border-slate-200">
                                                {product.sku}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="text-sm font-bold text-slate-700">{product.name}</span>
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <div className="inline-flex items-center px-2 py-1 bg-emerald-50 text-emerald-600 rounded-md text-[10px] font-black">
                                                ACTIVE
                                            </div>
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
