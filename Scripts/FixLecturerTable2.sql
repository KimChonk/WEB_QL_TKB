-- Try to add UserId column to Lecturers table if it doesn't exist
BEGIN TRY
    -- Add UserId column to Lecturers table
    ALTER TABLE Lecturers ADD UserId nvarchar(450) NULL;
    PRINT 'UserId column added to Lecturers table.';
END TRY
BEGIN CATCH
    -- Column might already exist - that's fine
    PRINT 'UserId column already exists or another error occurred.';
END CATCH

-- Try to create the index
BEGIN TRY
    CREATE INDEX IX_Lecturers_UserId ON Lecturers(UserId);
    PRINT 'Index created for UserId column.';
END TRY
BEGIN CATCH
    PRINT 'Index already exists or another error occurred.';
END CATCH

-- Try to create foreign key constraint
BEGIN TRY
    ALTER TABLE Lecturers ADD CONSTRAINT FK_Lecturers_AspNetUsers_UserId
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL;
    PRINT 'Foreign key constraint added.';
END TRY
BEGIN CATCH
    PRINT 'Foreign key constraint already exists or another error occurred.';
END CATCH

-- Link lecturers with users where possible (by matching email addresses)
UPDATE l SET l.UserId = u.Id
FROM Lecturers l JOIN AspNetUsers u ON l.Email = u.Email
WHERE l.UserId IS NULL AND u.Role = 'Lecturer';

PRINT 'Lecturers linked with corresponding user accounts based on email.'; 