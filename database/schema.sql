-- Esquema de la API de facturas: tabla, indice de busqueda y los procedimientos
-- almacenados que resuelven las tres operaciones. No se usa Entity Framework.

IF DB_ID(N'InvoiceDb') IS NULL
BEGIN
    CREATE DATABASE [InvoiceDb];
END
GO

USE [InvoiceDb];
GO

IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Invoices]
    (
        [Id]         INT             IDENTITY(1,1) NOT NULL,
        [ClientName] NVARCHAR(100)   NOT NULL,
        [Amount]     DECIMAL(18,2)   NOT NULL,
        [IssueDate]  DATETIME2(0)    NOT NULL,
        [Status]     VARCHAR(20)     NOT NULL,
        [CreatedAt]  DATETIME2(0)    NOT NULL CONSTRAINT [DF_Invoices_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Invoices] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [CK_Invoices_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Invoices_Status] CHECK ([Status] IN ('PENDING', 'PAID', 'CANCELLED'))
    );
END
GO

-- Indice que cubre por completo la busqueda por cliente: la clave resuelve el
-- filtro, IssueDate DESC ahorra el ordenamiento y las columnas incluidas evitan
-- tener que volver a la tabla por cada fila.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_ClientName' AND object_id = OBJECT_ID(N'[dbo].[Invoices]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Invoices_ClientName]
        ON [dbo].[Invoices] ([ClientName] ASC, [IssueDate] DESC)
        INCLUDE ([Amount], [Status], [CreatedAt]);
END
GO

-- Devuelve la fila creada para que la API no tenga que releerla despues.
CREATE OR ALTER PROCEDURE [dbo].[SP_InsertInvoice]
    @ClientName NVARCHAR(100),
    @Amount     DECIMAL(18,2),
    @IssueDate  DATETIME2(0),
    @Status     VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Inserted TABLE
    (
        [Id]         INT,
        [ClientName] NVARCHAR(100),
        [Amount]     DECIMAL(18,2),
        [IssueDate]  DATETIME2(0),
        [Status]     VARCHAR(20),
        [CreatedAt]  DATETIME2(0)
    );

    INSERT INTO [dbo].[Invoices] ([ClientName], [Amount], [IssueDate], [Status])
    OUTPUT
        INSERTED.[Id],
        INSERTED.[ClientName],
        INSERTED.[Amount],
        INSERTED.[IssueDate],
        INSERTED.[Status],
        INSERTED.[CreatedAt]
    INTO @Inserted
    VALUES (@ClientName, @Amount, @IssueDate, @Status);

    SELECT [Id], [ClientName], [Amount], [IssueDate], [Status], [CreatedAt]
    FROM @Inserted;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SP_GetInvoiceById]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Id], [ClientName], [Amount], [IssueDate], [Status], [CreatedAt]
    FROM [dbo].[Invoices]
    WHERE [Id] = @Id;
END
GO

-- Devuelve dos conjuntos de resultados en una sola llamada: primero el total de
-- coincidencias y despues la pagina pedida.
CREATE OR ALTER PROCEDURE [dbo].[SP_SearchInvoicesByClient]
    @ClientName NVARCHAR(100),
    @Page       INT = 1,
    @PageSize   INT = 50,
    @MatchMode  TINYINT = 0   -- 0 = exacto, 1 = prefijo, 2 = contiene
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 1;
    IF @PageSize > 200 SET @PageSize = 200;
    IF @MatchMode NOT IN (0, 1, 2) SET @MatchMode = 0;

    -- Los caracteres con significado especial en LIKE se escapan, de modo que un
    -- valor como '%' o '_' se busque literalmente y no como comodin.
    DECLARE @Escapado NVARCHAR(200) =
        REPLACE(REPLACE(REPLACE(@ClientName, N'\', N'\\'), N'%', N'\%'), N'_', N'\_');

    -- El patron depende del modo:
    --   exacto   -> igualdad, resuelta con una busqueda puntual en el indice.
    --   prefijo  -> 'texto%', resuelta como busqueda por rango en el indice.
    --   contiene -> '%texto%'. El comodin inicial impide usar el indice y obliga a
    --               recorrer la tabla. Se ofrece porque hay busquedas legitimas que
    --               lo necesitan (el nombre distintivo de una empresa no siempre va
    --               al principio), pero no es el modo por defecto y su coste crece
    --               con el volumen.
    DECLARE @Patron NVARCHAR(202) =
        CASE @MatchMode
            WHEN 1 THEN @Escapado + N'%'
            WHEN 2 THEN N'%' + @Escapado + N'%'
        END;

    SELECT COUNT_BIG(1) AS [TotalCount]
    FROM [dbo].[Invoices]
    WHERE (@MatchMode = 0 AND [ClientName] = @ClientName)
       OR (@MatchMode > 0 AND [ClientName] LIKE @Patron ESCAPE N'\');

    SELECT [Id], [ClientName], [Amount], [IssueDate], [Status], [CreatedAt]
    FROM [dbo].[Invoices]
    WHERE (@MatchMode = 0 AND [ClientName] = @ClientName)
       OR (@MatchMode > 0 AND [ClientName] LIKE @Patron ESCAPE N'\')
    ORDER BY [IssueDate] DESC, [Id] DESC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);  -- un plan por modo, en vez de uno compartido suboptimo
END
GO
