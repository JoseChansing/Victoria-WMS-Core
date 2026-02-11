// src/Victoria.UI/src/features/inventory/SkuMaster.tsx
import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import {
    Search, Database, ChevronLeft, ChevronRight, ChevronDown,
    Filter, X, Trash2,
    Tag, Layers, Image, Loader2
} from 'lucide-react';
import api from '../../api/axiosConfig';

interface Product {
    sku: string;
    name: string;
    barcode: string;
    category: string;
    brand?: string;
    sides?: string;
    isArchived: boolean;
    hasImage: boolean;
    physicalAttributes?: {
        weight: number;
        length: number;
        width: number;
        height: number;
    };
}

interface PagedResult<T> {
    items: T[];
    totalItems: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export const SkuMaster: React.FC = () => {
    const queryClient = useQueryClient();
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(50);
    const [rangeInput, setRangeInput] = useState('');
    const [searchTerm, setSearchTerm] = useState('');
    const [searchTags, setSearchTags] = useState<string[]>([]);
    const [serverSearch, setServerSearch] = useState(''); // Combined value for query
    const [showSuggestions, setShowSuggestions] = useState(false);

    // Filters State (Free Text Search)
    const [brandFilter, setBrandFilter] = useState('');
    const [categoryFilter, setCategoryFilter] = useState('');
    const [selectedHasImage, setSelectedHasImage] = useState('');

    // Debounce Search Effect (Search + Filters)
    React.useEffect(() => {
        const timer = setTimeout(() => {
            const combinedSearch = [...searchTags, searchTerm].filter(Boolean).join(',');
            setServerSearch(combinedSearch);
            setPage(1);
        }, 300);
        return () => clearTimeout(timer);
    }, [searchTerm, searchTags, brandFilter, categoryFilter]);

    // Suggestions Query
    const { data: suggestionsData, isLoading: suggestionsLoading } = useQuery({
        queryKey: ['suggestions', searchTerm],
        queryFn: async () => {
            if (searchTerm.length < 2) return [];
            const { data } = await api.get<PagedResult<Product>>(`products?search=${searchTerm}&pageSize=10`);
            return data.items;
        },
        enabled: searchTerm.length >= 2 && showSuggestions
    });

    const suggestions = suggestionsData || [];

    const { data, isLoading } = useQuery({
        queryKey: ['products', page, pageSize, serverSearch, brandFilter, categoryFilter, selectedHasImage],
        queryFn: async () => {
            const params = new URLSearchParams();
            params.append('page', page.toString());
            params.append('pageSize', pageSize.toString());
            if (serverSearch) params.append('search', serverSearch);
            if (brandFilter) params.append('brand', brandFilter);
            if (categoryFilter) params.append('category', categoryFilter);
            if (selectedHasImage) params.append('hasImage', selectedHasImage);

            const { data } = await api.get<PagedResult<Product>>(`products?${params.toString()}`);
            return data;
        },
        placeholderData: keepPreviousData
    });

    const products = data?.items || [];
    const totalItems = data?.totalItems || 0;

    const deleteMutation = useMutation({
        mutationFn: async (sku: string) => {
            return await api.delete(`products/${sku}`);
        },
        onSuccess: () => {
            alert("✅ Product successfully deleted");
            queryClient.invalidateQueries({ queryKey: ['products'] });
        },
        onError: (error: any) => {
            if (error.response) {
                if (error.response.status === 400) {
                    alert("⛔ Cannot delete: Product has physical inventory in warehouse.");
                } else if (error.response.status === 409) {
                    alert("⚠️ Product still exists in Odoo. Please delete or archive it in the ERP first.");
                } else {
                    alert(`❌ Error deleting: ${error.message}`);
                }
            } else {
                alert("❌ Server connection error.");
            }
        }
    });

    const handleDelete = (sku: string) => {
        if (window.confirm(`Are you sure you want to delete product ${sku}? This action cannot be undone.`)) {
            deleteMutation.mutate(sku);
        }
    };

    const handleAddTag = (tag: string) => {
        const cleanTag = tag.trim().toUpperCase();
        if (cleanTag && !searchTags.includes(cleanTag)) {
            setSearchTags([...searchTags, cleanTag]);
        }
        setSearchTerm('');
        setShowSuggestions(false);
    };

    const handleRemoveTag = (tagToRemove: string) => {
        setSearchTags(searchTags.filter(t => t !== tagToRemove));
    };

    const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            if (searchTerm) {
                handleAddTag(searchTerm);
            }
        } else if (e.key === 'Backspace' && !searchTerm && searchTags.length > 0) {
            handleRemoveTag(searchTags[searchTags.length - 1]);
        }
    };

    React.useEffect(() => {
        if (totalItems > 0) {
            const start = (page - 1) * pageSize + 1;
            const end = Math.min(page * pageSize, totalItems);
            setRangeInput(`${start}-${end}`);
        } else {
            setRangeInput('0-0');
        }
    }, [page, pageSize, totalItems]);

    const handleRangeCommit = () => {
        const parts = rangeInput.split(/[-/]/);
        if (parts.length === 2) {
            const start = parseInt(parts[0].trim());
            const end = parseInt(parts[1].trim());

            if (!isNaN(start) && !isNaN(end) && end >= start && start > 0) {
                const newSize = end - start + 1;
                const newPage = Math.floor((start - 1) / newSize) + 1;
                const safeSize = Math.min(newSize, 10000);
                setPageSize(safeSize);
                setPage(newPage);
                return;
            }
        }
        const start = (page - 1) * pageSize + 1;
        const end = Math.min(page * pageSize, totalItems);
        setRangeInput(`${start}-${end}`);
    };

    return (
        <div className="space-y-4 animate-in fade-in duration-500 h-[calc(100vh-140px)] flex flex-col">
            <div className="flex items-center justify-between shrink-0 px-2">
                <div>
                    <h1 className="text-2xl font-black text-white tracking-tight">SKU Master</h1>
                    <p className="text-slate-400 font-medium">Consolidated catalog ({totalItems.toLocaleString()} items)</p>
                </div>
                <div className="bg-corp-nav/40 text-blue-300 px-5 py-2.5 rounded-2xl border border-corp-secondary shadow-lg shadow-black/10 flex items-center space-x-3">
                    <Database className="w-5 h-5" />
                    <span className="text-sm font-bold uppercase tracking-wider">
                        {totalItems.toLocaleString()} Products
                    </span>
                </div>
            </div>

            <div className="bg-corp-nav/40 rounded-3xl border border-corp-secondary shadow-lg shadow-black/10 flex flex-col flex-1 overflow-hidden">
                <div className="p-6 border-b border-corp-secondary/50 flex flex-col space-y-4 bg-corp-base/30 shrink-0">
                    <div className="flex items-center justify-between gap-4">
                        <div className="relative w-full max-w-2xl">
                            <div className={`flex flex-wrap items-center gap-2 p-1.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl min-h-[46px] focus-within:ring-2 focus-within:ring-corp-accent transition-all ${showSuggestions && suggestions.length > 0 ? 'rounded-b-none' : ''}`}>
                                <Search className="w-4 h-4 ml-3 text-slate-500 shrink-0" />

                                {searchTags.map(tag => (
                                    <span key={tag} className="flex items-center gap-1 px-2 py-1 bg-blue-600/20 text-blue-100 border border-blue-500/30 rounded-lg text-xs font-bold animate-in zoom-in-95 duration-200">
                                        {tag}
                                        <button onClick={() => handleRemoveTag(tag)} className="hover:text-rose-400 transition-colors">
                                            <X className="w-3 h-3" />
                                        </button>
                                    </span>
                                ))}

                                <input
                                    type="text"
                                    value={searchTerm}
                                    onChange={(e) => {
                                        setSearchTerm(e.target.value);
                                        setShowSuggestions(true);
                                    }}
                                    onBlur={() => setTimeout(() => setShowSuggestions(false), 200)}
                                    onKeyDown={handleInputKeyDown}
                                    placeholder={searchTags.length > 0 ? "Add more..." : "Filter by SKU or name..."}
                                    className="flex-1 min-w-[120px] bg-transparent border-none focus:ring-0 text-sm text-white placeholder:text-slate-600 outline-none"
                                />
                                {(isLoading || suggestionsLoading) && <Loader2 className="w-4 h-4 mr-3 text-blue-500 animate-spin shrink-0" />}
                            </div>

                            {showSuggestions && suggestions.length > 0 && (
                                <div className="absolute top-full left-0 right-0 bg-corp-nav border-x border-b border-corp-secondary rounded-b-xl shadow-2xl z-50 overflow-hidden animate-in slide-in-from-top-2 duration-200">
                                    {suggestions.map((s) => (
                                        <button
                                            key={s.sku}
                                            onClick={() => handleAddTag(s.sku)}
                                            className="w-full px-5 py-3 text-left hover:bg-corp-accent/20 transition-colors flex items-center justify-between group"
                                        >
                                            <div className="flex flex-col">
                                                <span className="text-sm font-bold text-white group-hover:text-corp-accent transition-colors">{s.sku}</span>
                                                <span className="text-xs text-slate-400 truncate max-w-[400px]">{s.name}</span>
                                            </div>
                                            <span className="text-[10px] font-black text-slate-600 uppercase tracking-widest bg-corp-base/50 px-2 py-1 rounded border border-corp-secondary/30">
                                                {s.category || 'No Cat'}
                                            </span>
                                        </button>
                                    ))}
                                </div>
                            )}
                        </div>

                        <div className="flex items-center gap-4">
                            {/* Filter Menu (Odoo Style - FIXED HOVER) */}
                            <div className="relative group/menu py-1"> {/* py-1 creates a protection zone to avoid gaps */}
                                <button
                                    className="flex items-center gap-2 px-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-xl text-sm font-bold text-slate-300 hover:text-white hover:bg-corp-accent/10 hover:border-corp-accent/50 transition-all data-[active=true]:text-corp-accent data-[active=true]:border-corp-accent"
                                    data-active={brandFilter !== '' || categoryFilter !== '' || selectedHasImage !== ''}
                                >
                                    <Filter className="w-4 h-4" />
                                    <span>Filters</span>
                                    {(brandFilter !== '' || categoryFilter !== '' || selectedHasImage !== '') && (
                                        <span className="flex items-center justify-center w-5 h-5 ml-1 text-[10px] bg-corp-accent text-white rounded-full">
                                            {(brandFilter ? 1 : 0) + (categoryFilter ? 1 : 0) + (selectedHasImage ? 1 : 0)}
                                        </span>
                                    )}
                                    <ChevronDown className="w-3 h-3 ml-1 opacity-50" />
                                </button>

                                <div className="absolute top-full left-0 mt-0 w-64 bg-corp-nav border border-corp-secondary rounded-xl shadow-2xl z-50 overflow-visible p-1 hidden group-hover/menu:block hover:block animate-in fade-in zoom-in-95 duration-200">
                                    {/* Brand Filter Input */}
                                    <div className="p-2 pt-3">
                                        <div className="flex items-center gap-2 px-3 py-1.5 bg-corp-base/50 border border-corp-secondary/50 rounded-lg group-within:border-corp-accent/50 transition-colors">
                                            <Tag className="w-4 h-4 text-indigo-400" />
                                            <input
                                                type="text"
                                                placeholder="Filter by Brand..."
                                                value={brandFilter}
                                                onChange={(e) => { setBrandFilter(e.target.value); setPage(1); }}
                                                className="bg-transparent border-none focus:ring-0 text-xs text-white placeholder:text-slate-600 outline-none w-full"
                                            />
                                            {brandFilter && (
                                                <button onClick={() => setBrandFilter('')}>
                                                    <X className="w-3 h-3 text-slate-500 hover:text-white" />
                                                </button>
                                            )}
                                        </div>
                                    </div>

                                    {/* Category Filter Input */}
                                    <div className="p-2">
                                        <div className="flex items-center gap-2 px-3 py-1.5 bg-corp-base/50 border border-corp-secondary/50 rounded-lg group-within:border-corp-accent/50 transition-colors">
                                            <Layers className="w-4 h-4 text-emerald-400" />
                                            <input
                                                type="text"
                                                placeholder="Filter by Category..."
                                                value={categoryFilter}
                                                onChange={(e) => { setCategoryFilter(e.target.value); setPage(1); }}
                                                className="bg-transparent border-none focus:ring-0 text-xs text-white placeholder:text-slate-600 outline-none w-full"
                                            />
                                            {categoryFilter && (
                                                <button onClick={() => setCategoryFilter('')}>
                                                    <X className="w-3 h-3 text-slate-500 hover:text-white" />
                                                </button>
                                            )}
                                        </div>
                                    </div>

                                    <div className="h-px bg-corp-secondary/30 my-1"></div>

                                    <div className="h-px bg-corp-secondary/30 my-1"></div>

                                    <div className="relative group/submenu">
                                        <button className="w-full flex items-center justify-between px-3 py-2 text-sm text-slate-300 hover:bg-corp-base/50 hover:text-white rounded-lg transition-colors">
                                            <div className="flex items-center gap-2">
                                                <Image className="w-4 h-4 text-amber-400" />
                                                <span>Image</span>
                                            </div>
                                            <ChevronRight className="w-3 h-3 text-slate-500" />
                                        </button>
                                        {/* Submenu with hover bridge */}
                                        <div className="absolute top-0 left-full w-48 bg-corp-nav border border-corp-secondary rounded-xl shadow-2xl p-2 hidden group-hover/submenu:block hover:block animate-in slide-in-from-left-2 duration-200">
                                            {/* Invisible bridge to maintain hover state across the gap */}
                                            <div className="absolute -left-4 top-0 bottom-0 w-4 bg-transparent" />

                                            <button onClick={() => { setSelectedHasImage(''); setPage(1); }} className={`w-full text-left px-3 py-1.5 text-sm rounded-lg mb-1 ${selectedHasImage === '' ? 'bg-corp-accent/20 text-corp-accent font-bold' : 'text-slate-400 hover:bg-corp-base/50 hover:text-white'}`}>All Products</button>
                                            <button onClick={() => { setSelectedHasImage('true'); setPage(1); }} className={`w-full text-left px-3 py-1.5 text-sm rounded-lg mb-1 ${selectedHasImage === 'true' ? 'bg-corp-accent/20 text-corp-accent font-bold' : 'text-slate-400 hover:bg-corp-base/50 hover:text-white'}`}>With Image</button>
                                            <button onClick={() => { setSelectedHasImage('false'); setPage(1); }} className={`w-full text-left px-3 py-1.5 text-sm rounded-lg ${selectedHasImage === 'false' ? 'bg-corp-accent/20 text-corp-accent font-bold' : 'text-slate-400 hover:bg-corp-base/50 hover:text-white'}`}>No Image</button>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div className="flex items-center space-x-3">
                                <div className="flex items-center space-x-2">
                                    <input
                                        type="text"
                                        value={rangeInput}
                                        onChange={(e) => setRangeInput(e.target.value)}
                                        onBlur={handleRangeCommit}
                                        onKeyDown={(e) => e.key === 'Enter' && handleRangeCommit()}
                                        className="w-24 bg-corp-base/50 border border-corp-secondary/50 rounded-lg text-center text-sm font-bold text-corp-accent py-1.5 focus:ring-2 focus:ring-corp-accent outline-none font-mono"
                                    />
                                    <span className="text-slate-500 font-bold">/</span>
                                    <span className="text-slate-400 font-bold">{totalItems}</span>
                                </div>
                                <div className="h-4 w-px bg-corp-secondary/50 mx-1"></div>
                                <div className="flex items-center space-x-1">
                                    <button onClick={() => setPage(old => Math.max(old - 1, 1))} disabled={page === 1 || isLoading} className="p-2 bg-corp-base/50 border border-corp-secondary/50 rounded-lg hover:bg-corp-accent/40 transition-all text-slate-400 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed active:scale-95">
                                        <ChevronLeft className="w-4 h-4" />
                                    </button>
                                    <button onClick={() => setPage(old => (data?.hasNextPage ? old + 1 : old))} disabled={!data?.hasNextPage || isLoading} className="p-2 bg-corp-base/50 border border-corp-secondary/50 rounded-lg hover:bg-corp-accent/40 transition-all text-slate-400 hover:text-white disabled:opacity-30 disabled:cursor-not-allowed active:scale-95">
                                        <ChevronRight className="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                <div className="flex-1 overflow-auto bg-corp-base/20 relative no-scrollbar">
                    {isLoading && !data && (
                        <div className="absolute inset-0 flex items-center justify-center bg-black/20 backdrop-blur-sm z-10">
                            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-corp-accent"></div>
                        </div>
                    )}

                    <table className="w-full text-left border-collapse">
                        <thead className="sticky top-0 z-20 bg-corp-base shadow-sm ring-1 ring-white/5">
                            <tr className="border-b border-corp-secondary/30">
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest bg-corp-base">Item (SKU)</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest bg-corp-base">Description</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest bg-corp-base">Barcode</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest bg-corp-base">Brand</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center bg-corp-base">Sides</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest bg-corp-base">Category</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center bg-corp-base">Unit Weight</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right bg-corp-base">Volume m3 (Unit)</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-center bg-corp-base">Image</th>
                                <th className="px-6 py-4 text-[10px] font-black text-slate-400 uppercase tracking-widest text-right bg-corp-base">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/20">
                            {products.length === 0 ? (
                                <tr>
                                    <td colSpan={10} className="px-6 py-12 text-center text-slate-500 italic text-sm">
                                        No products found{searchTerm ? ` matching "${searchTerm}"` : ''}.
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
                                            {product.brand ? (
                                                <div className={`w-fit px-3 py-1 rounded-xl text-[10px] font-black uppercase tracking-wider border transition-all duration-300 ${product.isArchived ? 'bg-slate-900 text-slate-600 border-slate-800' : 'bg-blue-600/10 text-blue-300 border-blue-500/20 shadow-[0_0_10px_rgba(59,130,246,0.05)]'}`}>
                                                    {product.brand}
                                                </div>
                                            ) : (
                                                <span className="text-xs text-slate-600">-</span>
                                            )}
                                        </td>
                                        <td className="px-6 py-4 text-center">
                                            <span className="text-xs font-mono text-slate-400">
                                                {product.sides || '-'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className={`w-fit px-3 py-1 rounded-xl text-[10px] font-black uppercase tracking-wider border transition-all duration-300 ${product.isArchived ? 'bg-slate-900 text-slate-600 border-slate-800' : 'bg-corp-accent/10 text-slate-300 border-corp-secondary/30 shadow-[0_0_10px_rgba(59,130,246,0.05)]'}`}>
                                                {product.category || 'No Category'}
                                            </div>
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
                                        <td className="px-6 py-4 text-center">
                                            <span className={`px-3 py-1 rounded-lg text-[10px] font-black uppercase tracking-wider border ${product.hasImage ? 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20' : 'bg-slate-500/10 text-slate-500 border-slate-500/20'}`}>
                                                {product.hasImage ? 'Yes' : 'No'}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <button
                                                onClick={() => handleDelete(product.sku)}
                                                className="p-2 text-slate-500 hover:text-rose-400 hover:bg-rose-900/40 rounded-lg transition-all border border-transparent hover:border-rose-900/50"
                                                title="Delete product"
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
