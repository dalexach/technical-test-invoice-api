# Optimización del prompt de resumen (Parte 2)

## Contexto

El prompt original pide resumir una comunicación sobre una póliza de vida
asociada a un crédito bancario. El texto mezcla dos bloques de naturaleza
distinta: **obligaciones de aviso con plazos** (revocación, siniestro, mora) y
**características de la póliza** (valor asegurado, coberturas, vigencia, forma
de pago). Es un documento con valor contractual: un resumen que altere un plazo
o un monto no es un resumen impreciso, es un resumen incorrecto.

---

## 1. ¿Qué mejoras hice y por qué?

### 1.1 Delimitar el texto fuente

El original inserta el documento directamente después de `"Resume el siguiente
texto:"` y cierra con `"Devuelve solo un resumen corto y preciso"` *después* del
contenido. El documento contiene sus propios numerales y verbos en imperativo,
de modo que no hay frontera entre instrucción y dato.

Encierro el documento en etiquetas `<documento>` y coloco todas las
instrucciones **antes** del bloque. Así el modelo distingue qué debe obedecer y
qué debe procesar, y una frase imperativa dentro del documento no puede
reinterpretarse como orden.

### 1.2 Fijar el rol y el destinatario

El original no dice quién lee el resumen. Sin destinatario, el modelo elige un
registro arbitrario y decide por su cuenta qué es relevante.

Defino el rol (analista de seguros) y el destinatario (un ejecutivo bancario que
necesita conocer obligaciones y coberturas). Eso ancla el criterio de relevancia:
los plazos de aviso importan porque generan obligaciones para el banco.

### 1.3 Estructura de salida explícita

`"un resumen corto y preciso"` no es verificable: cada ejecución produce una
extensión y un orden distintos. Reemplazo esa indicación por secciones fijas con
límite de viñetas. La estructura garantiza que los plazos no se pierdan entre la
descripción de coberturas, y hace comparables dos ejecuciones distintas.

### 1.4 Preservar cifras y plazos textualmente

Es la regla crítica. El texto contiene datos que no admiten paráfrasis:
`$98.500.000`, `30 días calendario`, `10 días hábiles`, `50%`, `24:00`,
`Circular Externa 028 de 2019`. Un modelo que resume tiende a redondear
("aproximadamente 30 días") o a unificar unidades distintas. El documento usa
días calendario en un caso y días hábiles en otro, y confundirlos cambia el
plazo real.

Instruyo copiar cifras, plazos y referencias normativas **literalmente**, y
conservar la distinción entre calendario y hábiles.

### 1.5 Prohibir la información no presente

El dominio asegurador es denso en conocimiento previo, y el modelo puede
completar con lo que "suele" decir una póliza. Añado una restricción explícita:
si un dato no está en el documento, se omite; no se infiere ni se complementa.

### 1.6 Cerrar la salida

Los modelos tienden a envolver la respuesta ("Claro, aquí tienes el resumen…")
o a añadir recomendaciones al final. Prohíbo el preámbulo, el cierre y todo
comentario sobre la tarea: la respuesta empieza en la primera sección.

---

## 2. ¿Cómo evita respuestas irrelevantes?

| Mecanismo | Desvío que previene |
|---|---|
| Delimitación con `<documento>` | Que una frase del texto se lea como instrucción |
| Rol y destinatario definidos | Registro arbitrario y criterio de relevancia inestable |
| Secciones fijas y tope de viñetas | Extensión variable; omitir plazos por falta de espacio |
| Cifras y plazos literales | Redondeos y confusión entre días calendario y hábiles |
| Prohibición de datos externos | Añadir exclusiones o condiciones típicas que no están |
| Prohibición de preámbulo y cierre | Texto de cortesía y recomendaciones no solicitadas |
| Regla de vacío explícita | Rellenar una sección sin respaldo en el documento |
| Marcadores de anonimización preservados (§5) | Sustituir un identificador enmascarado por un nombre inventado |

El principio es que cada restricción cierra una vía de desvío concreta y
observable, no que el prompt sea más largo.

---

## 3. Ejemplo comparativo

### 3.1 Prompt original

```text
Resume el siguiente texto: [En caso de revocación de la póliza o modificaciones
de cualquiera de las condiciones generales o particulares del seguro por parte
de la Aseguradora, Tomador o Asegurado, Seguros Sura se compromete a dar aviso a
BANCO, por escrito y con una antelación no menor a 30 días ... (texto completo)].
Devuelve solo un resumen corto y preciso.
```

