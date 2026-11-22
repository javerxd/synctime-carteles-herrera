# Llamadas a la API por pantalla y por formulario — fase 15B (RNFP23)

Medido con Playwright (Chrome, servidor de desarrollo con StrictMode, API simulada) el 2026-11-22.
Una llamada «duplicada» es una petición idéntica (mismo método, dirección, consulta y cuerpo) que sale más de una vez sin una
escritura entre medio; las canceladas por el navegador (StrictMode en desarrollo) no cuentan.

| Pantalla o formulario | Llamadas | Lecturas | Escrituras | Duplicadas |
|---|---:|---:|---:|---|
| Abrir / | 4 | 4 | 0 | — |
| Abrir /ingresar | 1 | 1 | 0 | — |
| Abrir /ingresar/cliente | 1 | 1 | 0 | — |
| Abrir /consultas | 2 | 2 | 0 | — |
| Abrir /personal/ingresar | 1 | 1 | 0 | — |
| Abrir /cuenta/registro | 2 | 2 | 0 | — |
| Abrir /cuenta/recuperar | 1 | 1 | 0 | — |
| Abrir /cuenta | 3 | 2 | 1 | — |
| Abrir /cuenta/solicitud | 4 | 3 | 1 | — |
| Abrir /cuenta/solicitudes | 2 | 1 | 1 | — |
| Abrir /cuenta/solicitudes/123 | 4 | 3 | 1 | — |
| Abrir /panel | 9 | 8 | 1 | — |
| Abrir /panel/pedidos | 2 | 1 | 1 | — |
| Abrir /panel/pedidos/1 | 2 | 1 | 1 | — |
| Abrir /panel/solicitudes | 2 | 1 | 1 | — |
| Abrir /panel/solicitudes/123 | 4 | 3 | 1 | — |
| Abrir /panel/clientes | 2 | 1 | 1 | — |
| Abrir /panel/clientes/9 | 2 | 1 | 1 | — |
| Abrir /panel/cotizaciones | 2 | 1 | 1 | — |
| Abrir /panel/cotizaciones/45 | 2 | 1 | 1 | — |
| Abrir /panel/calendario | 2 | 1 | 1 | — |
| Abrir /panel/local | 5 | 4 | 1 | — |
| Abrir /panel/indicadores | 8 | 7 | 1 | — |
| Abrir /panel/reportes | 2 | 1 | 1 | — |
| Abrir /panel/perfil | 4 | 3 | 1 | — |
| Abrir /panel/empleados | 2 | 1 | 1 | — |
| Abrir /panel/mantenedores/categorias | 2 | 1 | 1 | — |
| Abrir /panel/auditoria | 3 | 2 | 1 | — |
| Abrir /panel/correos-no-enviados | 2 | 1 | 1 | — |
| Enviar el ingreso del cliente | 4 | 3 | 1 | — |
| Enviar el registro | 5 | 4 | 1 | — |
| Enviar la solicitud en línea | 6 | 4 | 2 | — |
| Emitir la cotización | 5 | 3 | 2 | — |
| Registrar un abono | 4 | 2 | 2 | — |
