-- Check if UserId column exists in Lecturers table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'UserId' AND object_id = OBJECT_ID('Lecturers'))
BEGIN
    -- Add UserId column to Lecturers table
    ALTER TABLE Lecturers ADD UserId nvarchar(450) NULL;

    -- Create index for the foreign key
    CREATE INDEX IX_Lecturers_UserId ON Lecturers(UserId);
    
    PRINT 'UserId column added to Lecturers table.';
END
ELSE
BEGIN
    PRINT 'UserId column already exists in Lecturers table.';
END

-- Check if foreign key constraint exists
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Lecturers_AspNetUsers_UserId')
BEGIN
    -- Add foreign key from Lecturers to AspNetUsers
    ALTER TABLE Lecturers ADD CONSTRAINT FK_Lecturers_AspNetUsers_UserId
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
    
    PRINT 'Foreign key constraint added between Lecturers and AspNetUsers.';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint already exists between Lecturers and AspNetUsers.';
END

-- Link lecturers with users where possible (by matching email addresses)
UPDATE l SET l.UserId = u.Id
FROM Lecturers l JOIN AspNetUsers u ON l.Email = u.Email
WHERE l.UserId IS NULL AND u.Role = 'Lecturer';

PRINT 'Lecturers linked with corresponding user accounts based on email.';

-- Show schema information
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Lecturers'; 