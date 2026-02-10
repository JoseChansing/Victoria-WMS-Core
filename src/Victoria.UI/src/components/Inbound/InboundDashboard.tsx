import React, { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    Truck,
    Search,
    Calendar,
    ChevronLeft,
    ChevronRight,
    CheckCircle2,
    Lock,
    AlertCircle,
    Clock,
    CheckSquare,
    Loader2
} from 'lucide-react';
import { useInbound } from '../../hooks/useInbound';

const InboundDashboard: React.FC = () => {
    const navigate = useNavigate();
    const { kpis, orders, isLoading, closeOrder, isClosing } = useInbound();

    // State for filters
    const [searchTerm, setSearchTerm] = useState('');
    const [statusFilter, setStatusFilter] = useState('All');
    const [dateFrom, setDateFrom] = useState('');
    const [dateTo, setDateTo] = useState('');
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 8;

    // Modal state
    const [confirmingOrder, setConfirmingOrder] = useState<any>(null);

    // Success message state
    const [lastClosedOrder, setLastClosedOrder] = useState<string | null>(null);

    // Filtering logic
    const filteredOrders = useMemo(() => {
        return orders.filter(order => {
            const matchesSearch =
                (order.orderNumber || '').toLowerCase().includes(searchTerm.toLowerCase()) ||
                (order.supplier || '').toLowerCase().includes(searchTerm.toLowerCase());

            const matchesStatus = statusFilter === 'All' || order.status === statusFilter;

            const orderDate = new Date(order.date);
            const matchesDateFrom = !dateFrom || orderDate >= new Date(dateFrom);
            const matchesDateTo = !dateTo || orderDate <= new Date(dateTo);

            return matchesSearch && matchesStatus && matchesDateFrom && matchesDateTo;
        });
    }, [orders, searchTerm, statusFilter, dateFrom, dateTo]);

    // Pagination logic
    const totalPages = Math.ceil(filteredOrders.length / itemsPerPage);
    const paginatedOrders = filteredOrders.slice(
        (currentPage - 1) * itemsPerPage,
        currentPage * itemsPerPage
    );

    const handleCloseOrderRequest = (order: any) => {
        setConfirmingOrder(order);
    };

    const handleFinalizeClose = async () => {
        if (!confirmingOrder) return;
        const orderId = confirmingOrder.id;
        const orderNumber = confirmingOrder.orderNumber;

        try {
            setConfirmingOrder(null);
            await closeOrder(orderId);
            setLastClosedOrder(orderNumber);
            setTimeout(() => setLastClosedOrder(null), 5000);
        } catch (error) {
            console.error('Failed to close order:', error);
        }
    };

    const getStatusColor = (status: string) => {
        switch (status) {
            case 'Pending': return 'bg-slate-700/50 text-slate-300 border-slate-600';
            case 'Ready': return 'bg-corp-accent/40 text-blue-300 border-corp-secondary';
            case 'In Progress': return 'bg-amber-900/40 text-amber-500 border-amber-800 animate-pulse';
            case 'Completed': return 'bg-emerald-900/40 text-emerald-500 border-emerald-800';
            default: return 'bg-corp-base/50 text-slate-400 border-corp-secondary';
        }
    };

    if (isLoading) {
        return (
            <div className="flex h-[80vh] items-center justify-center">
                <div className="flex flex-col items-center gap-4">
                    <Loader2 className="w-12 h-12 text-blue-600 animate-spin" />
                    <p className="text-slate-500 font-medium">Loading management dashboard...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6 animate-in fade-in duration-500">
            {/* Header & Stats */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-white tracking-tight">Inbound Dashboard</h1>
                    <p className="text-slate-400 text-sm">Monitoring and closing inbound orders</p>
                </div>

                <div className="flex gap-4">
                    <div className="bg-corp-nav/40 px-5 py-3 rounded-2xl shadow-lg shadow-black/10 border border-corp-secondary flex items-center gap-3">
                        <div className="p-2 bg-corp-accent/50 rounded-lg">
                            <Clock className="w-5 h-5 text-blue-300" />
                        </div>
                        <div>
                            <p className="text-[10px] uppercase font-bold text-slate-400 tracking-wider">Open</p>
                            <p className="text-xl font-bold text-white">{kpis?.pendingOrders ?? 0}</p>
                        </div>
                    </div>
                    <div className="bg-corp-nav/40 px-5 py-3 rounded-2xl shadow-lg shadow-black/10 border border-corp-secondary flex items-center gap-3">
                        <div className="p-2 bg-emerald-900/40 rounded-lg">
                            <CheckSquare className="w-5 h-5 text-emerald-400" />
                        </div>
                        <div>
                            <p className="text-[10px] uppercase font-bold text-slate-400 tracking-wider">Today</p>
                            <p className="text-xl font-bold text-white">12</p>
                        </div>
                    </div>
                </div>
            </div>

            {/* Success Alert */}
            {lastClosedOrder && (
                <div className="bg-emerald-900/20 border border-emerald-500/30 text-emerald-400 px-4 py-3 rounded-xl flex items-center gap-3 animate-in slide-in-from-top-4">
                    <CheckCircle2 className="w-5 h-5" />
                    <p className="font-medium text-sm">
                        Order <strong>{lastClosedOrder}</strong> closed. Inventory transferred to STAGE-RESERVE and STAGE-PICKING.
                    </p>
                </div>
            )}

            {/* Toolbar */}
            <div className="bg-corp-nav/40 p-4 rounded-3xl shadow-lg shadow-black/10 border border-corp-secondary flex flex-wrap gap-4 items-center">
                <div className="relative flex-1 min-w-[240px]">
                    <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                    <input
                        type="text"
                        placeholder="Search by PO # or Supplier..."
                        className="w-full pl-11 pr-4 py-2.5 bg-corp-base/50 border border-corp-secondary/50 rounded-2xl text-sm text-white focus:ring-2 focus:ring-corp-accent transition-all placeholder:text-slate-600"
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                    />
                </div>

                <div className="flex items-center gap-2 bg-corp-base/50 rounded-2xl p-1 shrink-0 border border-corp-secondary/30">
                    <button
                        onClick={() => setStatusFilter('All')}
                        className={`px-4 py-1.5 rounded-xl text-xs font-bold transition-all ${statusFilter === 'All' ? 'bg-corp-accent text-white shadow-md' : 'text-slate-500 hover:text-slate-300'}`}
                    >
                        All
                    </button>
                    <button
                        onClick={() => setStatusFilter('Pending')}
                        className={`px-4 py-1.5 rounded-xl text-xs font-bold transition-all ${statusFilter === 'Pending' ? 'bg-corp-accent text-white shadow-md' : 'text-slate-500 hover:text-slate-300'}`}
                    >
                        Pending
                    </button>
                    <button
                        onClick={() => setStatusFilter('In Progress')}
                        className={`px-4 py-1.5 rounded-xl text-xs font-bold transition-all ${statusFilter === 'In Progress' ? 'bg-corp-accent text-white shadow-md' : 'text-slate-500 hover:text-slate-300'}`}
                    >
                        Receiving
                    </button>
                    <button
                        onClick={() => setStatusFilter('Completed')}
                        className={`px-4 py-1.5 rounded-xl text-xs font-bold transition-all ${statusFilter === 'Completed' ? 'bg-corp-accent text-white shadow-md' : 'text-slate-500 hover:text-slate-300'}`}
                    >
                        Closed
                    </button>
                </div>

                <div className="flex items-center gap-2 shrink-0">
                    <div className="flex items-center gap-2 bg-corp-base/50 px-3 py-2 rounded-2xl border border-corp-secondary/30">
                        <Calendar className="w-4 h-4 text-slate-500" />
                        <input
                            type="date"
                            className="bg-transparent border-none text-xs text-slate-300 focus:ring-0 p-0 cursor-pointer [color-scheme:dark]"
                            value={dateFrom}
                            onChange={(e) => setDateFrom(e.target.value)}
                        />
                        <span className="text-slate-600 mx-1">/</span>
                        <input
                            type="date"
                            className="bg-transparent border-none text-xs text-slate-300 focus:ring-0 p-0 cursor-pointer [color-scheme:dark]"
                            value={dateTo}
                            onChange={(e) => setDateTo(e.target.value)}
                        />
                    </div>
                </div>
            </div>

            {/* Table Area */}
            <div className="bg-corp-nav/40 rounded-3xl shadow-lg shadow-black/10 border border-corp-secondary overflow-hidden">
                <div className="overflow-x-auto">
                    <table className="w-full text-left">
                        <thead>
                            <tr className="bg-corp-accent/10 border-b border-corp-secondary/50">
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400">PO Reference</th>
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400">Partner / Supplier</th>
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400">Date</th>
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400 text-center">Progress</th>
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400">Status</th>
                                <th className="px-6 py-4 text-[10px] font-bold uppercase tracking-wider text-slate-400 text-right">Actions</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-corp-secondary/30">
                            {paginatedOrders.length > 0 ? paginatedOrders.map((order) => {
                                const receivedUnits = order.lines.reduce((acc, l) => acc + l.receivedQty, 0);
                                const progress = Math.round((receivedUnits / order.totalUnits) * 100);
                                const visualProgress = Math.min(100, progress);

                                return (
                                    <tr key={order.id} className="hover:bg-corp-accent/5 transition-colors group">
                                        <td className="px-6 py-4">
                                            <div className="flex items-center gap-3">
                                                <div className="p-2 bg-corp-accent/20 rounded-lg group-hover:bg-corp-accent/40 transition-colors">
                                                    <Truck className="w-4 h-4 text-blue-300" />
                                                </div>
                                                <span className="font-mono font-bold text-white tracking-wide">{order.orderNumber}</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className="text-sm font-medium text-slate-300">{order.supplier}</span>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="flex items-center gap-1.5 text-slate-400">
                                                <Calendar className="w-3.5 h-3.5" />
                                                <span className="text-xs font-medium">{new Date(order.date).toLocaleDateString()}</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <div className="flex flex-col gap-1.5 items-center">
                                                <div className="w-24 bg-corp-base rounded-full h-1.5 overflow-hidden border border-corp-secondary/30">
                                                    <div
                                                        className={`h-full transition-all duration-700 ${progress >= 100 ? 'bg-emerald-500' : 'bg-corp-accent'}`}
                                                        style={{ width: `${visualProgress}%` }}
                                                    ></div>
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-500">{progress}%</span>
                                            </div>
                                        </td>
                                        <td className="px-6 py-4">
                                            <span className={`px-3 py-1 rounded-lg text-[10px] font-bold uppercase tracking-tight border ${getStatusColor(order.status)}`}>
                                                {order.status === 'In Progress' ? 'Receiving' :
                                                    order.status === 'Completed' ? 'Closed' :
                                                        order.status === 'Pending' ? 'Pending' : order.status}
                                            </span>
                                        </td>
                                        <td className="px-6 py-4 text-right">
                                            <div className="flex justify-end gap-2">
                                                {order.status !== 'Completed' && (
                                                    <button
                                                        onClick={() => handleCloseOrderRequest(order)}
                                                        disabled={isClosing}
                                                        className="p-2 text-slate-400 hover:text-emerald-400 hover:bg-emerald-900/40 rounded-xl transition-all disabled:opacity-50 border border-transparent hover:border-emerald-800/50"
                                                        title="Finalize Receipt"
                                                    >
                                                        {isClosing ? <Loader2 className="w-5 h-5 animate-spin" /> : <Lock className="w-5 h-5" />}
                                                    </button>
                                                )}
                                                <button
                                                    onClick={() => {
                                                        const searchParams = new URLSearchParams(window.location.search);
                                                        const mode = searchParams.get('mode') || 'standard';
                                                        navigate(`/inbound/receive/${mode}/${order.id}`);
                                                    }}
                                                    className="p-2 text-slate-400 hover:text-white hover:bg-corp-accent/40 rounded-xl transition-all border border-transparent hover:border-corp-secondary/50"
                                                    title="Continue Receipt"
                                                >
                                                    <ChevronRight className="w-5 h-5" />
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                );
                            }) : (
                                <tr>
                                    <td colSpan={6} className="px-6 py-20 text-center">
                                        <div className="flex flex-col items-center gap-3">
                                            <div className="p-4 bg-slate-50 rounded-2xl">
                                                <AlertCircle className="w-8 h-8 text-slate-300" />
                                            </div>
                                            <p className="text-slate-500 font-medium">No orders found matching criteria</p>
                                        </div>
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </table>
                </div>

                {/* Pagination */}
                <div className="px-6 py-4 bg-corp-base/30 border-t border-corp-secondary/50 flex items-center justify-between">
                    <p className="text-xs font-medium text-slate-400">
                        Showing <span className="text-white">{Math.min(filteredOrders.length, itemsPerPage)}</span> of <span className="text-white">{filteredOrders.length}</span> results
                    </p>
                    <div className="flex items-center gap-2">
                        <button
                            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
                            disabled={currentPage === 1}
                            className="p-1.5 rounded-lg border border-corp-secondary text-slate-400 disabled:opacity-30 hover:bg-corp-accent/40 hover:text-white hover:shadow-sm transition-all"
                        >
                            <ChevronLeft className="w-4 h-4" />
                        </button>
                        <span className="text-xs font-bold text-slate-300">
                            Page {currentPage} of {totalPages || 1}
                        </span>
                        <button
                            onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
                            disabled={currentPage === totalPages || totalPages === 0}
                            className="p-1.5 rounded-lg border border-corp-secondary text-slate-400 disabled:opacity-30 hover:bg-corp-accent/40 hover:text-white hover:shadow-sm transition-all"
                        >
                            <ChevronRight className="w-4 h-4" />
                        </button>
                    </div>
                </div>
            </div>
            {/* Confirmation Modal */}
            {confirmingOrder && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-300">
                    <div className="bg-corp-nav border border-corp-secondary w-full max-w-md rounded-[2.5rem] shadow-2xl overflow-hidden animate-in zoom-in-95 duration-300">
                        <div className="p-8 space-y-6">
                            <div className="flex items-center gap-4">
                                <div className="p-4 bg-emerald-900/20 rounded-2xl border border-emerald-500/30">
                                    <Lock className="w-6 h-6 text-emerald-400" />
                                </div>
                                <div>
                                    <h3 className="text-xl font-bold text-white">Close Order</h3>
                                    <p className="text-xs text-slate-500 font-bold uppercase tracking-widest">{confirmingOrder.orderNumber}</p>
                                </div>
                            </div>

                            <div className="space-y-4">
                                {(() => {
                                    const received = confirmingOrder.lines.reduce((acc: number, l: any) => acc + l.receivedQty, 0);
                                    const total = confirmingOrder.totalUnits;
                                    const isComplete = received >= total;
                                    const diff = total - received;

                                    return (
                                        <>
                                            <div className="bg-corp-base/50 p-6 rounded-3xl border border-corp-secondary/30 space-y-4">
                                                <div className="flex justify-between items-center text-xs font-bold uppercase tracking-widest">
                                                    <span className="text-slate-500">Received</span>
                                                    <span className={isComplete ? 'text-emerald-400' : 'text-amber-400'}>{received} / {total}</span>
                                                </div>
                                                <div className="w-full h-2 bg-slate-800 rounded-full overflow-hidden border border-slate-700">
                                                    <div className={`h-full transition-all duration-1000 ${isComplete ? 'bg-emerald-500' : 'bg-amber-500'}`} style={{ width: `${Math.min(100, (received / total) * 100)}%` }} />
                                                </div>
                                            </div>

                                            {isComplete ? (
                                                <p className="text-sm text-slate-300 leading-relaxed text-center px-4">
                                                    The order is <strong>complete</strong>. Continuing will sync with Odoo and mark it as finished.
                                                </p>
                                            ) : (
                                                <div className="bg-amber-900/20 border border-amber-500/20 p-5 rounded-2xl flex gap-4">
                                                    <AlertCircle className="w-6 h-6 text-amber-500 shrink-0" />
                                                    <div className="space-y-1">
                                                        <p className="text-sm font-bold text-amber-400 uppercase tracking-tight">Partial Completion Detected</p>
                                                        <p className="text-[11px] text-amber-200/60 leading-relaxed font-medium">
                                                            <strong>{diff}</strong> units remaining. Odoo will automatically generate a <strong>Backorder</strong> for pending products.
                                                        </p>
                                                    </div>
                                                </div>
                                            )}
                                        </>
                                    );
                                })()}
                            </div>

                            <div className="grid grid-cols-2 gap-3 pt-2">
                                <button
                                    onClick={() => setConfirmingOrder(null)}
                                    className="px-6 py-4 rounded-2xl font-black text-[10px] uppercase tracking-[0.2em] bg-corp-base/60 text-slate-500 border border-corp-secondary/50 hover:bg-slate-800 transition-all active:scale-95"
                                >
                                    Cancel
                                </button>
                                <button
                                    onClick={handleFinalizeClose}
                                    className="px-6 py-4 rounded-2xl font-black text-[10px] uppercase tracking-[0.2em] bg-emerald-600 text-white shadow-xl shadow-emerald-900/20 hover:bg-emerald-500 transition-all active:scale-95 border border-emerald-400/20"
                                >
                                    Confirm Close
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default InboundDashboard;
