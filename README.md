# Invoice API — Prueba técnica

Microservicio REST en ASP.NET Core 8 para el registro y la consulta de facturas
sobre SQL Server, con acceso a datos exclusivamente mediante procedimientos
almacenados.

El repositorio contiene los tres entregables de la prueba:

| Parte | Entregable | Ubicación |
|---|---|---|
| 1 | API REST con SQL Server y procedimientos almacenados | [`src/`](src/), [`database/`](database/) |
| 2 | Optimización del prompt de resumen | [`PROMPTS.md`](PROMPTS.md) |
| 3 | Estrategia de pruebas volumétricas | [`VOLUMETRIC_TEST_STRATEGY.md`](VOLUMETRIC_TEST_STRATEGY.md) |

---

## Puesta en marcha

### Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (para la base de datos) o una instancia de SQL Server accesible

### 1. Configuración

```bash
cp .env.example .env
```

Edite `.env` y reemplace `Jwt__Key` y `Jwt__Client__ClientSecret` por valores
propios. El archivo `.env` está excluido del control de versiones: en el
repositorio no se versiona ningún secreto, y `appsettings.json` deja esos campos
vacíos a propósito.

### 2. Base de datos

```bash
docker compose up -d
```

Levanta SQL Server 2022 y aplica [`database/schema.sql`](database/schema.sql)
automáticamente: crea la base `InvoiceDb`, la tabla `Invoices`, el índice de
búsqueda y los tres procedimientos almacenados.

En Apple Silicon la imagen oficial corre emulada, por lo que el primer arranque
tarda algunos minutos. El estado se consulta con:

```bash
docker compose ps
```

Para una instancia propia de SQL Server, basta ejecutar `database/schema.sql` y
ajustar `ConnectionStrings__DefaultConnection` en `.env`.

### 3. Ejecución

```bash
set -a && source .env && set +a
dotnet run --project src/InvoiceApi
```

Swagger UI queda disponible en la URL que indique la consola, en `/swagger`.

### 4. Pruebas

```bash
dotnet test
```

38 pruebas: validación del modelo, comportamiento del controlador, emisión de
tokens y pruebas de integración sobre la aplicación completa en memoria.

---

## Endpoints

| Método | Ruta | Autenticación | Descripción |
|---|---|---|---|
| `POST` | `/auth/token` | No | Emite un token de acceso |
| `POST` | `/invoice` | Sí | Registra una factura |
| `GET` | `/invoice/{id}` | Sí | Consulta una factura por identificador |
| `GET` | `/invoice/search?client={nombre}` | Sí | Busca las facturas de un cliente |

### Autenticación

Los endpoints de facturas requieren un token JWT. Se obtiene con las credenciales
configuradas en `.env`:

```bash
curl -X POST http://localhost:5219/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"wtw-demo-client","clientSecret":"<su-secreto>"}'
```

La respuesta incluye `accessToken`, que se envía en la cabecera
`Authorization: Bearer {token}`. En Swagger UI, el botón **Authorize** acepta el
token directamente.

### Ejemplos

**Registrar una factura**

```bash
curl -X POST http://localhost:5219/invoice \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{
    "clientName": "Seguros Sura",
    "amount": 98500000.00,
    "issueDate": "2026-08-18T00:00:00",
    "status": "PENDING"
  }'
```

```http
HTTP/1.1 201 Created
Location: http://localhost:5219/invoice/1
```

```json
{
  "id": 1,
  "clientName": "Seguros Sura",
  "amount": 98500000.00,
  "issueDate": "2026-08-18T00:00:00",
  "status": "PENDING",
  "createdAt": "2026-08-18T23:29:50"
}
```

**Buscar por cliente**

