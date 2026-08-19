namespace InvoiceApi.Models;

/// <summary>
/// Forma en que se compara el nombre de cliente en la busqueda de facturas.
/// </summary>
/// <remarks>
/// Los tres modos difieren en coste. Los dos primeros se resuelven con el indice
/// de <c>ClientName</c>; el tercero obliga a recorrer la tabla, porque un patron
/// que empieza por comodin no permite al motor acotar el rango de busqueda. De
/// ahi que el modo exacto sea el predeterminado y el de contenido deba pedirse
/// de forma explicita.
/// </remarks>
public enum ClientMatchMode
{
    /// <summary>
    /// El nombre debe coincidir por completo. Se resuelve con una busqueda puntual
    /// en el indice y es el modo mas eficiente.
    /// </summary>
    Exact = 0,

    /// <summary>
    /// Coinciden los nombres que empiezan por el texto indicado: "Seguros"
    /// encuentra "Seguros Sura". Se resuelve como busqueda por rango en el indice.
    /// </summary>
    Prefix = 1,

    /// <summary>
    /// Coinciden los nombres que contienen el texto en cualquier posicion: "Sura"
    /// encuentra "Seguros Sura". Util cuando la parte distintiva del nombre no va
    /// al principio, algo habitual en razones sociales. Recorre la tabla completa,
    /// por lo que su coste crece con el volumen almacenado.
    /// </summary>
    Contains = 2
}