**Debilidades:** sin frontera entre instrucción y documento; "corto y preciso" no
es medible; nada protege las cifras; nada impide añadir conocimiento externo;
nada define el destinatario.

### 3.2 Prompt optimizado

```text
Eres analista de seguros. Resumes comunicaciones de pólizas para un ejecutivo
bancario que necesita conocer las obligaciones del banco y las coberturas
vigentes.

Resume el documento delimitado por <documento></documento> siguiendo exactamente
esta estructura:

**Avisos y plazos**
- Máximo 4 viñetas. Cada una indica quién avisa a quién, en qué situación y con
  qué plazo exacto.

**Cobertura**
- Máximo 4 viñetas: valor asegurado, riesgos cubiertos, condiciones de
  incapacidad e inicio de vigencia.

**Condiciones administrativas**
- Máximo 3 viñetas: forma de pago, cesión, exclusiones y marco normativo.

Reglas:
1. Copia cifras, porcentajes, plazos y referencias normativas exactamente como
   aparecen. No redondees ni conviertas unidades.
2. Conserva la distinción entre "días calendario" y "días hábiles".
3. No incluyas información que no esté en el documento. No infieras ni
   complementes con conocimiento general sobre seguros.
4. Si una sección no tiene respaldo en el documento, escribe "No especificado".
5. Responde solo con las tres secciones. Sin preámbulo, sin cierre, sin
   comentarios sobre la tarea.
6. Máximo 200 palabras en total.

<documento>
[texto íntegro de la comunicación]
</documento>
```

### 3.3 Salidas contrastadas

**Con el prompt original** (patrón típico observado en modelos de propósito general):

```text
Claro, aquí tienes un resumen:

La póliza de Seguros Sura cubre el fallecimiento del asegurado por
aproximadamente $98 millones e incluye incapacidad total y permanente. La
aseguradora debe avisar al banco con un mes de anticipación ante cambios o
revocación. El pago es anual y existen exclusiones en el clausulado.

Espero que sea útil. ¿Necesitas que amplíe algún punto?
```

Problemas concretos: `$98.500.000` degradado a "aproximadamente $98 millones";
"30 días" convertido en "un mes"; se pierden el plazo de 10 días hábiles, el
umbral del 50% de capacidad laboral, la hora 24:00 de inicio de vigencia y la
Circular Externa 028 de 2019; se añaden preámbulo y cierre conversacional.

**Con el prompt optimizado:**

```text
**Avisos y plazos**
- Seguros Sura avisa a BANCO por escrito, con no menos de 30 días de antelación,
  ante revocación o modificación de condiciones generales o particulares.
- El asegurado o beneficiario avisa el siniestro a Seguros Sura dentro de los 30
  días calendario siguientes a conocerlo.
- Seguros Sura informa el siniestro a BANCO dentro de los 10 días hábiles.
- En terminación automática por mora, se informa al beneficiario oneroso con
  máximo 30 días de antelación, con cobertura garantizada durante ese periodo.

**Cobertura**
- Valor asegurado de $98.500.000 por fallecimiento.
- Muerte por cualquier causa desde el primer momento: homicidio, suicidio,
  terrorismo, embriaguez, secuestro, atraco, presunción de muerte por
  desaparecimiento declarado judicialmente, epidemia, pandemia o SIDA no
  preexistente a la contratación.
- Incapacidad total y permanente por enfermedad o accidente desde el inicio de
  vigencia: pérdida permanente del 50% o más de la capacidad laboral.
- Vigencia desde las 24:00 del día de expedición que figura en la carátula.

**Condiciones administrativas**
- Pago anual por COBRO BANCARIO.
- Cedible ante titularización de cartera, con notificación de la cesión.
- Exclusiones generales en el clausulado; las particulares, en la carátula.
- Circular Externa 028 de 2019 (Superintendencia Financiera): la entidad
  financiera puede pagar la prima para evitar la terminación automática.
```

Los cuatro plazos quedan íntegros y diferenciados, las cifras conservan su
precisión y no hay texto de relleno.

---

## 4. Resumen de las mejoras

