-- Licensed to the .NET Foundation under one or more agreements.
-- The .NET Foundation licenses this file to you under the MIT license.
-- See the LICENSE file in the project root for more information.

CREATE PROCEDURE [dbo].[Routine_Insert]
    @name nvarchar(256)
AS
BEGIN
BEGIN TRANSACTION
    DECLARE @RID as int
    SELECT @RID = [R].[Id]
    FROM [Routine] AS [R] 
    WHERE [R].[Name] = @name 

    IF @RID IS NULL -- if the method wasn't found create it
    BEGIN
        INSERT INTO Routine([Name]) 
        VALUES ( @name )
        SELECT @RID = SCOPE_IDENTITY()
    END
COMMIT TRANSACTION
SELECT @RID, @name
END