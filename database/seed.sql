-- Datos de ejemplo para poder probar la API sin crearlos a mano. Va aparte del
-- esquema a proposito: schema.sql debe poder aplicarse sobre una base con datos
-- reales, y esto solo tiene sentido en local. Solo inserta si la tabla esta
-- vacia, asi que se puede ejecutar las veces que haga falta.
--
-- Los nombres imitan al sector asegurador colombiano, donde varias companias
-- comparten el prefijo "Seguros" y lo que las distingue va al final. Es
-- justamente el caso que hace util la busqueda por contenido.
--
-- Bancolombia concentra mas facturas que el tamano de pagina por defecto, de
-- modo que la paginacion se aprecia sin tener que forzar el parametro, mientras
-- que Davivienda se queda algo por debajo y cabe en una sola pagina.

USE [InvoiceDb];
GO

SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM [dbo].[Invoices])
BEGIN
    PRINT 'La tabla Invoices ya contiene datos. No se inserta nada.';
END
ELSE
BEGIN
    INSERT INTO [dbo].[Invoices] ([ClientName], [Amount], [IssueDate], [Status])
    VALUES
        (N'Seguros Sura', 98500000.00, '2026-08-18', 'PENDING'),
        (N'Seguros Sura', 4750000.00, '2026-07-30', 'PAID'),
        (N'Seguros Sura', 12300000.50, '2026-06-15', 'PAID'),
        (N'Seguros Sura', 890000.00, '2026-05-02', 'CANCELLED'),
        (N'Seguros Bolivar', 31200000.00, '2026-08-10', 'PENDING'),
        (N'Seguros Bolivar', 6540000.75, '2026-07-01', 'PAID'),
        (N'Seguros del Estado', 8900000.00, '2026-08-05', 'PAID'),
        (N'Davivienda S.A.', 18750000.00, '2026-07-22', 'PAID'),
        (N'Grupo Exito', 5600000.00, '2026-06-30', 'CANCELLED'),
        (N'Ecopetrol S.A.', 120000000.00, '2026-08-01', 'PENDING'),
        (N'Bancolombia S.A.', 14673899.50, '2026-12-01', 'PENDING'),
        (N'Bancolombia S.A.', 6915585.45, '2026-12-08', 'PAID'),
        (N'Bancolombia S.A.', 29344411.12, '2026-12-15', 'PAID'),
        (N'Bancolombia S.A.', 3398767.46, '2026-12-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 24184307.89, '2026-12-01', 'PENDING'),
        (N'Bancolombia S.A.', 16551147.92, '2026-11-08', 'PAID'),
        (N'Bancolombia S.A.', 2751251.78, '2026-11-15', 'PAID'),
        (N'Bancolombia S.A.', 22908492.63, '2026-11-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 1831680.28, '2026-11-01', 'PENDING'),
        (N'Bancolombia S.A.', 19599008.91, '2026-11-08', 'PAID'),
        (N'Bancolombia S.A.', 3283015.75, '2026-10-15', 'PAID'),
        (N'Bancolombia S.A.', 4218478.65, '2026-10-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 19189685.63, '2026-10-01', 'PENDING'),
        (N'Bancolombia S.A.', 37234317.79, '2026-10-08', 'PAID'),
        (N'Bancolombia S.A.', 5702517.96, '2026-10-15', 'PAID'),
        (N'Bancolombia S.A.', 10162267.56, '2026-09-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 28290380.02, '2026-09-01', 'PENDING'),
        (N'Bancolombia S.A.', 42654746.07, '2026-09-08', 'PAID'),
        (N'Bancolombia S.A.', 26033067.25, '2026-09-15', 'PAID'),
        (N'Bancolombia S.A.', 17941119.29, '2026-09-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 43935041.49, '2026-08-01', 'PENDING'),
        (N'Bancolombia S.A.', 2239233.23, '2026-08-08', 'PAID'),
        (N'Bancolombia S.A.', 38652310.39, '2026-08-15', 'PAID'),
        (N'Bancolombia S.A.', 13138976.49, '2026-08-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 6619840.49, '2026-08-01', 'PENDING'),
        (N'Bancolombia S.A.', 5432981.88, '2026-07-08', 'PAID'),
        (N'Bancolombia S.A.', 13985409.81, '2026-07-15', 'PAID'),
        (N'Bancolombia S.A.', 36753267.21, '2026-07-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 8255578.14, '2026-07-01', 'PENDING'),
        (N'Bancolombia S.A.', 26234767.34, '2026-07-08', 'PAID'),
        (N'Bancolombia S.A.', 28805269.08, '2025-06-15', 'PAID'),
        (N'Bancolombia S.A.', 16852029.79, '2025-06-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 24716339.29, '2025-06-01', 'PENDING'),
        (N'Bancolombia S.A.', 2966085.53, '2025-06-08', 'PAID'),
        (N'Bancolombia S.A.', 2823112.47, '2025-06-15', 'PAID'),
        (N'Bancolombia S.A.', 9387248.27, '2025-05-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 30665938.80, '2025-05-01', 'PENDING'),
        (N'Bancolombia S.A.', 19327514.91, '2025-05-08', 'PAID'),
        (N'Bancolombia S.A.', 14239500.59, '2025-05-15', 'PAID'),
        (N'Bancolombia S.A.', 26412449.58, '2025-05-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 20475319.28, '2025-04-01', 'PENDING'),
        (N'Bancolombia S.A.', 13594549.81, '2025-04-08', 'PAID'),
        (N'Bancolombia S.A.', 35777919.75, '2025-04-15', 'PAID'),
        (N'Bancolombia S.A.', 31499900.35, '2025-04-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 11097728.51, '2025-04-01', 'PENDING'),
        (N'Bancolombia S.A.', 25912903.41, '2025-03-08', 'PAID'),
        (N'Bancolombia S.A.', 23705063.20, '2025-03-15', 'PAID'),
        (N'Bancolombia S.A.', 39399916.68, '2025-03-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 32865621.23, '2025-03-01', 'PENDING'),
        (N'Bancolombia S.A.', 13064008.76, '2025-03-08', 'PAID'),
        (N'Bancolombia S.A.', 44110841.91, '2025-02-15', 'PAID'),
        (N'Bancolombia S.A.', 5445250.15, '2025-02-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 18902808.56, '2025-02-01', 'PENDING'),
        (N'Bancolombia S.A.', 34107770.69, '2025-02-08', 'PAID'),
        (N'Bancolombia S.A.', 6966506.38, '2025-02-15', 'PAID'),
        (N'Bancolombia S.A.', 22079995.06, '2025-01-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 1908445.48, '2025-01-01', 'PENDING'),
        (N'Bancolombia S.A.', 30119481.17, '2025-01-08', 'PAID'),
        (N'Bancolombia S.A.', 34441003.35, '2025-01-15', 'PAID'),
        (N'Bancolombia S.A.', 25850213.42, '2025-01-22', 'CANCELLED'),
        (N'Bancolombia S.A.', 39415179.86, '2025-12-01', 'PENDING'),
        (N'Bancolombia S.A.', 14221575.95, '2025-12-08', 'PAID'),
        (N'Davivienda S.A.', 15323921.48, '2026-12-01', 'PENDING'),
        (N'Davivienda S.A.', 13112644.01, '2026-12-08', 'PAID'),
        (N'Davivienda S.A.', 12795503.93, '2026-12-15', 'PAID'),
        (N'Davivienda S.A.', 10085458.81, '2026-12-22', 'CANCELLED'),
        (N'Davivienda S.A.', 18493694.07, '2026-12-01', 'PENDING'),
        (N'Davivienda S.A.', 20787962.79, '2026-11-08', 'PAID'),
        (N'Davivienda S.A.', 10477494.57, '2026-11-15', 'PAID'),
        (N'Davivienda S.A.', 14641574.82, '2026-11-22', 'CANCELLED'),
        (N'Davivienda S.A.', 1419267.16, '2026-11-01', 'PENDING'),
        (N'Davivienda S.A.', 15459690.19, '2026-11-08', 'PAID'),
        (N'Davivienda S.A.', 14268593.20, '2026-10-15', 'PAID'),
        (N'Davivienda S.A.', 21848732.03, '2026-10-22', 'CANCELLED'),
        (N'Davivienda S.A.', 18098372.07, '2026-10-01', 'PENDING'),
        (N'Davivienda S.A.', 6325488.11, '2026-10-08', 'PAID'),
        (N'Davivienda S.A.', 8542690.50, '2026-10-15', 'PAID'),
        (N'Davivienda S.A.', 14740181.01, '2026-09-22', 'CANCELLED'),
        (N'Davivienda S.A.', 584353.75, '2026-09-01', 'PENDING'),
        (N'Davivienda S.A.', 10205743.72, '2026-09-08', 'PAID'),
        (N'Davivienda S.A.', 3771939.98, '2026-09-15', 'PAID'),
        (N'Davivienda S.A.', 2655568.86, '2026-09-22', 'CANCELLED'),
        (N'Davivienda S.A.', 1381691.33, '2026-08-01', 'PENDING'),
        (N'Davivienda S.A.', 16921984.78, '2026-08-08', 'PAID'),
        (N'Davivienda S.A.', 2923844.26, '2026-08-15', 'PAID'),
        (N'Davivienda S.A.', 5515241.01, '2026-08-22', 'CANCELLED'),
        (N'Davivienda S.A.', 8655708.00, '2026-08-01', 'PENDING'),
        (N'Davivienda S.A.', 19182855.45, '2026-07-08', 'PAID'),
        (N'Davivienda S.A.', 1855536.31, '2026-07-15', 'PAID'),
        (N'Davivienda S.A.', 9931695.95, '2026-07-22', 'CANCELLED');

    -- El recuento se toma de la propia insercion, para que el mensaje no dependa
    -- de un numero escrito a mano que quede desfasado al editar la lista.
    PRINT CONCAT('Insertadas ', @@ROWCOUNT, ' facturas de ejemplo.');
END
GO
