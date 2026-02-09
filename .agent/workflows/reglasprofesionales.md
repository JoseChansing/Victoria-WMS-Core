---
description: 🛡️ El "Pack de Reglas Profesionales" (Protocolo de Estabilidad)
---

--------------------------------------------------------------------------------
🛡️ El "Pack de Reglas Profesionales" (Protocolo de Estabilidad)
Para detener la improvisación, de ahora en adelante, cada instrucción debe adherirse a este protocolo estricto. No más "intenta esto".
REGLA #1: La Ley de Propiedad (Ownership Law)
• Axioma: Docker siempre creará archivos como root en un build.
• Mandato: Ningún despliegue se considera terminado hasta ejecutar explícitamente: docker exec -u 0 nginx-proxy chown -R nginx:nginx /usr/share/nginx/html
REGLA #2: Verificación de Capa 7 (Application Layer Check)
• Axioma: docker ps (Estado del contenedor) NO es prueba de éxito.
• Mandato: El éxito se mide solo si curl -I https://app.victoriawms.dev devuelve HTTP 200. Si devuelve 403 o 500, el despliegue es FALLIDO y requiere rollback o fix inmediato.
REGLA #3: Inmutabilidad de Configuración
• Axioma: Los archivos de configuración (nginx.conf) no deben editarse "en vivo".
• Mandato: Usar volúmenes persistentes definidos en docker-compose y solo reiniciar el servicio para aplicar cambios.
REGLA #4: El Test de Humo de la API (Smoke Test)
• Axioma: Que el contenedor api-perfect esté "Running" no significa que funcione. Puede estar reiniciándose en bucle o lanzando excepciones al inicio.
• Mandato: El despliegue FALLA si el siguiente comando no devuelve HTTP 200: curl -s -o /dev/null -w "%{http_code}" http://localhost:8081/api/v1/inbound/kpis (o el puerto interno correspondiente).
    ◦ Si devuelve 500: PROHIBIDO reportar éxito. Se debe ejecutar docker logs inmediatamente.
REGLA #5: Validación de Inyección de Dependencias
• Axioma: El 90% de los errores 500 tras un cambio de código son por fallos en Program.cs (no se registró una clase nueva).
• Mandato: Si se añade un servicio nuevo (ej. LpnFactory, ScanClassifier), se debe verificar explícitamente su registro en el contenedor DI antes de compilar.

--------------------------------------------------------------------------------