---
description: 
---

ATENCIÓN, AGENTE: Estás bajo supervisión estricta de arquitectura. El incumplimiento de cualquiera de las siguientes reglas resultará en el rechazo inmediato del código.
1. ARQUITECTURA Y LENGUAJE (C# .NET 8)
• Encapsulamiento Total: Se prohíbe terminantemente el Modelo de Dominio Anémico. Las clases del dominio NO pueden tener propiedades con public set. El estado solo muta a través de métodos de negocio que generan eventos (ej: Receive(), Pack()).
• Modularidad Estricta: El código debe respetar la estructura de Monolito Modular. El módulo Victoria.Inventory no debe tener referencias directas a Victoria.Orders. La comunicación es por contratos o eventos.
• Tipado Fuerte: Usa sealed class para Agregados y record para Eventos y Value Objects. El uso de herencia de clases para reutilizar código está desaconsejado; favorece la composición,.
2. OBSESIÓN POR LOS PRIMITIVOS (PROHIBIDA)
• Value Objects Obligatorios: Está prohibido usar string, int o Guid "desnudos" para identificadores o conceptos de negocio.
    ◦ ❌ Mal: public void Receive(string lpn, string sku)
    ◦ ✅ Bien: public void Receive(LpnCode lpn, SkuCode sku)
• Debes implementar los Value Objects (LpnCode, Sku, UserId) con validación en el constructor. Si el formato es inválido, el objeto no se instancia.
3. EVENT SOURCING & AUDITORÍA (INNEGOCIABLE)
• Inmutabilidad: Los eventos son hechos históricos. Deben implementarse como public sealed record.
• Metadatos Obligatorios: Todo evento debe implementar una interfaz IDomainEvent que garantice la trazabilidad exigida:
    ◦ OccurredOn (UTC Timestamp)
    ◦ CreatedBy (User ID)
    ◦ StationId (PDT ID / Device ID)
    ◦ Reason (Motivo de la operación),.
• Doble Nivel: Distingue claramente entre eventos físicos (LpnCreated) y eventos de flujo de negocio (ReceiptCompleted),.
4. CONCURRENCIA Y CONSISTENCIA
• Redis como Semáforo: Antes de cargar cualquier Agregado del repositorio, debes simular la adquisición de un Lock distribuido en Redis (LOCK:LPN:{id}). Redis NO contiene lógica de negocio, solo gestiona el acceso.
• PostgreSQL es la Verdad: La única fuente de verdad es el Event Stream en PostgreSQL. No uses colecciones en memoria ni variables estáticas para simular persistencia,.
• Cero Negativos: El Agregado LPN debe lanzar una excepción de dominio (DomainException) si una operación resultara en stock negativo o en una transición de estado inválida (ej. de Receipt a Dispatched sin pasar por Putaway),.
5. LIMPIEZA DE CÓDIGO
• No uses carpetas genéricas como "Helpers", "Utils" o "Common". Cada componente debe tener un nombre semántico claro.
• El código debe compilar "mentalmente": usa las características modernas de C# 12/NET 8 (Primary Constructors, Global Usings, File-scoped namespaces).