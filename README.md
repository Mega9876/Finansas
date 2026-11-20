# FinanzasPersonales

Primera versión del backend de finanzas personales: infraestructura y **únicamente Usuario**. API tradicional con controladores, PostgreSQL y EF Core Code First. Sin Clean Architecture, CQRS, login, JWT ni roles.

> **Seguridad:** los endpoints de administración de usuarios son exclusivamente para pruebas locales hasta implementar autenticación y autorización. **No desplegarlos públicamente sin protección.** Compose publica la API solo en `127.0.0.1` y no publica PostgreSQL. Development no es un entorno apropiado para producción.

## Tecnologías y paquetes

- .NET SDK 10 / ASP.NET Core 10, C#.
- Microsoft.EntityFrameworkCore y Microsoft.EntityFrameworkCore.Design **10.0.15**.
- Npgsql.EntityFrameworkCore.PostgreSQL **10.0.3**.
- Microsoft.AspNetCore.OpenApi **10.0.15**; Scalar.AspNetCore **3.1.1** (sin Swagger UI).
- BCrypt.Net-Next **4.2.0**: salt aleatorio y coste 12.
- PostgreSQL **18.1**, Docker multietapa, Docker Compose y Git.
- Herramienta local `dotnet-ef` **10.0.15**, versionada en `dotnet-tools.json`.

Las versiones están fijadas en el proyecto; revisar actualizaciones de seguridad antes de un despliegue real.

## Estructura

El directorio actual es la raíz del repositorio (no se crea otra carpeta envolvente).

```text
.
├── FinanzasPersonales.slnx
├── dotnet-tools.json
├── FinanzasPersonales.Api/
│   ├── Controllers/UsuariosController.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── AppDbContextFactory.cs
│   │   └── DevelopmentMigrations.cs
│   ├── DTOs/{CrearUsuarioDto,UsuarioRespuestaDto}.cs
│   ├── Models/Usuario.cs
│   ├── Services/{IPasswordHasher,BCryptPasswordHasher}.cs
│   ├── Migrations/
│   │   ├── 20261120115427_InicialUsuario.cs
│   │   ├── 20261120115427_InicialUsuario.Designer.cs
│   │   └── AppDbContextModelSnapshot.cs
│   ├── Properties/launchSettings.json
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── FinanzasPersonales.Api.csproj
│   └── Dockerfile
├── tests/
│   ├── pruebas_integracion.py
│   └── pruebas_produccion.py
├── constructor.yml
├── .env.example
├── .gitignore
├── .dockerignore
├── VERIFICACION.md
└── README.md
```

`.env` es local, se ignora en Git y Docker y nunca debe publicarse. `Migrations/` y `.env.example` sí se versionan.

## Requisitos y dependencias

- Docker Engine/Desktop con Compose v2 o posterior, ejecutándose.
- Puerto local 8080 libre.
- Para compilar fuera de Docker: SDK .NET 10. Para las pruebas automatizadas: Python 3, sin paquetes adicionales.
- Git; opcionalmente curl y OpenSSL para los ejemplos de Linux/macOS/WSL.

Desde la raíz:

```bash
dotnet tool restore
dotnet restore
dotnet build
dotnet build -c Release
dotnet list FinanzasPersonales.Api package
```

Docker instala/restaura las dependencias de la API durante la construcción; no requiere el SDK en el host.

## Configuración local y arranque

```bash
# Solo la primera vez; no sobrescribe una configuración existente.
test -f .env || cp .env.example .env
chmod 600 .env
openssl rand -hex 32
```

En una instalación nueva, editar `.env`: sustituir `POSTGRES_USER` por un usuario local y `POSTGRES_PASSWORD` por el valor aleatorio anterior. No usar los marcadores de ejemplo como credenciales. **En el entorno entregado ya se generó un `.env` local con contraseña aleatoria y se inicializó PostgreSQL: conservarlo.** Para esta configuración, evitar `;`, saltos de línea y comillas en esos valores porque se interpolan en una cadena de conexión Npgsql; una contraseña hexadecimal aleatoria es adecuada.

