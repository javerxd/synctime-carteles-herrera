# Desviaciones y pendientes externos

Registro de todo requisito que **no** se cumple al pie de la letra, o que no se puede verificar en el equipo de desarrollo. Se actualiza al cerrar cada fase. Fuente de los requisitos: `docs/requisitos/requerimientos_del_sistema.md`.

## Desviaciones decididas

| Requisito | Qué pide | Qué se hace | Quién lo decidió | Fecha |
|---|---|---|---|---|
| RFS25, RFU22 | Identificar al empleado solo con la cámara, comparando contra todos los rostros registrados | Se pide primero el email y se compara solo contra el rostro de esa persona | El usuario, por seguridad | 12-11-2026 |
| RFS24, RFS27, RFU21, RFU22, E17 | Ingreso facial para los empleados | Solo `dueño` y `administrador`. Diseñador y operario entran con contraseña | El usuario | 12-11-2026 |
| RNFO12 | Al menos 500 registros sintéticos y 12 meses de historia | Datos mínimos de prueba en una base aparte, `LetreriaLosHerreraDB_Demo` | El usuario | 12-11-2026 |
| E17 | Tres capturas de cámara | Cinco capturas; el servidor exige un mínimo de tres | Propuesta de Claude, aceptada al aprobar el plan | 12-11-2026 |
| RFS10 | — | No existe en el documento: la numeración salta de RFS09 a RFS11 | — | — |

## Restricciones del equipo de desarrollo

| Tema | Situación | Consecuencia |
|---|---|---|
| Docker | El equipo usa OptiOS 11 Lite, sin Hyper-V: Docker Desktop no se puede instalar | Las fases 1 a 15 se desarrollan y prueban de forma nativa. Los contenedores (RNFO14, RNFO15) se construyen en la fase 16 y se verifican en GitHub Codespaces y GitHub Actions |
| Base de datos | Instancia `localhost\SQLEXPRESS` (SQL Server 2025 Express) con autenticación de Windows. No se usa LocalDB | Las pruebas crean bases temporales en esa instancia |
| SDK de .NET | El único SDK instalado es `11.0.100-rc.3`, una versión preliminar. Los proyectos apuntan a `net10.0` y se ejecutan con el runtime 10.0.14 | Los requisitos piden el SDK 10. **Pendiente de decisión del usuario:** instalar el SDK 10 y fijarlo con `global.json`. Hallazgo de la fase 0 |

## Requisitos pendientes de verificación externa

Quedan construidos y documentados, pero su comprobación no depende del código ni de este equipo.

| Requisito | Qué falta comprobar | Responsable |
|---|---|---|
| RNFP04 | Disponibilidad mensual del 99 % | Depende del hosting que se contrate |
| RNFE03 | HTTPS con certificado TLS vigente | Depende del hosting y del dominio |
| RNFP09 | Respaldo diario guardado fuera del servidor principal | Depende del hosting |
| RNFO14, RNFO15 | `docker compose up` y pipeline de CI/CD en verde | Usuario, en GitHub Codespaces y GitHub Actions (fase 16) |
| RNFP02 | Que un cliente sin capacitación registre una solicitud en 5 minutos | Prueba con una persona real |
| RNFO13 | Prueba manual con teclado y lector de pantalla (NVDA) | Usuario, con el guion de la fase 15 |
| RNFP05 | Funcionamiento en Safari real | Prueba en un dispositivo Apple |
| RNFE01, RNFE06 | Revisión legal de la política de privacidad, los términos y el consentimiento facial | Asesor legal |
