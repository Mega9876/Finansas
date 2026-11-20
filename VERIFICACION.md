# Verificación real del proyecto

Ejecutada el **20 de noviembre de 2026**, en el directorio actual, inicialmente vacío. No se sobrescribió trabajo previo.

## Entorno

- Ubuntu Linux x64; SDK del host **10.0.115**, runtime **10.0.15**.
- Docker Engine **29.9.3**, Compose **5.7.0**.
- Imágenes de compilación/ejecución .NET **10.0**, PostgreSQL **18.1**.
- Git inicializado en rama `main`, sin commits, remoto ni publicaciones.

## Resultados

| Comprobación | Resultado |
|---|---|
| Instalación de los seis paquetes del `.csproj` y herramienta EF | Correcta |
| `dotnet restore` / `dotnet restore --force` | Correcto |
| `dotnet build` y `dotnet build -c Release --no-restore` | 0 errores y 0 advertencias |
| `dotnet format FinanzasPersonales.slnx --verify-no-changes --no-restore` | Correcto |
| Generación EF de `20261120115427_InicialUsuario` | Correcta; tres archivos en Migrations |
| `dotnet ef migrations has-pending-model-changes --project FinanzasPersonales.Api` | Sin diferencias pendientes |
| Migración aplicada en PostgreSQL | Confirmada mediante `__EFMigrationsHistory` |
| Aplicación manual desde contenedor SDK | Correcta; base ya actualizada, ninguna migración pendiente |
| `docker compose -f constructor.yml config --quiet` | YAML/Compose válido |
| Build multietapa Docker con restore/build/publish | Correcto, 0 errores y 0 advertencias de compilación |
| API y PostgreSQL iniciados mediante Compose | API Up; PostgreSQL Up (healthy) |
| Usuario de ejecución de API | UID/GID 1654, no root |
| Scalar `/scalar` | HTTP 200; HTML servido |
| OpenAPI `/openapi/v1.json` | HTTP 200; exactamente GET, POST y DELETE de Usuarios |
| GET sin activos | HTTP 200 con `[]` |
| POST válido y normalización de mayúsculas | HTTP 201; fila persistida y correo en minúsculas |
| Campos públicos y fecha UTC | Correctos, sin contraseña/hash |
| Hash almacenado | BCrypt, coste 12, salts distintos para la misma contraseña |
| Datos inválidos, ausentes o nulos | HTTP 400 |
| Contraseñas de más de 72 bytes UTF-8, NUL o espacios | HTTP 400 |
| Correo duplicado, incluso de usuario inactivo | HTTP 409 |
| Seis POST concurrentes del mismo correo con distinta capitalización | Un 201 y cinco 409; ningún 500 |
| DELETE de id inexistente | HTTP 404 |
| DELETE de activo / repetición sobre inactivo | HTTP 204 / 204 |
| GET después de soft delete | Omite al desactivado |
| Consulta directa posterior al DELETE | Fila existente con `activo=false` |
| Logs API/PostgreSQL durante las pruebas | Sin la contraseña ni los hashes de prueba |
| Persistencia tras `down` y `up` sin `-v` | Filas completas idénticas, incluidos hash y estado activo/inactivo |
| Production con `Database__ApplyMigrations=true` y BD inaccesible | Inicia sin migrar; Scalar/OpenAPI devuelven 404 |
| Error de conexión consultando en Production | 500 ProblemDetails sin detalles internos |
| Paquetes vulnerables, incluidas dependencias transitivas | Ninguno informado por NuGet al ejecutar la comprobación |
| Exclusión de `.env`, bin y obj en Git | Confirmada; `.env.example` y migraciones permanecen visibles |

Las pruebas funcionales se ejecutaron con `python3 tests/pruebas_integracion.py --reiniciar-contenedores`; las de configuración con `python3 tests/pruebas_produccion.py`. Las consultas SQL de comprobación son solo SELECT. El esquema se generó íntegramente mediante EF Core.

## Incidencias y soluciones

1. **Rutas NuGet de obj procedentes del host al ejecutar EF en un SDK Docker:** la primera prueba manual produjo `NETSDK1064`. Se añadió `dotnet restore --force` antes de aplicar migraciones, se repitió el comando y terminó correctamente. También se restauraron de nuevo las dependencias del host y se recompiló con éxito.
2. **Formato de una expresión de captura:** la comprobación adicional de formato detectó una sangría; se corrigió y `dotnet format --verify-no-changes` terminó correctamente. Se reconstruyó la imagen final.
3. **Avisos informativos del SDK Docker:** durante la restauración de herramientas indicó una comprobación de workloads y sugirió dotnet-ef 11. No bloquearon la operación. Se conserva dotnet-ef 10.0.15 deliberadamente para EF Core 10.
4. **Listado offline de migraciones:** `--no-connect` muestra la migración, pero no determina si está aplicada. La aplicación real se confirmó por separado consultando PostgreSQL y ejecutando `database update` en su red Docker.

## Estado entregado y límites

- API accesible en **http://localhost:8080**, Scalar en **http://localhost:8080/scalar**.
- PostgreSQL sin puerto publicado en el host; volumen persistente conservado.
- `.env` local generado con contraseña aleatoria, permisos 0600, excluido de Git y Docker. Conservarlo para seguir usando la base inicializada.
- Las pruebas dejan registros ficticios activos e inactivos; no se borraron filas ni volúmenes.
- No se publicaron repositorio ni imagen. La imagen local es `finanzas-personales-api:local`.
- No se realizó una prueba visual con navegador: se verificaron el HTML de Scalar y el documento OpenAPI por HTTP.
- No se probó un despliegue público ni se implementó autenticación/autorización. Los endpoints siguen siendo exclusivamente locales y no deben exponerse sin protección.