```bash
curl "http://localhost:5219/invoice/search?client=Seguros%20Sura&page=1&pageSize=50" \
  -H "Authorization: Bearer $TOKEN"
```

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 3,
  "totalPages": 1
}
```

La búsqueda sin coincidencias devuelve `200` con `items` vacío: la forma de la
respuesta no cambia según haya o no resultados, de modo que el cliente la procesa
siempre igual.

### Respuestas de error

Los errores siguen [RFC 7807 (Problem Details)](https://datatracker.ietf.org/doc/html/rfc7807)
e incluyen un `traceId` que correlaciona la respuesta con el registro del
servidor.

| Código | Situación |
|---|---|
| `400` | Datos inválidos o incompletos; parámetros de búsqueda o paginación fuera de rango |
| `401` | Token ausente, inválido o expirado; credenciales incorrectas |
| `404` | La factura no existe |
| `500` | Error interno. El detalle técnico queda en el registro del servidor, nunca en la respuesta |

---

## Decisiones técnicas

**Dapper en lugar de Entity Framework.** La prueba exige procedimientos
almacenados sin EF. Dapper aporta el mapeo objeto-relacional sin capa de
seguimiento de entidades ni generación de SQL: cada operación invoca su
procedimiento con parámetros tipados.

**Parámetros tipados con tamaño explícito.** Declarar `DbType` y `size` en cada
parámetro evita conversiones implícitas que impedirían el uso del índice, y
mantiene estable el plan de ejecución cacheado.

**Índice de cobertura.** `IX_Invoices_ClientName` indexa `(ClientName, IssueDate DESC)`
e incluye `Amount`, `Status` y `CreatedAt`. La clave resuelve el filtro y el
orden; las columnas incluidas evitan volver a la tabla. El plan de ejecución
resuelve con `Index Seek`.

**Paginación en el procedimiento almacenado.** `SP_SearchInvoicesByClient` pagina
con `OFFSET`/`FETCH` y devuelve además el total de coincidencias. El límite se
valida en la API (400 si `pageSize` excede 200) y de nuevo en el procedimiento,
que acota el valor por si se invoca directamente. Sin este límite, un cliente con
decenas de miles de facturas produciría respuestas de tamaño arbitrario.

**El procedimiento de inserción devuelve la fila creada.** Usa la cláusula
`OUTPUT`, lo que evita una segunda consulta y elude las conversiones de
`SCOPE_IDENTITY()`, que devuelve `numeric(38,0)`.

**Autorización por defecto.** Una política de respaldo exige autenticación en
todo endpoint; el acceso anónimo es explícito y se limita a `/auth/token`. Así,
un endpoint nuevo queda protegido aunque se olvide anotarlo.

**Los errores no exponen detalles internos.** Un manejador global convierte
cualquier excepción no controlada en una respuesta genérica y registra el detalle
del lado del servidor. Devolver el mensaje de la excepción revelaría estructura
interna y, en el caso de errores de conexión, datos de la base.

**Configuración validada al arranque.** Las opciones de JWT se validan al iniciar:
si falta la clave de firma o mide menos de 32 caracteres, el proceso no arranca.
Es preferible a levantar una API con la seguridad inoperante.

**Comparación en tiempo constante.** La validación de credenciales usa
`CryptographicOperations.FixedTimeEquals`, de modo que el tiempo de respuesta no
revela cuántos caracteres del secreto son correctos.

---

## Estructura

```
.
├── database/
│   └── schema.sql                     Tabla, índice y procedimientos almacenados
├── src/InvoiceApi/
│   ├── Auth/                          Emisión y configuración de tokens
│   ├── Controllers/                   Endpoints de autenticación y facturas
│   ├── Data/                          Repositorio sobre procedimientos almacenados
│   ├── Infrastructure/                Manejador global de excepciones
│   ├── Models/                        Contratos de entrada y salida
│   └── Program.cs                     Composición de servicios y pipeline
├── tests/InvoiceApi.Tests/            Pruebas unitarias y de integración
├── docker-compose.yml                 SQL Server y aplicación del esquema
├── PROMPTS.md                         Parte 2
└── VOLUMETRIC_TEST_STRATEGY.md        Parte 3
```

---

## Verificación realizada

El proyecto se ejecutó contra SQL Server 2022 en contenedor, comprobando:

- Los tres endpoints de facturas rechazan con `401` las peticiones sin token, y
  `/auth/token` rechaza credenciales incorrectas.
- `POST /invoice` devuelve `201` con la cabecera `Location`, y `400` con el
  detalle de cada regla incumplida cuando los datos son inválidos.
- `GET /invoice/{id}` devuelve la factura o `404` sin filtrar detalles internos.
- `GET /invoice/search` resuelve su propia ruta sin colisionar con la de
  identificador, ordena por fecha descendente y pagina correctamente.
- El plan de ejecución de la búsqueda por cliente resuelve con `Index Seek`
  sobre `IX_Invoices_ClientName`, sin escaneo de tabla.
- Las 38 pruebas automatizadas pasan.