| # | Mejora | Efecto |
|---|---|---|
| 1 | Documento delimitado con etiquetas | Separa instrucción de dato |
| 2 | Rol y destinatario explícitos | Fija el criterio de relevancia |
| 3 | Estructura de salida con topes | Salida estable y comparable |
| 4 | Cifras y plazos literales | Elimina redondeos y errores de unidad |
| 5 | Prohibición de datos externos | Evita completar con conocimiento previo |
| 6 | Sin preámbulo ni cierre | Respuesta directamente utilizable |
| 7 | Regla de "No especificado" | Evita rellenar secciones sin respaldo |
| 8 | Anonimización previa al envío | Protege los datos personales sin sacrificar las cifras contractuales |

---

## 5. Tratamiento de datos personales

El enunciado pide precisión y relevancia; esta sección responde a una condición
previa a ambas. El texto a resumir es una comunicación real de una aseguradora
sobre la póliza de una persona concreta, y enviarlo a un modelo de terceros
constituye un tratamiento de datos personales. En Colombia eso queda cubierto
por la Ley 1581 de 2012, y tratándose de un producto asociado a un crédito
bancario, por el régimen de la Superintendencia Financiera, incluida la misma
Circular Externa 028 de 2019 que el documento cita.

### 5.1 Qué se enmascara y qué se conserva

La decisión importante no es *si* anonimizar, sino *qué*. Enmascarar sin
criterio destruiría el valor del resumen: si `$98.500.000` se convierte en
`[VALOR_ASEGURADO]`, la salida deja de servirle al ejecutivo bancario, y se
pierde justamente la precisión que persigue la mejora 1.4.

El corte va entre **identificadores directos**, que señalan a una persona o
entidad concreta, y **datos contractuales**, que describen el producto y no
identifican a nadie por sí solos.

| Elemento | Tratamiento | Razón |
|---|---|---|
| Nombre del asegurado o beneficiario | Enmascarar | Identificador directo |
| Documento de identidad | Enmascarar | Identificador directo |
| Número de póliza o de crédito | Enmascarar | Identifica de forma indirecta al titular |
| Datos de contacto | Enmascarar | Identificador directo |
| Nombre de la aseguradora y del banco | Enmascarar según el caso | Persona jurídica: no es dato personal, pero sí información comercial sensible |
| Valor asegurado | **Conservar** | Dato contractual; el resumen carece de utilidad sin él |
| Plazos, porcentajes y fechas | **Conservar** | Sustancia de las obligaciones que el resumen debe transmitir |
| Coberturas y exclusiones | **Conservar** | Descripción del producto, no de la persona |
| Referencias normativas | **Conservar** | Información pública |

Un valor asegurado por sí solo no identifica a nadie. Combinado con nombre y
número de póliza, sí. Por eso el corte se hace sobre los identificadores y no
sobre las cifras.

### 5.2 Dónde ocurre: antes del modelo, no dentro del prompt

La anonimización es un **preprocesamiento determinista**, no una instrucción
dentro del prompt. Pedirle al modelo "no menciones datos personales" no protege
nada: para cuando el modelo lee esa instrucción, el texto sin enmascarar ya
viajó al proveedor. La fuga ocurre en el envío, no en la respuesta.

```
Documento original
      │
      ▼
[1] Detección y tokenización     ← determinista (regex + catálogo de entidades)
      │   Guarda el mapa token → valor original, en memoria del proceso
      ▼
Documento enmascarado  ──►  [2] Modelo (prompt de la sección 3)
                                   │
                                   ▼
                            Resumen con tokens
                                   │
      ┌────────────────────────────┘
      ▼
[3] Restitución de tokens        ← determinista, con el mapa del paso 1
      │
      ▼
Resumen final, con los datos reales
```

El mapa de sustitución nunca sale del proceso, y el proveedor del modelo solo ve
la versión enmascarada. Los tokens son reversibles y estables dentro de una misma
ejecución: si el asegurado aparece cinco veces, las cinco reciben el mismo token,
de modo que el modelo conserva la noción de que se trata de la misma persona.

### 5.3 Reglas que añade al prompt

El preprocesamiento requiere dos instrucciones adicionales, que se suman a las
seis de la sección 3.2:

```text
7. El documento contiene marcadores con el formato [TIPO_N] (por ejemplo
   [ASEGURADO_1]). Repítelos exactamente como aparecen. No los sustituyas por
   nombres, no los interpretes y no los omitas.
8. No intentes deducir la identidad de las personas o entidades enmascaradas a
   partir del contexto.
```

