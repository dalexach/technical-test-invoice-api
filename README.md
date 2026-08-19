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

Las partes 2 y 3 son documentos y se leen directamente. El resto de este archivo
describe la parte 1: cómo ejecutar el microservicio y qué decisiones se tomaron
al construirlo.

---

## Parte 1 — Puesta en marcha

### Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server, en contenedor o instalado. Sirve cualquier edición, incluidas
  Express y Developer, que son gratuitas.

### 1. Configuración

```bash
cp .env.example .env
```

Edite `.env` y reemplace `Jwt__Key` y `Jwt__Client__ClientSecret` por valores
propios. El archivo `.env` está excluido del control de versiones: en el
repositorio no se versiona ningún secreto, y `appsettings.json` deja esos campos
vacíos a propósito.

### 2. Base de datos

Hay dos formas de dejarla lista. La primera no requiere instalar SQL Server; la
segunda sirve si ya se dispone de una instancia o no se usa Docker.

#### Opción A: con Docker

```bash
docker compose up -d
```

Levanta SQL Server 2022, aplica [`database/schema.sql`](database/schema.sql) y
carga [`database/seed.sql`](database/seed.sql). Queda lista la base `InvoiceDb`
con la tabla `Invoices`, el índice de búsqueda, los tres procedimientos
almacenados y cien facturas de ejemplo repartidas entre siete clientes, de modo
que los endpoints de consulta se puedan probar sin crear datos a mano. El reparto
es desigual a propósito: el cliente con más facturas supera el tamaño de página
por defecto, así que la paginación se aprecia sin tener que forzar parámetros.

Los datos de ejemplo son opcionales y solo se insertan si la tabla está vacía;
`schema.sql` por sí solo no inserta nada.

En Apple Silicon la imagen oficial corre emulada, por lo que el primer arranque
tarda algunos minutos. El estado se consulta con:

```bash
docker compose ps
```

#### Opción B: con una instancia propia de SQL Server

Sirve cualquier instancia accesible: una local instalada en la máquina, LocalDB
en Windows, o una remota. Son dos pasos.

Primero, ejecutar los dos scripts contra la instancia, en este orden. Desde SQL
Server Management Studio o Azure Data Studio basta con abrirlos y ejecutarlos;
desde la línea de comandos:

```bash
sqlcmd -S localhost -U sa -P "<su-contraseña>" -C -i database/schema.sql
sqlcmd -S localhost -U sa -P "<su-contraseña>" -C -i database/seed.sql
```

`schema.sql` crea la base `InvoiceDb`, la tabla, el índice y los procedimientos
almacenados. `seed.sql` carga los datos de ejemplo y es opcional.

Después, ajustar la cadena de conexión en `.env` para que apunte a esa instancia:

```
ConnectionStrings__DefaultConnection="Server=localhost;Database=InvoiceDb;User Id=sa;Password=<su-contraseña>;TrustServerCertificate=True;Encrypt=True;"
```

En Windows con autenticación integrada, la cadena sustituye usuario y contraseña
por `Trusted_Connection=True`.

### 3. Ejecución

La aplicación lee su configuración de variables de entorno, así que primero hay
que cargar el archivo `.env` en la sesión actual.

En Linux o macOS:

```bash
set -a && source .env && set +a
dotnet run --project src/InvoiceApi
```

En Windows, desde PowerShell:

```powershell
Get-Content .env | Where-Object { $_ -match '^\s*[^#].*=' } | ForEach-Object {
    $nombre, $valor = $_ -split '=', 2
    [Environment]::SetEnvironmentVariable($nombre.Trim(), $valor.Trim('"', ' '))
}
dotnet run --project src/InvoiceApi
```

Desde Visual Studio no hace falta nada de lo anterior: basta con abrir
`InvoiceApi.sln` y ejecutar el proyecto, definiendo antes las mismas variables en
las propiedades de depuración o mediante `dotnet user-secrets`.

Swagger UI queda disponible en la URL que indique la consola, en `/swagger`.

### 4. Pruebas

```bash
dotnet test
```

52 pruebas: validación del modelo, comportamiento del controlador, emisión de
tokens y pruebas de integración sobre la aplicación completa en memoria.

---

## Endpoints

| Método | Ruta | Autenticación | Descripción |
|---|---|---|---|
| `POST` | `/auth/token` | No | Emite un token de acceso |
| `POST` | `/invoice` | Sí | Registra una factura |
| `GET` | `/invoice/{id}` | Sí | Consulta una factura por identificador |
| `GET` | `/invoice/search?client={nombre}` | Sí | Busca las facturas de un cliente |

