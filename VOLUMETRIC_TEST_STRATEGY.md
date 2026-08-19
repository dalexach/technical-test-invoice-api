# Estrategia de pruebas volumétricas — Invoice API (Parte 3)

Documento de diseño. No requiere implementación: describe cómo se planificaría,
ejecutaría e interpretaría una prueba volumétrica sobre un sistema de alto
tráfico. El escenario de la sección 2 se concreta sobre la API de facturas de la Parte 1,
tal como pide el enunciado; el resto del documento aplica a cualquier servicio
respaldado por una base de datos relacional.

### Correspondencia con lo solicitado

| Punto solicitado | Dónde se responde |
|---|---|
| 1. Definir qué es una prueba volumétrica | [Sección 1](#1-qué-es-una-prueba-volumétrica) |
| · Diferenciarla de pruebas de carga y estrés | [Sección 1.1](#11-diferencia-con-carga-y-estrés) |
| 2. Diseñar un escenario de pruebas volumétricas | [Sección 2](#2-escenario-de-prueba) |
| · Caso de uso realista sobre la API desarrollada | [Sección 2.1](#21-caso-de-uso) |
| · Volúmenes de datos y transacciones | [Sección 2.3](#23-volúmenes-de-datos), [Sección 2.4](#24-volumen-de-transacciones) |
| 3. Proponer métricas y KPIs | [Sección 3](#3-métricas-y-kpis) |
| · Qué indicadores medir | [Sección 3.1](#31-indicadores-a-medir), con su [aplicación al escenario](#aplicación-al-escenario-de-la-sección-2) |
| · Qué herramientas usar para la medición | [Sección 3.2](#32-herramientas-de-medición) |
| 4. Describir una estrategia de ejecución | [Sección 4](#4-estrategia-de-ejecución) |
| · Cómo se planificaría la ejecución | [Sección 4.1](#41-planificación-de-la-ejecución) |
| · Cómo se simularía el alto volumen | [Sección 4.2](#42-simulación-del-volumen-de-datos), [Sección 4.3](#43-simulación-del-tráfico) |
| · Criterios de éxito o fallo | [Sección 4.4](#44-criterios-de-éxito-o-fallo) |
| 5. Identificar cuellos de botella y soluciones | [Sección 5](#5-cuellos-de-botella-y-soluciones) |
| · Problemas esperados en la aplicación | [Sección 5](#5-cuellos-de-botella-y-soluciones) |
| · Soluciones ante degradación | [Sección 5](#5-cuellos-de-botella-y-soluciones), [Sección 6](#6-priorización-de-hallazgos) |

---

## 1. ¿Qué es una prueba volumétrica?

Una prueba volumétrica evalúa cómo se comporta un sistema cuando el volumen de
datos almacenados crece hasta los niveles que se proyectan en producción. La
pregunta que responde es si sigue respondiendo igual de rápido con decenas de
millones de registros acumulados que con unos pocos miles.

Es una degradación que no aparece con la carga, sino con la acumulación, y por
eso escapa a las pruebas habituales: un entorno de desarrollo con datos de
ejemplo nunca la reproduce. Los mecanismos por los que aparece son tres.

El primero es el cambio de plan de ejecución. El optimizador de la base de
datos decide cómo resolver una consulta a partir de estadísticas sobre los datos
existentes. Con una tabla pequeña puede resolver por índice; con la misma
consulta sobre una tabla grande y otra distribución de valores puede concluir
que le sale más barato recorrerla entera. La consulta no cambió, el plan sí.

El segundo es el desbordamiento de la memoria caché. Mientras los datos de
trabajo caben en el buffer de la base, las lecturas se sirven desde memoria.
Cuando dejan de caber, cada consulta empieza a tocar disco, y la diferencia de
latencia entre ambos casos es de dos órdenes de magnitud.

El tercero es el crecimiento del conjunto de resultados. Una consulta sin
paginar que devolvía veinte filas en el primer mes de operación devuelve decenas
de miles al cabo de unos años. El coste no está solo en la base: está en
transportar y serializar esa respuesta.

Ninguno de los tres se manifiesta de forma gradual y previsible. Suelen aparecer
de golpe, al cruzar un umbral, que es precisamente lo que hace valiosa esta
prueba: encontrar el umbral antes que el usuario.

### 1.1 Diferencia con carga y estrés

| Prueba | Qué se aumenta | Qué responde |
|---|---|---|
| Volumétrica | Datos almacenados | ¿Escala con el histórico acumulado? |
| Carga | Usuarios concurrentes esperados | ¿Cumple el SLA en operación normal? |
| Estrés | Usuarios más allá de lo esperado | ¿Dónde y cómo se rompe? |

La diferencia práctica está en qué se deja fijo. La prueba de carga y la de
estrés mantienen constante el volumen de datos y mueven la concurrencia; la
volumétrica hace lo contrario. Es también la única de las tres en la que no se
espera provocar un fallo.

Las tres son complementarias. En esta estrategia el eje es el volumen, y la
concurrencia se mantiene fija para que la única variable sea el tamaño del
histórico.

---

## 2. Escenario de prueba

### 2.1 Caso de uso

Una entidad financiera usa la API como repositorio central de facturación de sus
proveedores. El sistema lleva cinco años en operación y el área de cuentas por
pagar consulta constantemente el histórico por proveedor.

Tres operaciones concentran el uso:

1. Registro (`POST /invoice`): carga diaria, con un pico al cierre de mes.
2. Consulta puntual (`GET /invoice/{id}`): desde el detalle de un pago.
3. Consulta por cliente (`GET /invoice/search?client=`), la crítica: la
   conciliación mensual recorre el histórico de cada proveedor.

La tercera es donde el volumen duele. Un proveedor grande concentra decenas de
miles de facturas, y esa consulta es la que degrada primero.

### 2.2 Supuesto de partida

Los volúmenes no se eligen: se derivan de la operación que se quiere
representar. El punto de partida son dos datos del negocio.

- La entidad trabaja con unos 87.400 proveedores activos.
- Se registran alrededor de 712.000 facturas al mes.

De ahí sale todo lo demás. A cinco años el histórico ronda los 42,7 millones de
filas (712.000 × 12 × 5), y con el tamaño medio de fila del esquema actual, unos
158 bytes entre `nvarchar(100)`, `decimal(18,2)` y los dos campos de fecha, eso
son algo más de 6 GB de datos sin contar índices.

### 2.3 Volúmenes de datos

Se prueba en fases incrementales para observar la curva de degradación, no solo
el punto final:

| Fase | Filas en `Invoices` | Histórico equivalente | Datos |
|---|---|---|---|
| Base | 120.000 | ~1 semana | ~18 MB |
| F1 | 4,3 millones | ~6 meses | ~0,6 GB |
| F2 | 18,6 millones | ~2 años | ~2,7 GB |
| F3 | 42,7 millones | 5 años | ~6,3 GB |

La distribución por proveedor importa más que el total. Repartir las facturas de
forma uniforme entre los 87.400 proveedores daría unas 490 por cabeza y la
búsqueda nunca se vería en apuros. La realidad es que unos pocos proveedores
concentran el grueso de la facturación:

| Segmento | Proveedores | Facturas acumuladas c/u |
|---|---|---|
| Alta rotación | 40 | ~71.000 |
| Recurrentes | 3.800 | ~6.200 |
| Ocasionales | 83.560 | ~205 |

Los segmentos suman ~43,5 millones, un 2% por encima de la proyección lineal.
El desajuste es esperable: la proyección asume un ritmo constante y el reparto
por segmento no lo es.

El caso adverso es el proveedor de alta rotación. Una búsqueda sin paginar sobre
71.000 facturas es exactamente lo que debe probarse, y es también lo que la
distribución uniforme habría ocultado.

### 2.4 Volumen de transacciones

La tasa de escritura sale del mismo supuesto: 712.000 facturas al mes repartidas
en unos 20 días hábiles dan cerca de 1,2 registros por segundo en promedio, que
en el cierre mensual se multiplica varias veces. Se prueba con el pico, no con el
promedio.

Las lecturas dominan porque el histórico se consulta mucho más de lo que se
escribe:

| Operación | Proporción | Tasa objetivo |
|---|---|---|
| `GET /invoice/search` | 60% | 30 req/s |
| `GET /invoice/{id}` | 30% | 15 req/s |
| `POST /invoice` | 10% | 5 req/s |

En total unas 50 peticiones por segundo sostenidas durante 30 minutos en cada
fase. La tasa se mantiene igual en las cuatro: si cambiara junto con el volumen,
no habría forma de saber cuál de las dos variables causó la degradación.

---

## 3. Métricas y KPIs

### 3.1 Indicadores a medir

Los indicadores se agrupan en tres capas, y el orden importa. Las métricas de
aplicación dicen si hay un problema; las de base de datos, por qué; las de
infraestructura, si el sistema tiene margen para absorberlo. Medir solo la
primera capa deja ver la degradación sin poder explicarla.

Los umbrales que siguen son los que se usarían de referencia; en un caso real se
toman del acuerdo de nivel de servicio del sistema, y si no existe, del
comportamiento actual medido en producción más el margen que el negocio acepte.

#### Métricas de aplicación

| Métrica | Por qué importa | Umbral |
|---|---|---|
| Latencia p50 | Experiencia típica | < 200 ms |
| Latencia p95 | Experiencia en la cola | < 800 ms |
| Latencia p99 | Peor caso tolerable | < 2 s |
| Throughput | Capacidad efectiva | ≥ 50 req/s sostenidos |
| Tasa de error | Estabilidad | < 0,1% |
| Timeouts | Saturación incipiente | 0 |

Se miden percentiles y no promedios. Un promedio de 300 ms puede esconder que el
5% de las consultas tarda diez segundos, y ese 5% son justamente las de los
usuarios con más datos acumulados: los que más notan la degradación y los que
más peso suelen tener en el negocio. El p99 se vigila aparte porque marca el
peor caso que alguien va a experimentar de forma habitual.

La distinción entre las tres latencias tiene una lectura práctica: si el p50 se
mantiene y el p95 se dispara, el problema depende de los datos (unos usuarios
sufren y otros no). Si suben los tres a la vez, el problema es de recursos y
afecta a todos por igual.

#### Métricas de base de datos

| Métrica | Qué revela |
|---|---|
| Lecturas lógicas por consulta | Si el índice se usa o hay escaneo |
| Page Life Expectancy | Si el buffer pool retiene los datos calientes |
| Buffer cache hit ratio | Proporción de lecturas servidas desde memoria |
| Esperas por tipo (`PAGEIOLATCH`, `LCK_M_*`) | Si el cuello es E/S o bloqueo |
| Duración de la consulta más lenta | Identifica la operación a optimizar |

La métrica más informativa es la de lecturas lógicas por consulta: el número
de páginas de datos que la base tiene que leer para resolverla. Su valor está en
que no depende del hardware. Un servidor más rápido baja los milisegundos y
disimula el problema, pero las páginas leídas siguen siendo las mismas. Si ese
número crece en proporción al volumen, la consulta está recorriendo la tabla en
lugar de ir directa por el índice, y ningún hardware lo va a arreglar.

El indicador de permanencia en caché mide cuántos segundos sigue una página en
memoria antes de ser desalojada. Si cae al aumentar el volumen, es la señal del
segundo mecanismo descrito en la sección 1: los datos de trabajo dejaron de caber en
caché.

#### Aplicación al escenario de la sección 2

Llevadas al escenario planteado antes, las métricas anteriores se concretan así
para cada una de las tres operaciones:

| Operación | Qué se vigila de cerca | Comportamiento esperado si el sistema escala |
|---|---|---|
| `GET /invoice/search` | p95 y p99 por segmento de proveedor; lecturas lógicas por consulta | Las lecturas lógicas se mantienen en el mismo orden entre las cuatro fases, porque la consulta va por índice y solo devuelve una página |
| `GET /invoice/{id}` | p50 y lecturas lógicas | Prácticamente planas en todas las fases: es una búsqueda por clave primaria y no debería notar el volumen |
| `POST /invoice` | p95 y esperas por bloqueo | Ligero aumento por el mantenimiento del índice al crecer la tabla, sin esperas de bloqueo relevantes |

La consulta por identificador funciona aquí como grupo de control. Al resolverse
por clave primaria, su latencia no debería moverse con el volumen. Si se degrada
igual que la búsqueda, el problema no está en cómo está escrita esa búsqueda,
sino en algo compartido: memoria insuficiente, disco saturado o contención en el
pool de conexiones.

La búsqueda por cliente merece medirse separando los segmentos, no en conjunto.
Un promedio global mezcla los proveedores ocasionales, que devuelven unas
doscientas filas, con los de alta rotación, que acumulan decenas de miles. Con
la proporción de tráfico definida, los casos costosos quedan diluidos en el
percentil general, y precisamente son los que interesa observar.

#### Métricas de infraestructura

CPU de la aplicación y de la base por separado, memoria de trabajo, tiempo
dedicado a recolección de basura, profundidad de la cola de disco, latencia de
red y conexiones activas del pool frente a su máximo.

Interesan sobre todo como descarte. Si la latencia se degrada y estos
indicadores están holgados, el problema es de diseño de consultas o de índices,
no de capacidad, y añadir máquina no lo resolvería. La memoria merece atención
propia: lo que se busca no es que sea alta, sino que se estabilice. Un consumo
que crece de forma sostenida durante toda la prueba señala una fuga, y esa es
una falla que solo aparece en ejecuciones largas.

### 3.2 Herramientas de medición

Hacen falta cuatro piezas: algo que genere el tráfico, algo que recoja las
métricas, algo que las muestre y algo que explique qué pasa dentro de la base de
datos.

| Función | Herramienta | Por qué |
|---|---|---|
| Generación de carga | k6 | Los guiones se escriben en JavaScript y se versionan como código. Reporta percentiles de forma nativa y consume pocos recursos, lo que evita que el propio generador limite la prueba |
| Alternativa | Apache JMeter | Interfaz gráfica y ecosistema maduro de plugins. Conviene cuando el equipo ya lo domina o se necesitan protocolos que k6 no cubre |
| Métricas de aplicación | OpenTelemetry con Prometheus | Instrumentación estándar y agnóstica del proveedor. Permite cambiar de backend de observabilidad sin tocar el código instrumentado |
| Visualización | Grafana | Superpone en un mismo eje temporal las métricas de aplicación, base de datos e infraestructura. Correlacionar picos entre capas es la mitad del diagnóstico |
| Diagnóstico de la base | Query Store y vistas de gestión dinámica | Query Store guarda el histórico de planes, lo que permite ver si un plan cambió entre fases. Las vistas dan las consultas más costosas del periodo |
| Recursos del sistema | Exportador de métricas del nodo | CPU, memoria, disco y red del servidor, en la misma serie temporal que el resto |

Entre k6 y JMeter la elección práctica se reduce a esto: k6 encaja mejor si la
prueba va a ejecutarse de forma repetida dentro de una canalización de
integración continua, porque el guion es un archivo de texto que vive junto al
código. JMeter encaja mejor si quien diseña la prueba no programa.

Lo que no puede faltar, sea cual sea la herramienta, es que la generación de
carga corra en una máquina distinta de la del sistema bajo prueba. Si comparten
CPU, se acaba midiendo al generador.

---

## 4. Estrategia de ejecución

### 4.1 Planificación de la ejecución

La prueba se ejecuta sobre una réplica aislada con el mismo dimensionamiento que
producción. Si algo no puede coincidir, que no sean estos dos parámetros: la
memoria asignada a la base de datos, que determina cuánto histórico cabe en
caché, y el tipo de disco, porque entre uno mecánico y uno de estado sólido el
resultado cambia por completo.

Cuando no se dispone de una réplica equivalente, que es lo habitual, la prueba
sigue siendo útil si se acepta una condición: los valores absolutos de latencia
no serán trasladables a producción, pero la forma de la curva sí. Si al
multiplicar por diez el volumen la latencia se multiplica por diez, eso ocurrirá
igual en un servidor más potente, solo que partiendo de otro número. Lo que se
está midiendo es cómo escala el sistema, no cuánto tarda.

Cada fase sigue la misma secuencia:

1. Cargar el volumen correspondiente.
2. Actualizar las estadísticas con muestreo completo. Sin esto el optimizador
   trabaja con información obsoleta y el plan que se mida no será el que use
   producción.
3. Calentar durante cinco minutos, descartando esa ventana de la medición.
4. Medir treinta minutos a tasa constante.
5. Capturar métricas, planes de ejecución y las consultas más lentas del periodo.
6. Pasar a la fase siguiente sin reiniciar la base, porque el estado acumulado
   forma parte de lo que se está midiendo.

Las fases van siempre de menor a mayor volumen. Ejecutarlas en otro orden
impediría comparar cada resultado contra la línea base del propio sistema.

### 4.2 Simulación del volumen de datos

Generación por lotes con `INSERT ... SELECT` sobre una tabla de números, dentro
de la propia base. Es órdenes de magnitud más rápido que insertar fila por fila
desde un cliente externo, y evita que la red se convierta en el cuello de la
carga.

```sql
-- Genera filas respetando la distribución desigual por proveedor:
-- 40 de alta rotación, 3.800 recurrentes y el resto ocasionales.
-- Se ejecuta por lotes de 1.000.000 para acotar el log de transacciones.
WITH Numeros AS (
    SELECT TOP (1000000)
           ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_columns a
    CROSS JOIN sys.all_columns b
)
INSERT INTO dbo.Invoices (ClientName, Amount, IssueDate, Status)
SELECT
    CASE
        WHEN n % 15 = 0 THEN CONCAT('Proveedor Alta Rotacion ', n % 40)
        WHEN n % 2  = 0 THEN CONCAT('Proveedor Recurrente ',    n % 3800)
        ELSE                 CONCAT('Proveedor Ocasional ',     n % 83560)
    END,
    CAST((ABS(CHECKSUM(NEWID())) % 10000000) / 100.0 AS DECIMAL(18,2)) + 0.01,
    DATEADD(DAY, -(ABS(CHECKSUM(NEWID())) % 1825), SYSUTCDATETIME()),
    CHOOSE(1 + ABS(CHECKSUM(NEWID())) % 3, 'PENDING', 'PAID', 'CANCELLED')
FROM Numeros;
```

La carga en sí conviene prepararla. Con el modelo de recuperación en
`BULK_LOGGED` el registro de transacciones crece mucho menos, y ejecutar un
`CHECKPOINT` entre lotes evita que se acumule. Lo que más tiempo ahorra, sin
embargo, es deshabilitar los índices no agrupados antes de insertar y
reconstruirlos al terminar: reconstruir un índice una vez sale más barato que
mantenerlo actualizado en cada una de las millones de inserciones.

### 4.3 Simulación del tráfico

El generador ejecuta los tres tipos de operación en paralelo, respetando la
proporción definida en la sección 2.4. Cada uno se declara como un escenario
independiente con su propia tasa, de modo que la mezcla se mantenga estable
aunque uno de ellos se ralentice:

```javascript
export const options = {
  scenarios: {
    busqueda: { executor: 'constant-arrival-rate', rate: 30, timeUnit: '1s',
                duration: '30m', preAllocatedVUs: 60, exec: 'buscarPorCliente' },
    detalle:  { executor: 'constant-arrival-rate', rate: 15, timeUnit: '1s',
                duration: '30m', preAllocatedVUs: 30, exec: 'consultarPorId' },
    registro: { executor: 'constant-arrival-rate', rate: 5,  timeUnit: '1s',
                duration: '30m', preAllocatedVUs: 10, exec: 'registrarFactura' },
  },
  thresholds: {
    'http_req_duration{scenario:busqueda}': ['p(95)<800', 'p(99)<2000'],
    http_req_failed: ['rate<0.001'],
  },
};
```

El detalle que más condiciona la validez de la medición es el modo de llegada.
A tasa fija, el generador envía las peticiones al ritmo definido sin importar
cuánto tarde el sistema en responder. Si en su lugar se fijara un número de
usuarios en bucle, un sistema degradado recibiría menos peticiones precisamente
por estar lento, y la degradación se enmascararía a sí misma.

El nombre de cliente que recibe cada búsqueda se toma de una lista ponderada
según los segmentos definidos antes. Consultar siempre proveedores con pocos
registros mediría el caso fácil y daría por buena una consulta que en el caso
adverso tarda cien veces más.

El token de acceso se obtiene una vez por usuario virtual y se reutiliza durante
toda la ejecución, ya que pedir uno en cada petición acabaría midiendo el
servicio de autenticación en lugar del que interesa.

### 4.4 Criterios de éxito o fallo

Los criterios se fijan antes de ejecutar. Definirlos después, viendo ya los
resultados, lleva a racionalizar cualquier número que haya salido.

Se evalúan sobre la fase de mayor volumen, pero el criterio decisivo no es el
valor absoluto sino la comparación entre fases: un sistema que responde en 700
milisegundos y se mantiene ahí al cuadruplicar el volumen es mejor noticia que
uno que responde en 200 y se degrada a 900 en el mismo salto. El primero escala;
el segundo todavía no ha llegado a su límite.

Cada indicador tiene un valor por debajo del cual el resultado se da por bueno y
otro a partir del cual se considera fallo. Basta que uno solo entre en la
columna de fallo para no dar por superada la prueba.

| Indicador | Éxito | Fallo |
|---|---|---|
| Latencia p95 de la búsqueda | < 800 ms | > 2 s |
| Latencia p99 de la búsqueda | < 2 s | > 5 s |
| Tasa de error | < 0,1%, sin agotamientos de tiempo de espera | > 1%, o cualquier agotamiento |
| Throughput | 50 peticiones por segundo sostenidas | Por debajo de lo solicitado |
| Lecturas lógicas por consulta | Estables entre fases | Crecen en proporción al volumen |
| Memoria del proceso | Se estabiliza durante la medición | Crece de forma sostenida sin techo |
| Degradación entre fases consecutivas | Inferior al 50% | Superior al 50% |

Las lecturas lógicas son el indicador determinante. Si crecen en proporción al
volumen, la consulta está recorriendo la tabla, y aunque las latencias todavía
cumplan es cuestión de tiempo que dejen de hacerlo. La memoria funciona de forma
parecida: lo relevante no es el valor que alcanza, sino si llega a estabilizarse.

Entre ambas columnas queda una zona intermedia. El sistema cumple hoy, pero la
tendencia entre fases proyecta que dejará de cumplir antes de lo previsto. No
bloquea la salida a producción; sí justifica planificar la optimización y
acortar el intervalo de revisión.

Aunque todo pase, la prueba debería dejar registrado el volumen a partir del cual
el sistema dejaría de cumplir. Ese número es el que permite decidir cuándo toca
volver a intervenir.

---

## 5. Cuellos de botella y soluciones

Dos problemas de diseño concentran la mayor parte de la degradación por volumen,
y son los que conviene descartar primero. El resto de lo que suele aparecer se
resuelve por configuración.

### 5.1 Consulta de búsqueda sin índice de cobertura

Es el hallazgo más frecuente y el de mayor impacto. Se reconoce porque la
latencia de la búsqueda crece en proporción al volumen mientras las consultas por
clave primaria siguen planas, y porque el plan de ejecución muestra un recorrido
de tabla o un acceso adicional por cada fila devuelta.

La causa habitual es un índice que cubre la columna del filtro pero no las demás
columnas que la consulta necesita. El filtro se resuelve rápido y a continuación
la base vuelve a la tabla fila por fila para completar el resto. Sobre unos
cientos de filas no se nota; sobre decenas de miles, son decenas de miles de
accesos adicionales.

La solución es un índice de cobertura:

```sql
CREATE NONCLUSTERED INDEX IX_Tabla_Filtro
    ON dbo.Tabla (ColumnaFiltro ASC, ColumnaOrden DESC)
    INCLUDE (OtrasColumnasDeLaConsulta);
```

La columna del filtro va primero para permitir la búsqueda directa. La de
ordenamiento va después y en el mismo sentido en que se ordena, con lo que las
filas salen ya ordenadas y desaparece la operación de `Sort`. Las demás van en
`INCLUDE`, disponibles sin formar parte de la clave. El coste es espacio en disco
y escrituras algo más lentas, un intercambio favorable cuando las lecturas
dominan.

### 5.2 Respuestas sin límite de tamaño

El segundo por impacto, y el que produce las fallas más severas: no degradación
gradual sino errores y caídas del proceso.

Ocurre cuando un endpoint devuelve todos los resultados que encuentra. Con pocos
datos parece inofensivo; con decenas de miles de filas el servicio debe
materializar la lista completa en memoria y serializarla antes de enviar nada.
Varias peticiones así de forma simultánea agotan la memoria disponible.

La corrección es paginar en la consulta, no en el código que la llama:

```sql
ORDER BY ColumnaOrden DESC, ClavePrimaria DESC
OFFSET (@Pagina - 1) * @TamanoPagina ROWS
FETCH NEXT @TamanoPagina ROWS ONLY;
```

Dos detalles hacen la diferencia. El límite debe validarse tanto en la API como
en la consulta, para que siga vigente si alguien la invoca directamente. Y el
ordenamiento necesita la clave primaria como criterio de desempate: sin ella,
dos filas con el mismo valor pueden alternar de posición entre páginas, y un
registro acaba repitiéndose o no apareciendo nunca.

Cuando las páginas profundas empiezan a pesar, porque el desplazamiento obliga a
descartar todas las filas anteriores, la evolución natural es la paginación por
marcador, que arranca desde la última fila entregada.

### 5.3 Problemas de configuración

Los siguientes aparecen con menos frecuencia y se resuelven sin tocar código.

**Estadísticas desactualizadas.** El síntoma característico es una degradación
abrupta en lugar de gradual: el plan cambia de golpe tras una carga grande. El
umbral de actualización automática es proporcional al tamaño de la tabla, así que
en tablas muy grandes millones de filas nuevas pueden no dispararlo. Se corrige
actualizando las estadísticas de forma explícita y con muestreo completo después
de cada carga masiva.

**Bloqueos entre lectura y escritura.** Durante los picos de escritura, las
consultas esperan a que los escritores liberen las filas que están modificando.
Activar el aislamiento por versiones entrega a cada lector una copia consistente
en lugar de hacerlo esperar, a cambio de espacio en la base temporal.

**Pool de conexiones agotado.** Se manifiesta como errores de tiempo de espera al
obtener conexión mientras la base sigue holgada. Ampliar el tamaño máximo del
pool alivia el síntoma, pero lo que lo resuelve es liberar cada conexión en
cuanto se deja de usar y que la ejecución sea asíncrona de extremo a extremo,
para que ningún hilo quede bloqueado esperando.

**Crecimiento de los archivos de datos.** Produce pausas periódicas sin relación
con la consulta en curso, porque cada expansión del archivo detiene la escritura.
Se evita dimensionando los archivos por anticipado, con incrementos amplios.

## 6. Priorización de hallazgos

Una prueba volumétrica suele arrojar más hallazgos de los que se pueden atender
a la vez. El orden que sigue está pensado por relación entre lo que resuelve
cada medida y lo que cuesta aplicarla.

1. Índices de cobertura sobre las consultas críticas. Es lo que más impacto
   tiene por menos esfuerzo: no requiere tocar código de aplicación y suele
   convertir un recorrido de tabla en una búsqueda directa.
2. Paginación de los endpoints que devuelven listas. Evita el modo de fallo más
   severo, que es el agotamiento de memoria del proceso, y a diferencia del
   punto anterior sí obliga a cambiar el contrato de la API.
3. Ajustes de configuración de la base de datos: estadísticas, aislamiento por
   versiones, dimensionamiento previo de archivos. No exigen cambios de código y
   se revierten con facilidad si algo no funciona como se esperaba.
4. Una capa de caché para las consultas más repetidas, solo si tras lo anterior
   el rendimiento sigue siendo insuficiente. Introduce el problema de la
   invalidación, que es complejidad permanente, y por eso no conviene adoptarla
   de forma preventiva.
5. Particionado de las tablas grandes por rango de fecha, o archivado del
   histórico que ya no se consulta. Es la medida de mayor alcance y la que más
   trabajo operativo implica; se reserva para cuando el volumen supere lo que
   las anteriores pueden absorber.

El criterio general es agotar lo que no añade piezas nuevas antes de introducir
componentes adicionales. Cada elemento que se suma a la arquitectura es uno más
que hay que operar, vigilar y mantener.
