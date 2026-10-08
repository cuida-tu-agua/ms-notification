# ms-notification (notification-service · :3006)

Centro de notificaciones de Cuida Tu Agua: bandeja por usuario y rol, preferencias por nivel de urgencia (HU-034), correo de alertas críticas (HU-027) y alerta de sensor desconectado (HU-032). Genera la alerta de válvula cerrada (HU-033) que dispara el correo.
.NET 10 · hexagonal · SQL Server (schema `notification`, lo crea **ms-notification-db**) · RabbitMQ · SMTP (Mailpit en desarrollo).

## Cómo funciona
```
device-service ──RabbitMQ──▶ notification.device-events ──▶ DeviceEventsHandler ──▶ NotificationDispatcher
   device.linked / unlinked                                                             │
   device.valve.reported            (válvula cerrada = CRÍTICA)                         ├─ app    → notifications (bandeja)
   device.reading.received          (prueba de vida)                                    └─ correo → email_outbox ──▶ EmailDeliveryWorker ──▶ SMTP
                                                                                              (dirección: ms-iam /internal/users/{id}/contact)
OfflineDeviceWorker (cada 60 s): dispositivo callado ≥ 10 min ──▶ alerta importante "Sensor desconectado" (una sola vez)
```

**Quién ve qué.** Una notificación es de UN usuario (`user_id`, contextual: "tu válvula se cerró") o de TODOS los de un rol (`role`, difusión). Cada persona ve las suyas más las de los roles de su token (`roles`: `USER`, `ADMIN`). La lectura es personal: que una la lea Juan no la marca leída para Ana. Pedir la de otra persona responde 404.

**Matriz de canales por nivel** (HU-034, valores por defecto; el usuario los cambia):

| Nivel | Se guarda como | App | Push | Correo | SMS/WhatsApp |
|---|---|---|---|---|---|
| Crítica | `CRITICAL` | sí (no se puede apagar) | sí | sí | sí |
| Importante | `WARNING` | sí | sí | no | no |
| Informativa | `INFO` | sí | no | no | no |

Hoy se entregan **app** y **correo**. Push y SMS/WhatsApp (HU-026, HU-028) se guardan como preferencia pero aún no tienen adaptador.

**Correo.** Se escribe en una cola (`email_outbox`) junto con la notificación, así que un SMTP caído no pierde la alerta: se reintenta a 1 min, 5 min, 15 min y 1 h; a la 5.ª falla queda `FAILED`. La dirección no se guarda: se pide a ms-iam al enviar. Un evento repetido no manda dos correos. Con `Smtp:Host` vacío el canal queda apagado.

**Sensor desconectado.** Solo para dispositivos que ya reportaron alguna vez (uno que nunca reportó se ve como "Nunca ha reportado", HU-013). Umbral `Devices:OfflineAfterMinutes` (10 por defecto). Una alerta hasta que el dispositivo reporte de nuevo.

## Endpoints (JWT de ms-iam)
| Método | Ruta | |
|---|---|---|
| GET | `/api/notifications?unreadOnly&before&limit` | mi bandeja, más reciente primero (30 por defecto, máx. 100) |
| GET | `/api/notifications/unread-count` | el número de la campana |
| POST | `/api/notifications/{id}/read` | marcar una como leída (idempotente) |
| POST | `/api/notifications/read-all` | marcar todas |
| GET | `/api/notification-preferences` | canales por nivel (INFO, WARNING, CRITICAL) |
| PUT | `/api/notification-preferences/{severity}` `{inApp,push,email,sms}` | 400 `preferences.critical_requires_in_app` si una crítica pierde la app |
| GET | `/health` | sin token |

Errores en RFC 9457: el código estable va en `title` (`notification.not_found`, `preferences.critical_requires_in_app`, `auth.invalid_token`).

## Puesta en marcha
Antes: `ms-notification-db` aplicada (`docker compose up`, hasta `v1.4-device-liveness`), Redis y RabbitMQ arriba.
```powershell
cd C:\Users\User\Desktop\proyect\services\notification\backend\ms-notification
dotnet user-secrets set "ConnectionStrings:Notification" "Server=localhost,1433;Database=sy-water-db;User Id=notification_app;Password=<...>;TrustServerCertificate=True" --project src/SyWater.Notifications.Api
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/SyWater.Notifications.Api
dotnet user-secrets set "Internal:ApiKey" "<la MISMA que en ms-devices>" --project src/SyWater.Notifications.Api
Copy-Item ..\..\..\iam\backend\iam-backend\keys\public.pem src\SyWater.Notifications.Api\keys\iam-public.pem -Force
dotnet test
dotnet run --project src/SyWater.Notifications.Api
```
En desarrollo el correo sale por Mailpit (`localhost:1025`, ver http://localhost:8025). El correo necesita que ms-iam exponga `GET /internal/users/{id}/contact` (`X-Internal-Key`, respuesta `{id,email,fullName}`); mientras no exista, los correos reintentan y terminan en `FAILED`.

## Configuración (publicación)
| Clave | Para qué | Ejemplo en producción |
|---|---|---|
| `Cors:AllowedOrigins` | orígenes del frontend web que pueden llamar a la API (sin esto el navegador bloquea) | `["https://app.cuidatuagua.com"]` |
| `App:PublicUrl` | base de los enlaces "Abrir en la app" de los correos | `https://app.cuidatuagua.com` |
| `Smtp:Host/Port/Security/Username/Password` | SMTP real (`Security`: `None`, `StartTls` 587, `Ssl` 465). La contraseña va en secretos, nunca en git | `smtp.proveedor.com`, `587`, `StartTls` |
| `Smtp:FromAddress` / `FromName` | remitente (debe estar autorizado en el proveedor) | `no-reply@cuidatuagua.com` |
| `Jwt:Issuer` + `keys/iam-public.pem` | los mismos que ms-iam | |
| `Redis:Configuration` | el mismo Redis de ms-iam (revocación de tokens) | |
| `Services:IamBaseUrl`, `Internal:ApiKey` | para pedir el correo del usuario a ms-iam | |
| `Devices:OfflineAfterMinutes` | umbral de "sensor desconectado" | `10` |
| `RabbitMq:*` | broker; con `Host` vacío no consume eventos | |

## Pruebas
`dotnet test`: dominio, casos de uso con fakes, EF contra SQLite en memoria y la API completa con `WebApplicationFactory`. SQLite no reproduce algunos detalles de SQL Server (índices únicos con NULL, el `DENY` del rol): se validan al levantar con la BD real.