La búsqueda admite `matchMode` para elegir cómo se compara el nombre.

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

**Modos de coincidencia.** El parámetro `matchMode` decide cómo se compara el
nombre del cliente:

| Modo | Comportamiento | Coste |
|---|---|---|
| `Exact` (por defecto) | Coincidencia completa | Búsqueda puntual en el índice |
| `Prefix` | Nombres que empiezan por el texto | Búsqueda por rango en el índice |
| `Contains` | Nombres que contienen el texto | Recorrido de la tabla |

```bash
curl "http://localhost:5219/invoice/search?client=Sura&matchMode=Contains" \
  -H "Authorization: Bearer $TOKEN"
```

Con los datos de ejemplo, la diferencia entre los tres modos se ve así:

| Búsqueda | `Exact` | `Prefix` | `Contains` |
|---|---|---|---|
| `Seguros Sura` | 4 | 4 | 4 |
| `Seguros` | 0 | 7 | 7 |
| `sura` | 0 | 0 | 4 |

Los siete clientes reparten las cien facturas de forma desigual, como ocurre en
la práctica: `Bancolombia S.A.` acumula 62 y `Davivienda S.A.` 29, mientras que
el resto tiene entre 1 y 4.

`Contains` existe porque hay búsquedas que los otros dos modos no resuelven. En
las razones sociales del sector asegurador la parte distintiva suele ir al final
—Seguros Sura, Seguros Bolívar, Seguros del Estado— de modo que buscar por
prefijo devuelve toda la industria y no la compañía concreta.

Su contrapartida es que un patrón con comodín inicial impide al motor acotar el
rango y lo obliga a recorrer la tabla. Por eso no es el modo predeterminado.
Verificado sobre 300.000 filas: `Exact` y `Prefix` resuelven con `Index Seek`,
mientras que `Contains` degrada a `Index Scan`. Cuando el volumen lo justifique,
la evolución natural es un índice de texto completo, que resuelve la búsqueda
por palabra sin recorrer la tabla.

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
procedimiento con parámetros tipados, lo que además mantiene estable el plan de
ejecución y descarta la inyección SQL por construcción.

**Índice de cobertura para la búsqueda por cliente.** `IX_Invoices_ClientName`
indexa `(ClientName, IssueDate DESC)` e incluye `Amount`, `Status` y `CreatedAt`.
La clave resuelve el filtro y el orden; las columnas incluidas evitan volver a la
tabla por cada fila. El plan resuelve con `Index Seek`.

**Tres modos de búsqueda, con el más barato por defecto.** La coincidencia exacta
y la de prefijo se resuelven con el índice; la de contenido recorre la tabla y por
eso debe pedirse de forma explícita. Exponer el compromiso en la API, en lugar de
imponer un único modo, deja la decisión en quien conoce el tamaño de sus datos.

**Paginación en el procedimiento almacenado.** `SP_SearchInvoicesByClient` pagina
con `OFFSET`/`FETCH` y devuelve además el total de coincidencias. El límite se
valida en la API y de nuevo en el procedimiento, por si se invoca directamente.
Sin él, un cliente con decenas de miles de facturas produciría respuestas de
tamaño arbitrario.

**Autorización por defecto.** Una política de respaldo exige autenticación en
todo endpoint; el acceso anónimo es explícito y se limita a `/auth/token`. Así,
un endpoint nuevo queda protegido aunque se olvide anotarlo.

**Los errores no filtran detalles internos.** Un manejador global convierte
cualquier excepción no controlada en una respuesta genérica con un `traceId`, y
registra el detalle del lado del servidor. Los mensajes que genera el
deserializador de .NET, en inglés y con el tipo interno, se sustituyen por otros
propios; los de las reglas del modelo llegan intactos.

El resto de decisiones menores está documentado en el propio código, junto al
punto donde aplica.

## Estructura

```
.
├── database/
│   ├── schema.sql                     Tabla, índice y procedimientos almacenados
│   └── seed.sql                       Datos de ejemplo (opcional)
├── src/InvoiceApi/
│   ├── Auth/                          Emisión y configuración de tokens
│   ├── Controllers/                   Endpoints de autenticación y facturas
│   ├── Data/                          Repositorio sobre procedimientos almacenados
│   ├── Infrastructure/                Manejo de errores y respuestas de validación
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
- Los tres modos de búsqueda se comportan como se documenta, y el comodín `%` se
  trata como texto literal en lugar de alterar el patrón.
- Sobre 300.000 filas, la coincidencia exacta y la de prefijo resuelven con
  `Index Seek`; la búsqueda por contenido degrada a `Index Scan`.
- Las 52 pruebas automatizadas pasan.