```bash
docker compose -f constructor.yml up --build -d
docker compose -f constructor.yml ps
docker compose -f constructor.yml logs --tail=100 api
docker compose -f constructor.yml exec -T postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

Compose obtiene las credenciales de `.env` y construye `ConnectionStrings__DefaultConnection` con `Host=postgres`, nunca localhost. `.env` lo lee Compose; `dotnet run` **no** lo carga automáticamente. Las credenciales no se incorporan a la imagen ni a los archivos appsettings. No compartir la salida de `docker compose config` o `docker inspect`: puede contener secretos.

La API espera el healthcheck de PostgreSQL. Solo en **Development**, con `Database__ApplyMigrations=true`, aplica las migraciones mediante EF al arrancar, con hasta 15 intentos ante fallos transitorios. Un error permanente hace fallar el arranque en vez de servir una API con esquema inválido. EF utiliza su bloqueo de migraciones. En producción la opción se ignora; aplicar migraciones como tarea controlada antes de iniciar la API.

### Scalar y OpenAPI

- **http://localhost:8080/scalar**
- Documento: http://localhost:8080/openapi/v1.json

Ambos solo están habilitados en Development. Scalar permite ejecutar los tres endpoints. La comprobación automatizada verifica HTML y OpenAPI; la interacción visual se puede revisar en el navegador.

## Endpoints

| Método | Ruta | Resultado |
|---|---|---|
| GET | `/api/usuarios` | 200, solo activos; `[]` si no hay ninguno |
| POST | `/api/usuarios` | 201; 400 si falla la validación; 409 si el email existe |
| DELETE | `/api/usuarios/{id}` | 204 tanto si se desactiva como si ya estaba inactivo; 404 si no existe |

La API solo devuelve `id`, `nombre`, `email`, `fechaCreacion`. No devuelve contraseña, hash ni `activo`. Los errores usan ProblemDetails/ValidationProblemDetails sin enviar trazas internas.

### Crear y listar

JSON de POST:

```json
{
  "nombre": "Usuario Ejemplo",
  "email": "usuario@ejemplo.com",
  "password": "ClaveSegura123!"
}
```

```bash
curl -i http://localhost:8080/api/usuarios
curl -i -X POST http://localhost:8080/api/usuarios \
  -H 'Content-Type: application/json' \
  --data '{"nombre":"Usuario Ejemplo","email":"usuario@ejemplo.com","password":"ClaveSegura123!"}'
```

Repetir POST con el mismo email, incluso en mayúsculas, devuelve **409**, también si el usuario está inactivo. La consulta previa y la captura de la violación del índice único cubren registros concurrentes. `Location` del 201 apunta a la colección `/api/usuarios`: deliberadamente no existe GET por id.

Validación: nombre obligatorio, no solo espacios, hasta 100 caracteres; email válido de hasta 150; contraseña obligatoria de 12 a 72 caracteres y **máximo 72 bytes UTF-8**, no solo espacios ni con NUL (límite de BCrypt, evita truncamiento silencioso). El nombre se recorta; el email se recorta y pasa a minúsculas. El esquema impide correos sin normalizar con una restricción y crea `ux_usuario_email` único. La unicidad no depende solo de la consulta del controlador.

`Guid` y fecha UTC se asignan automáticamente; la columna de fecha también tiene `CURRENT_TIMESTAMP` predeterminado. `activo` tiene valor predeterminado `true`. La tabla y las columnas usan snake_case. No se habilitan logs de datos sensibles ni del cuerpo de las peticiones. `IPasswordHasher` deja un punto de sustitución para autenticación futura, sin implementarla todavía.

### Comprobar soft delete

Sustituir el UUID por el `id` devuelto por POST:

```bash
ID='REEMPLAZAR_POR_UUID'
curl -i -X DELETE "http://localhost:8080/api/usuarios/$ID"
curl -i http://localhost:8080/api/usuarios
curl -i -X DELETE "http://localhost:8080/api/usuarios/$ID"
docker compose -f constructor.yml exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT id, nombre, email, activo, fecha_creacion FROM usuario ORDER BY fecha_creacion;"'
```

DELETE devuelve 204, GET omite al usuario y PostgreSQL conserva la fila con `activo = false`. La segunda eliminación devuelve 204 de forma idempotente. No se usa borrado físico. El SQL de este README y de las pruebas es únicamente de consulta, no de creación de tablas.

## Migraciones Code First

La migración **InicialUsuario** ya está generada y versionada: crea `usuario`, PK, restricciones y el índice único. No ejecutar de nuevo el comando de creación inicial en este repositorio. Para reproducir su generación en un proyecto sin migraciones se usó:

```bash
dotnet ef migrations add InicialUsuario --project FinanzasPersonales.Api
```

Consultar estado y crear una futura migración después de modificar el modelo:

```bash
dotnet tool restore
dotnet ef migrations list --project FinanzasPersonales.Api --no-connect
dotnet ef migrations has-pending-model-changes --project FinanzasPersonales.Api
# Solo cuando haya cambios reales:
dotnet ef migrations add NombreDescriptivo --project FinanzasPersonales.Api
```

La fábrica de diseño permite generar migraciones sin credenciales ni PostgreSQL disponible. Para **aplicarlas** necesita la conexión real en una variable de entorno. Compose ya aplica automáticamente las pendientes en desarrollo. Para una actualización controlada desde el SDK se puede ejecutar en la misma red Docker, sin publicar PostgreSQL:

```bash
# Con PostgreSQL de Compose en marcha; las variables se leen sin mostrar su valor.
set -a
. ./.env
set +a
docker run --rm --network finanzas-personales_default \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e NUGET_PACKAGES=/tmp/nuget \
  -e "ConnectionStrings__DefaultConnection=Host=postgres;Database=finanzas_personales;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD;Include Error Detail=false" \
  -v "$PWD:/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c 'dotnet tool restore && dotnet restore --force && dotnet ef database update --project FinanzasPersonales.Api'
