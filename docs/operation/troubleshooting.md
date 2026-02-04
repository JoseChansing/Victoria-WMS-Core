# Guía de Operación & Troubleshooting

Esta guía contiene los procedimientos para resolver las anomalías operativas más comunes en Victoria WMS.

## 1. Gestión de SyncErrors (Odoo)
**Síntoma**: La orden en Victoria aparece con etiqueta roja `SyncError`.
- **Causa**: Fallo en la comunicación XML-RPC o regla contable de Odoo incumplida.
- **Solución**:
    1. Revisar los logs de `OdooFeedbackConsumer`.
    2. Verificar si el periodo contable en Odoo está abierto.
    3. Una vez resuelto en Odoo, pulsar "Retry Sync" en la Torre de Control (o re-publicar el evento `DispatchConfirmed`).

## 2. LPNs en Quarantine
**Síntoma**: Un LPN no aparece disponible para picking.
- **Causa**: Recibido con excedente o discrepancia RFID detectada.
- **Solución**:
    1. Ir a **Torre de Control > Quarantines**.
    2. Revisar la razón del bloqueo (ej: `OVERAGE_PENDING_APPROVAL`).
    3. El Supervisor debe aprobar o rechazar (Authorize Adjustment) para liberar el stock.

## 3. RfidDebouncer Saturado
**Síntoma**: El sistema tarda en procesar ráfagas masivas de etiquetas.
- **Causa**: Burst de más de 5,000 tags por segundo o memoria insuficiente en Redis.
- **Solución**:
    1. Verificar el TTL de las llaves en Redis.
    2. Aumentar el `WindowSeconds` en la configuración si las lecturas repetidas son persistentes.

## 4. Error de Reserva (Shortage)
**Síntoma**: `CycleCountTask` creado automáticamente tras un picking fallido.
- **Acción**: El sistema dispara esta tarea cuando el stock lógico decía haber 10 pero el físico encontró 0.
- **Solución**: Debe ejecutarse el conteo cíclico de inmediato para ajustar la desviación del mundo real.
