# Procedimiento "Big Bang": Sincronización Inicial con Odoo

Cuando se despliega el sistema por primera vez, es necesario traer todo el catálogo histórico de Odoo.

## 1. Reset de Estado de Sincronización
Por defecto, el polller solo trae cambios desde la `LastSyncDate`. Para forzar una carga completa, ejecuta el siguiente SQL en RDS:

```sql
-- Eliminar el estado previo para forzar reinicio completo
DELETE FROM mt_doc_integrationstate WHERE id ILIKE '%ODOO%';
```

O actualiza la fecha a una muy antigua:
```sql
UPDATE mt_doc_integrationstate 
SET data = jsonb_set(data, '{LastSyncDate}', '"2000-01-01T00:00:00Z"')
WHERE id ILIKE '%ODOO%';
```

## 2. Configuración en appsettings.json
Asegúrate de que el intervalo de polling sea corto durante la carga inicial para procesar ráfagas:

```json
"Odoo": {
  "PollingIntervalSeconds": 10
}
```

## 3. Logs de Monitoreo
Monitorea la consola de la instancia EC2 para verificar el progreso:
```bash
docker logs -f victoria-api
```
Deberías ver ráfagas de logs tipo:
`[ACL] Syncing Product X ...`
`[ACL] Syncing Product Y ...`

## 4. Verificación
Una vez terminado el proceso (los logs de sync se detienen), verifica el conteo de SKUs:
```sql
SELECT count(*) FROM mt_doc_sku;
```
