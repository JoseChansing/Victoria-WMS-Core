import React, { useState } from 'react';
import { Container, Paper, Typography, TextField, Button, Box, MenuItem } from '@mui/material';
import { useAuth } from '../../context/AuthContext';
import { useNavigate } from 'react-router-dom';

const TENANTS = ['PERFECTPTY', 'NATSUKI', 'PDM', 'FILTROS'];

export const LoginPage: React.FC = () => {
    const [selectedTenant, setSelectedTenant] = useState('PERFECTPTY');
    const { login } = useAuth();
    const navigate = useNavigate();

    const handleLogin = (e: React.FormEvent) => {
        e.preventDefault();
        // Simulación de Login exitoso y obtención de JWT
        const mockToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.s2v_";
        login(mockToken, selectedTenant);
        navigate('/');
    };

    return (
        <Container maxWidth="xs">
            <Box sx={{ mt: 8, display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                <Paper elevation={3} sx={{ p: 4, width: '100%' }}>
                    <Typography variant="h4" align="center" gutterBottom sx={{ fontWeight: 'bold', color: '#1976d2' }}>
                        Victoria WMS
                    </Typography>
                    <Typography variant="body2" align="center" color="textSecondary" gutterBottom>
                        Seleccione su Entorno de Trabajo
                    </Typography>

                    <form onSubmit={handleLogin}>
                        <TextField
                            select
                            fullWidth
                            label="Compañía (Tenant)"
                            value={selectedTenant}
                            onChange={(e) => setSelectedTenant(e.target.value)}
                            margin="normal"
                        >
                            {TENANTS.map((t) => (
                                <MenuItem key={t} value={t}>{t}</MenuItem>
                            ))}
                        </TextField>
                        <TextField
                            fullWidth
                            label="Usuario"
                            defaultValue="admin_supervisor"
                            margin="normal"
                            disabled
                        />
                        <TextField
                            fullWidth
                            label="Password"
                            type="password"
                            defaultValue="********"
                            margin="normal"
                            disabled
                        />

                        <Button
                            type="submit"
                            fullWidth
                            variant="contained"
                            size="large"
                            sx={{ mt: 3, mb: 2 }}
                        >
                            Entrar a la Torre de Control
                        </Button>
                    </form>
                </Paper>
            </Box>
        </Container>
    );
};