Sin la regla 7, un modelo tiende a "resolver" el marcador, reemplazándolo por un
nombre genérico como "el asegurado", y entonces la restitución del paso 3 no
encuentra qué reponer.

### 5.4 Formato de los tokens

El formato `[TIPO_N]` no es arbitrario. Los corchetes lo separan visiblemente del
texto corrido, el tipo le da al modelo el contexto gramatical que necesita para
redactar con naturalidad, y el índice distingue entidades del mismo tipo. Un
token opaco como `XK7F2` haría que el modelo lo tratara como ruido o intentara
interpretarlo.

### 5.5 Versión final del prompt

Las secciones anteriores presentan las piezas por separado. Esta es la versión
completa lista para usar: el prompt de la sección 3.2 con las dos reglas de
anonimización integradas. Es la que se emplea en el paso [2] del pipeline, y
recibe el documento ya enmascarado.

```text
Eres analista de seguros. Resumes comunicaciones de pólizas para un ejecutivo
bancario que necesita conocer las obligaciones del banco y las coberturas
vigentes.

Resume el documento delimitado por <documento></documento> siguiendo exactamente
esta estructura:

**Avisos y plazos**
- Máximo 4 viñetas. Cada una indica quién avisa a quién, en qué situación y con
  qué plazo exacto.

**Cobertura**
- Máximo 4 viñetas: valor asegurado, riesgos cubiertos, condiciones de
  incapacidad e inicio de vigencia.

**Condiciones administrativas**
- Máximo 3 viñetas: forma de pago, cesión, exclusiones y marco normativo.

Reglas:
1. Copia cifras, porcentajes, plazos y referencias normativas exactamente como
   aparecen. No redondees ni conviertas unidades.
2. Conserva la distinción entre "días calendario" y "días hábiles".
3. No incluyas información que no esté en el documento. No infieras ni
   complementes con conocimiento general sobre seguros.
4. Si una sección no tiene respaldo en el documento, escribe "No especificado".
5. Responde solo con las tres secciones. Sin preámbulo, sin cierre, sin
   comentarios sobre la tarea.
6. Máximo 200 palabras en total.
7. El documento contiene marcadores con el formato [TIPO_N] (por ejemplo
   [ASEGURADO_1]). Repítelos exactamente como aparecen. No los sustituyas por
   nombres, no los interpretes y no los omitas.
8. No intentes deducir la identidad de las personas o entidades enmascaradas a
   partir del contexto.

<documento>
[texto enmascarado de la comunicación]
</documento>
```

El orden de las reglas no es casual: las de fidelidad al contenido (1 y 2) van
primero porque son las que más pesan sobre la calidad del resumen, y las de
anonimización (7 y 8) van al final porque operan sobre la forma, no sobre qué
información incluir.

### 5.6 Límites de este enfoque

Conviene ser explícito sobre lo que el pipeline no resuelve:

- **La detección no es perfecta.** Un nombre escrito de forma inusual puede
  escapar al reconocimiento de entidades. Por eso el enmascaramiento se apoya en
  un catálogo de entidades conocidas y no solo en detección automática.
- **La reidentificación por combinación sigue siendo posible.** Un valor
  asegurado poco común más una fecha de expedición pueden señalar a una persona
  dentro de una cartera pequeña. Para cargas de alto riesgo, la mitigación real
  es un modelo desplegado en infraestructura propia, no la anonimización.
- **Enmascarar tiene un costo de calidad.** Cuantos más marcadores, menos natural
  la redacción. De ahí que el corte se limite a los identificadores directos.

---

## 6. Nota sobre verificación

Para uso en producción, estas instrucciones se acompañan de una verificación
automática: extraer del documento fuente las cifras, porcentajes y plazos
mediante expresiones regulares y confirmar que cada uno aparece literalmente en
el resumen. Es una comprobación determinista que detecta la falla más costosa
(la alteración de un dato contractual) sin depender de otro modelo.

La misma verificación cubre el pipeline de la sección 5: confirma que cada
marcador introducido en el paso de tokenización aparece en el resumen y que
ningún identificador original sobrevivió al enmascaramiento. Ambas
comprobaciones son exactas y no requieren juicio.
