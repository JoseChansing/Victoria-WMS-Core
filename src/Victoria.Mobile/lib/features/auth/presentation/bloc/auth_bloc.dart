import 'package:flutter_bloc/flutter_bloc.dart';
import '../../domain/repositories/auth_repository.dart';

// Events
abstract class AuthEvent {}
class LoginRequested extends AuthEvent {
  final String user;
  final String password;
  final String tenant;
  LoginRequested(this.user, this.password, this.tenant);
}
class LogoutRequested extends AuthEvent {}
class AppStarted extends AuthEvent {}

// States
abstract class AuthState {}
class AuthInitial extends AuthState {}
class AuthLoading extends AuthState {}
class Authenticated extends AuthState {
  final String tenant;
  Authenticated(this.tenant);
}
class Unauthenticated extends AuthState {}
class AuthError extends AuthState {
  final String message;
  AuthError(this.message);
}

// Bloc
class AuthBloc extends Bloc<AuthEvent, AuthState> {
  final AuthRepository _repository;

  AuthBloc(this._repository) : super(AuthInitial()) {
    on<AppStarted>((event, emit) async {
      if (await _repository.isAuthenticated()) {
        final tenant = await _repository.getSelectedTenant();
        emit(Authenticated(tenant ?? 'UNKNOWN'));
      } else {
        emit(Unauthenticated());
      }
    });

    on<LoginRequested>((event, emit) async {
      emit(AuthLoading());
      final success = await _repository.login(event.user, event.password, event.tenant);
      if (success) {
        emit(Authenticated(event.tenant));
      } else {
        emit(AuthError("Credenciales o compañía inválida"));
      }
    });

    on<LogoutRequested>((event, emit) async {
      await _repository.logout();
      emit(Unauthenticated());
    });
  }
}
