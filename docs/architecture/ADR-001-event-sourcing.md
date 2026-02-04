# ADR-001: Uso de Event Sourcing & Marten

## Estado
Aceptado

## Contexto
Los sistemas WMS tradicionales (CRUD) suelen perder la trazabilidad de "quién movió qué y por qué" a menos que se implementen tablas de auditoría complejas. En Victoria WMS, la precisión del inventario y la trazabilidad de eventos RFID son críticas.

## Decisión
Hemos decidido implementar **Event Sourcing** utilizando **Marten** (PostgreSQL) como almacén de eventos.

## Racional
1. **Auditoría Nativa**: No hay necesidad de tablas `AuditLog`. El historial de un LPN es su fuente de verdad.
2. **Re-generación de Estado**: Capacidad de reproducir fallos operativos re-ejecutando eventos.
3. **RFID Scalability**: Los eventos de ráfaga RFID se almacenan de forma secuencial, ideal para procesamiento asíncrono.
4. **Performance**: PostgreSQL con Marten ofrece un rendimiento de escritura superior al CRUD relacional pesado para streams de eventos.

## Consecuencias
- **Complejidad**: Los desarrolladores deben pensar en transiciones de estado, no en actualizaciones de columnas.
- **Eventual Consistency**: Algunas proyecciones de lectura requieren procesamiento asíncrono.
- **Storage**: Requiere estrategias de `Snapshooting` para streams muy largos.
