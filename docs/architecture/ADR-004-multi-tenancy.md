# ADR-004: Aislamiento Multi-Tenant (TenantGuard)

## Estado
Aceptado

## Contexto
Victoria WMS es una plataforma SaaS/3PL donde varios clientes independientes (Tenants) operan en el mismo sistema físico sin riesgo de fuga de datos o colisión de IDs.

## Decisión
Implementar una estrategia de **Aislamiento Lógico mediante TenantId** forzado en todas las capas del sistema (Routing, Domain, Persistence).

## Racional
1. **TenantGuard**: Cada comando y evento requiere un `TenantId` obligatorio.
2. **Global Query Filters**: Marten/PostgreSQL utiliza filtros automáticos por `TenantId` para evitar que un inquilino vea los datos de otro.
3. **Composite Keys**: El mapeo de IDs externos (como Odoo IDs) siempre es compuesto: `ExternalId + TenantId`.

## Consecuencias
- **Seguridad**: Máximo aislamiento sin la sobrecarga de mantener bases de datos físicas separadas por cliente.
- **Desarrollo**: Requiere que cada API endpoint incluya validación de pertenencia.
- **Escalabilidad**: Facilita el sharding futuro si un Tenant crece masivamente.
