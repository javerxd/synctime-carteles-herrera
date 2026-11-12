# Informe de cierre — Fase 0: preparación del repositorio y documentos

Fecha: 12-11-2026 · Rama: `fase-00-preparacion` · Estado: **en revisión, esperando el visto bueno del usuario**

## Qué se hizo

1. **Inventario.** La carpeta local se comparó, archivo por archivo, con `main` de GitHub (`aa7514a`): los 136 archivos coinciden. Solo faltaba en local el `.gitignore` raíz. El backend está publicado como archivos normales, no como submódulo. No hizo falta ningún commit de "trabajo local".
2. **Respaldo del git anidado del backend.** `C:\Users\Admin\Desktop\synctime-respaldo\` contiene:
   - `backend-historial.bundle`: historial completo, verificado con `git bundle verify` (3 ramas, 4 commits).
   - `backend.git`: la carpeta `.git` original, movida, no borrada.
   - `LEEME.txt`: ramas e historial.
3. **Repositorio unificado.** `C:\Users\Admin\Desktop\synctime` es ahora la copia de trabajo de `https://github.com/javerxd/synctime-carteles-herrera`. La unificación no modificó ningún archivo de trabajo.
4. **Higiene.**
   - `.gitattributes` nuevo: el repositorio guarda finales de línea LF, los guiones `.sh` siempre en LF y los binarios se marcan como tales. No reescribió ningún archivo, porque todo el índice ya estaba en LF.
   - `.gitignore`: se agregaron `*.bak`, `*.trn` y `*.bacpac`, para que un respaldo de base de datos nunca entre al repositorio. Ya cubría `bin`, `obj`, `node_modules`, `dist`, `.env` y archivos de base de datos.
5. **Documentos en el repositorio.**
   - `docs/requisitos/requerimientos_del_sistema.md` y `docs/requisitos/reglas-de-diseno.md`: copias idénticas a los originales (comprobado por hash).
   - `docs/plan/plan-de-construccion.md`: el plan aprobado.
   - `docs/plan/desviaciones.md`: desviaciones, restricciones del equipo y pendientes externos.
   - `docs/plan/trazabilidad.md`: matriz de los 183 identificadores con su fase, estado y evidencia.
   - `docs/plan/tablero.md`: tablero Kanban de las 18 fases (RNFO09).

## Verificación

| Comprobación | Resultado |
|---|---|
| Contenido local frente a GitHub | 136 de 136 archivos idénticos |
| Respaldo del git anidado | `git bundle verify`: correcto, historial completo |
| `git status` tras unificar | Sin diferencias |
| Secretos en el historial de GitHub (12 commits) | Ninguno. Lo que marcó el patrón son expresiones de código, marcadores de ejemplo y valores de prueba. No hay claves privadas, tokens ni contraseñas en cadenas de conexión. El commit "documentar credenciales admin" (`7184f9f`) solo describe el hecho, sin escribir ninguna contraseña |
| Matriz de trazabilidad | 183 identificadores: 47 RFS, 34 RFU, 29 RNFP, 17 RNFO, 9 RNFE, 24 entradas y 23 salidas. Ninguno sin fase y ninguno de sobra |
| Pruebas del backend sobre `localhost\SQLEXPRESS` | 136 de 136 en 1 min 4 s. No quedaron bases temporales |
| Pruebas del frontend | 79 de 79 |
| Compilación del frontend | Correcta. JavaScript inicial: 376 KB (115,8 KB comprimido). CSS: 30,7 KB |
| Linter del frontend | 3 avisos, todos en archivos que se reescriben en la fase 3 |

## Herramientas del equipo

| Herramienta | Versión | Observación |
|---|---|---|
| SDK de .NET | 11.0.100-rc.3 | **Es el único SDK instalado y es una versión preliminar.** Ver hallazgos |
| Runtime de ASP.NET Core | 10.0.14 y 11.0.0-rc.3 | Los proyectos apuntan a `net10.0` |
| Node.js / npm | 24.15.0 / 11.12.1 | LTS vigente |
| SQL Server | 2025 Express (17.0.1000.7) | Instancia `localhost\SQLEXPRESS`, autenticación de Windows |
| Playwright | 1.64.1 | Solo Chromium instalado |
| Git | 2.56.1 | Sin GitHub CLI |
| Docker | No disponible | Por decisión del usuario: se verifica en la nube en la fase 16 |

## Hallazgos

1. **SDK de .NET.** No está instalado el SDK 10 que piden los requisitos. Se compila con el SDK 11 preliminar apuntando a `net10.0`. Funciona, pero las imágenes de Docker y el pipeline de la fase 16 usarán el SDK 10, y una diferencia entre ambos podría aparecer recién ahí. Recomendación: instalar el SDK 10 y fijarlo con un `global.json` al comenzar la fase 1.
2. **Vulnerabilidad en una dependencia.** `npm audit` informa una de severidad alta en `follow-redirects`, que llega a través de `axios`. Se elimina en la fase 3, cuando `axios` se reemplace por `fetch`.
3. **Otras bases en la instancia.** Además de `LetreriaLosHerreraDB` existen `LetrerosHerreradb` (la base antigua, anterior a la migración) y `juegosdb` (ajena al proyecto). No se tocaron ni se tocarán.

## Lo que no se verificó

- Playwright de extremo a extremo: la única prueba existente simula el contrato antiguo y se elimina en la fase 3.
- La conversión de finales de línea en otro equipo o en Linux. Se comprobará en la fase 16, en el Codespace.

## Para la fase 1 se necesita del usuario

1. Decidir si instala el SDK de .NET 10.
2. Definir las contraseñas de los usuarios de la base demo. Se cargarán en User Secrets con un comando que no las muestra.
3. Confirmar la matriz de permisos por rol, que se presenta durante la fase.
