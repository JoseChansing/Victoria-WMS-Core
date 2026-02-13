import React, { useState, useEffect, useRef } from 'react';
import { Bell, Check, AlertCircle, Info, X } from 'lucide-react';

interface SystemNotification {
    id: string;
    title: string;
    message: string;
    severity: 'Critical' | 'Warning' | 'Info';
    isRead: boolean;
    createdAt: string;
    referenceId?: string;
}

const NotificationBell: React.FC = () => {
    const [notifications, setNotifications] = useState<SystemNotification[]>([]);
    const [isOpen, setIsOpen] = useState(false);
    const dropdownRef = useRef<HTMLDivElement>(null);

    const fetchNotifications = async () => {
        try {
            const response = await fetch('/api/v1/notifications/unread');
            if (response.ok) {
                const data = await response.json();
                setNotifications(data);
            }
        } catch (error) {
            console.error('Error fetching notifications:', error);
        }
    };

    useEffect(() => {
        fetchNotifications();
        const interval = setInterval(fetchNotifications, 30000); // Poll every 30s
        return () => clearInterval(interval);
    }, []);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
                setIsOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const markAsRead = async (id: string) => {
        try {
            const response = await fetch(`/api/v1/notifications/${id}/read`, { method: 'POST' });
            if (response.ok) {
                setNotifications(prev => prev.filter(n => n.id !== id));
            }
        } catch (error) {
            console.error('Error marking notification as read:', error);
        }
    };

    return (
        <div className="relative" ref={dropdownRef}>
            <button
                onClick={() => setIsOpen(!isOpen)}
                className="p-2 text-slate-400 hover:text-white transition-colors relative"
            >
                <Bell className="w-5 h-5" />
                {notifications.length > 0 && (
                    <span className="absolute top-1.5 right-1.5 w-4 h-4 bg-rose-500 text-[10px] text-white font-bold flex items-center justify-center rounded-full border-2 border-corp-nav">
                        {notifications.length}
                    </span>
                )}
            </button>

            {isOpen && (
                <div className="absolute right-0 mt-3 w-80 bg-corp-nav border border-corp-secondary rounded-2xl shadow-2xl overflow-hidden z-50">
                    <div className="p-4 border-b border-corp-secondary flex items-center justify-between">
                        <h3 className="font-bold text-sm">Notifications</h3>
                        <button onClick={() => setIsOpen(false)} className="text-slate-500 hover:text-white">
                            <X className="w-4 h-4" />
                        </button>
                    </div>

                    <div className="max-h-96 overflow-y-auto">
                        {notifications.length === 0 ? (
                            <div className="p-8 text-center text-slate-500">
                                <Bell className="w-8 h-8 mx-auto mb-2 opacity-20" />
                                <p className="text-xs">No unread notifications</p>
                            </div>
                        ) : (
                            notifications.map(n => (
                                <div key={n.id} className={`p-4 border-b border-corp-secondary/50 hover:bg-white/5 transition-colors group relative`}>
                                    <div className="flex items-start space-x-3">
                                        <div className={`mt-0.5 ${n.severity === 'Critical' ? 'text-rose-500' : 'text-blue-500'}`}>
                                            {n.severity === 'Critical' ? <AlertCircle className="w-4 h-4" /> : <Info className="w-4 h-4" />}
                                        </div>
                                        <div className="flex-1 min-w-0">
                                            <p className="text-xs font-bold text-white truncate">{n.title}</p>
                                            <p className="text-[11px] text-slate-400 leading-relaxed mt-0.5">{n.message}</p>
                                            <p className="text-[9px] text-slate-500 mt-2 uppercase font-bold tracking-wider">
                                                {new Date(n.createdAt).toLocaleTimeString()}
                                            </p>
                                        </div>
                                        <button
                                            onClick={() => markAsRead(n.id)}
                                            className="p-1.5 rounded-lg bg-corp-secondary text-slate-400 hover:text-emerald-400 opacity-0 group-hover:opacity-100 transition-all"
                                            title="Mark as read"
                                        >
                                            <Check className="w-3.5 h-3.5" />
                                        </button>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
            )}
        </div>
    );
};

export default NotificationBell;
