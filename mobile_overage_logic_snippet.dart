// Snippet de Lógica Dart para Pantalla de Picking (Victoria Mobile)
// Este código detecta el exceso y dispara la solicitud de ajuste.

void _onQuantityEntered(int inputQty, int orderedQty) {
  if (inputQty > orderedQty) {
    // ⚠️ ALERTA DE EXCESO DE DETECTADA
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text("⚠️ Exceso Detectado"),
        content: Text("La cantidad ($inputQty) excede lo solicitado ($orderedQty). ¿Deseas solicitar una aprobación de excedente al Supervisor?"),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: Text("CANCELAR"),
          ),
          ElevatedButton(
            onPressed: () {
              // Disparar comando de solicitud (RequestPickingOverage)
              _authBloc.add(RequestOverageEvent(
                orderId: _currentOrder,
                lineId: _currentLine,
                qty: inputQty
              ));
              Navigator.pop(ctx);
              
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(content: Text("Solicitud enviada a Torre de Control. Esperando aprobación..."))
              );
            },
            child: Text("SOLICITAR AJUSTE"),
          ),
        ],
      ),
    );
  } else {
    // Proceso normal de grabación
    _confirmPick(inputQty);
  }
}
