-- Fix Timetables ClassID not null constraint
BEGIN TRANSACTION;

-- 1. Create a default class if needed
IF NOT EXISTS (SELECT 1 FROM [Classes] WHERE [ClassName] = 'Default Class')
BEGIN
    INSERT INTO [Classes] ([ClassName], [ClassSize]) VALUES ('Default Class', 40);
    PRINT 'Created Default Class';
END

-- Get the default class ID to use for null ClassID values
DECLARE @defaultClassId INT;
SELECT @defaultClassId = [ClassID] FROM [Classes] WHERE [ClassName] = 'Default Class';
PRINT 'Using Default Class ID: ' + CAST(@defaultClassId AS NVARCHAR);

-- 2. Update any null ClassID values
UPDATE [Timetables] 
SET [ClassID] = @defaultClassId,
    [ClassIDInt] = @defaultClassId
WHERE [ClassID] IS NULL;
PRINT 'Updated NULL ClassID values to use Default Class';

-- 3. Check if constraint exists before dropping 
IF EXISTS (
    SELECT * FROM sys.foreign_keys 
    WHERE name = 'FK_Timetables_Classes_ClassID' AND parent_object_id = OBJECT_ID('Timetables')
)
BEGIN
    ALTER TABLE [Timetables] DROP CONSTRAINT [FK_Timetables_Classes_ClassID];
    PRINT 'Dropped existing foreign key constraint';
END

-- 4. Drop index if it exists
IF EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'IX_Timetables_ClassID' AND object_id = OBJECT_ID('Timetables')
)
BEGIN
    DROP INDEX [IX_Timetables_ClassID] ON [Timetables];
    PRINT 'Dropped index on ClassID';
END

-- 5. Update column to NOT NULL
DECLARE @DefaultConstraintName NVARCHAR(200);
SELECT @DefaultConstraintName = Name
FROM sys.default_constraints 
WHERE parent_object_id = OBJECT_ID('Timetables')
AND parent_column_id = (
    SELECT column_id 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('Timetables')
    AND name = 'ClassID'
);

IF @DefaultConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [Timetables] DROP CONSTRAINT [' + @DefaultConstraintName + ']');
    PRINT 'Dropped default constraint on ClassID';
END

-- 6. Alter the column to be not null
ALTER TABLE [Timetables] ALTER COLUMN [ClassID] INT NOT NULL;
PRINT 'Modified ClassID to be NOT NULL';

-- 7. Add default constraint
ALTER TABLE [Timetables] ADD CONSTRAINT DF_Timetables_ClassID DEFAULT 0 FOR [ClassID];
PRINT 'Added default constraint for ClassID';

-- 8. Recreate the index
CREATE INDEX [IX_Timetables_ClassID] ON [Timetables] ([ClassID]);
PRINT 'Recreated index on ClassID';

-- 9. Add the foreign key constraint
ALTER TABLE [Timetables] ADD CONSTRAINT [FK_Timetables_Classes_ClassID] 
FOREIGN KEY ([ClassID]) REFERENCES [Classes] ([ClassID]) ON DELETE CASCADE;
PRINT 'Added foreign key constraint';

-- 10. Update the migration history
IF EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250330072558_UpdateTimetableClassIDNonNullable')
BEGIN
    PRINT 'Migration already in history table';
END
ELSE
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250330072558_UpdateTimetableClassIDNonNullable', '9.0.3');
    PRINT 'Added migration to history table';
END

COMMIT;
PRINT 'Database update completed successfully'; 