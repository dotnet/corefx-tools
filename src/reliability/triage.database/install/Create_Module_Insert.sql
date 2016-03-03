CREATE PROCEDURE [dbo].[Module_Insert]
    @name nvarchar(256)
AS
BEGIN
    DECLARE @MID as int 
    SELECT @MID = [M].[Id]
    FROM [Modules] AS [M] 
    WHERE [M].[Name] = @name 

    IF @MID IS NULL -- if the module doesn't exist create it
    BEGIN TRY
        INSERT INTO [Modules] ([Name]) 
        VALUES ( @name )
        SELECT @MID = SCOPE_IDENTITY()
    END TRY
	BEGIN CATCH    
		SELECT @MID = [M].[Id]
		FROM [Modules] AS [M] 
		WHERE [M].[Name] = @name 
	END CATCH
SELECT @MID, @name
END