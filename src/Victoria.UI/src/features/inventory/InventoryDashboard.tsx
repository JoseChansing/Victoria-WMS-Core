import React from 'react';
import {
    Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
    Paper, Typography, Box, Chip, Button, Alert, LinearProgress
} from '@mui/material';
import { useInventory } from '../../hooks/useInventory';
import { useAuth } from '../../context/AuthContext';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';

export const InventoryDashboard: React.FC = () => {
    const { tenant, user } = useAuth();
    const { inventory, isLoading, approveAdjustment } = useInventory(tenant);

    if (isLoading) return <LinearProgress />;

    const handleApprove = async (lpnId: string, qty: number) => {
        if (window.confirm(`¿Autorizar ajuste a ${qty} unidades para el LPN ${lpnId}?`)) {
            await approveAdjustment.mutateAsync({
                lpnId,
                newQuantity: qty,
                reason: "SUPERVISOR_APPROVAL_UI"
            });
        }
    };

    return (
        <Box sx={{ p: 3 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3 }}>
                <Typography variant="h4" sx={{ fontWeight: 'bold' }}>
                    Torre de Control de Inventario
                </Typography>
                <Chip label={`Compañía: ${tenant}`} color="primary" variant="outlined" />
            </Box>

            {inventory.length === 0 && !isLoading && (
                <Alert severity="info" sx={{ mb: 2 }}>
                    No hay discrepancias pendientes o inventario cargado para este tenant.
                </Alert>
            )}

            <TableContainer component={Paper} elevation={4}>
                <Table>
                    <TableHead sx={{ bgcolor: '#f5f5f5' }}>
                        <TableRow>
                            <TableCell>LPN ID</TableCell>
                            <TableCell>SKU</TableCell>
                            <TableCell>Ubicación</TableCell>
                            <TableCell align="right">Cantidad</TableCell>
                            <TableCell>Estado</TableCell>
                            <TableCell align="center">Acciones (Supervisor)</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {inventory.map((item) => (
                            <TableRow
                                key={item.id}
                                sx={{
                                    bgcolor: item.status === 'Quarantine' ? '#fff4f4' : 'inherit',
                                    '&:hover': { bgcolor: '#f9f9f9' }
                                }}
                            >
                                <TableCell sx={{ fontWeight: 'bold' }}>{item.id}</TableCell>
                                <TableCell>{item.sku}</TableCell>
                                <TableCell>{item.location}</TableCell>
                                <TableCell align="right">{item.quantity}</TableCell>
                                <TableCell>
                                    <Chip
                                        label={item.status}
                                        color={item.status === 'Quarantine' ? 'error' : item.status === 'Putaway' ? 'success' : 'default'}
                                        size="small"
                                        icon={item.status === 'Quarantine' ? <WarningAmberIcon /> : undefined}
                                    />
                                </TableCell>
                                <TableCell align="center">
                                    {item.status === 'Quarantine' && user?.role === 'Supervisor' && (
                                        <Button
                                            variant="contained"
                                            color="error"
                                            size="small"
                                            onClick={() => handleApprove(item.id, item.quantity)}
                                        >
                                            Aprobar Ajuste
                                        </Button>
                                    )}
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>
        </Box>
    );
};
