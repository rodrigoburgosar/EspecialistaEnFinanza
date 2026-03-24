## ADDED Requirements

### Requirement: Usar PostgreSQL como motor de persistencia
El sistema SHALL usar PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` como proveedor de base de datos, reemplazando SQLite. La connection string SHALL leerse desde la variable de entorno `DATABASE_URL`.

#### Scenario: Conexión exitosa a PostgreSQL
- **WHEN** `DATABASE_URL` está configurado y PostgreSQL está disponible
- **THEN** el sistema inicia correctamente y aplica las migraciones pendientes automáticamente al arrancar

#### Scenario: PostgreSQL no disponible en desarrollo
- **WHEN** el entorno es `Development` y no hay PostgreSQL configurado
- **THEN** el sistema usa SQLite como fallback mediante `appsettings.Development.json`

### Requirement: Migraciones EF Core versionadas
El sistema SHALL gestionar el esquema de base de datos mediante migraciones EF Core, no con `EnsureCreated()`.

#### Scenario: Primera ejecución con base de datos vacía
- **WHEN** el sistema inicia contra una base de datos PostgreSQL sin esquema previo
- **THEN** se aplican todas las migraciones pendientes automáticamente y la tabla `Recomendaciones` queda creada con sus índices
