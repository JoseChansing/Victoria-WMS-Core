import axios from 'axios';

const api = axios.create({
    baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1',
    headers: {
        'Content-Type': 'application/json',
    },
});

// Interceptor para inyectar JWT y TenantId
api.interceptors.request.use((config) => {
    const token = localStorage.getItem('vicky_token');
    localStorage.getItem('vicky_tenant');

    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }

    // Si tenemos un tenant seleccionado, lo inyectamos en el payload si es un POST/PUT
    // O como un header custom si el backend lo requiere. Según nuestra Fase 9,
    // el backend espera el TenantId en el body de los requests.

    return config;
}, (error) => {
    return Promise.reject(error);
});

// Interceptor para manejo de errores globales
api.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            localStorage.removeItem('vicky_token');
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

export default api;