unset POSTGRES_USER POSTGRES_PASSWORD
```

Usar valores compatibles con shell en `.env` para este ejemplo (usuario sencillo y contraseña hexadecimal). `restore --force` regenera las rutas de paquetes para el SDK del contenedor; si después se compila en el host, ejecutar de nuevo `dotnet restore --force`. En producción usar un gestor de secretos, backups y un proceso de migración explícito; no reutilizar este Compose de desarrollo.

Para ejecutar sin Docker, suministrar `ConnectionStrings__DefaultConnection` a una instancia PostgreSQL accesible, aplicar la migración y arrancar:

```bash
# Configurar la variable por un mecanismo seguro; no versionarla.
dotnet ef database update --project FinanzasPersonales.Api
dotnet run --project FinanzasPersonales.Api --launch-profile http
```

## Pruebas y persistencia

```bash
python3 tests/pruebas_integracion.py
# Incluye down/up sin borrar el volumen: interrumpe brevemente la API local.
python3 tests/pruebas_integracion.py --reiniciar-contenedores
# Comprueba Production en un contenedor temporal, sin tocar PostgreSQL local.
python3 tests/pruebas_produccion.py
```

La suite comprueba Scalar/OpenAPI, listado, creación, normalización, validaciones 400, duplicados 409, concurrencia, hash con salt, 404, DELETE idempotente, exclusión en GET y conservación de la fila con `activo=false`. Con la opción adicional compara las filas completas antes y después de recrear los contenedores, incluido un usuario activo y uno inactivo. No borra datos; deja registros identificables por emails aleatorios `prueba.*` y `concurrencia.*`.

Prueba manual de persistencia:

```bash
docker compose -f constructor.yml down
docker compose -f constructor.yml up -d
docker compose -f constructor.yml ps
curl http://localhost:8080/api/usuarios
docker compose -f constructor.yml exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT id, email, activo FROM usuario;"'
```

`down` conserva el volumen `finanzas-personales_postgres_data`; **no usar `down -v` ni borrar el volumen** si se quieren conservar los registros. Cambiar las credenciales de `.env` no cambia automáticamente las de una base de datos ya inicializada. PostgreSQL 18 monta `/var/lib/postgresql`, no la ruta de volumen de versiones anteriores.

## GitHub (sin publicar automáticamente)

El repositorio Git local ya está inicializado con rama `main`; no se ha creado un commit ni configurado un remoto.

```bash
git status --short
git check-ignore .env
git add .
git diff --cached --stat
# Revisar que no se incluyan secretos antes de confirmar.
git commit -m "Backend inicial: usuarios, EF Core, PostgreSQL y Docker"
# Crear previamente un repositorio vacío en GitHub y sustituir los marcadores:
git remote add origin https://github.com/TU_USUARIO/FinanzasPersonales.git
git push -u origin main
```

## Imagen Docker Hub (sin publicar automáticamente)

Desde la raíz; no cambiar el contexto al directorio de la API:

```bash
docker build -f FinanzasPersonales.Api/Dockerfile -t finanzas-personales-api:1.0.0 .
docker tag finanzas-personales-api:1.0.0 TU_USUARIO/finanzas-personales-api:1.0.0
docker login
docker push TU_USUARIO/finanzas-personales-api:1.0.0
```

El Dockerfile usa SDK 10.0 para restore/build/publish y ASP.NET 10.0 para ejecución; escucha HTTP 8080 y se ejecuta con el usuario no privilegiado `APP_UID`. No contiene credenciales. La imagen publicada **no debe exponerse sin autenticación, autorización y TLS**. Scalar y migraciones automáticas están apagados por defecto fuera de Development. No se ha publicado nada en GitHub ni Docker Hub.

Consultar [VERIFICACION.md](VERIFICACION.md) para los resultados realmente obtenidos en este entorno.
