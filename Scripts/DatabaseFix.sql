-- Check if UserId column exists in Lecturers table
IF NOT EXISTS (SELECT 1 FROM sys.columns 
              WHERE name = 'UserId' AND object_id = OBJECT_ID('Lecturers'))
BEGIN
    -- Add UserId column to Lecturers table
    ALTER TABLE Lecturers
    ADD UserId nvarchar(450) NULL;

    -- Create index for the foreign key
    CREATE INDEX IX_Lecturers_UserId ON Lecturers(UserId);
END

-- Check if ApplicationUserId column already exists
-- If not, create it in the Timetables table
IF NOT EXISTS (SELECT 1 FROM sys.columns 
              WHERE name = 'ApplicationUserId' AND object_id = OBJECT_ID('Timetables'))
BEGIN
    -- Add ApplicationUserId column to Timetables table
    ALTER TABLE Timetables
    ADD ApplicationUserId nvarchar(450) NULL;

    -- Create index for the foreign key
    CREATE INDEX IX_Timetables_ApplicationUserId ON Timetables(ApplicationUserId);
END

-- Add foreign key constraints if they don't exist
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
              WHERE name = 'FK_Lecturers_AspNetUsers_UserId')
BEGIN
    -- Add foreign key from Lecturers to AspNetUsers
    ALTER TABLE Lecturers
    ADD CONSTRAINT FK_Lecturers_AspNetUsers_UserId
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
    ON DELETE SET NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys 
              WHERE name = 'FK_Timetables_AspNetUsers_ApplicationUserId')
BEGIN
    -- Add foreign key from Timetables to AspNetUsers
    ALTER TABLE Timetables
    ADD CONSTRAINT FK_Timetables_AspNetUsers_ApplicationUserId
    FOREIGN KEY (ApplicationUserId) REFERENCES AspNetUsers(Id)
    ON DELETE SET NULL;
END

-- Clean up duplicate lecturer data (optional)
-- UPDATE Timetables SET ApplicationUserId = NULL WHERE ApplicationUserId IS NOT NULL;
-- UPDATE Lecturers SET UserId = NULL WHERE UserId IS NOT NULL;

-- Link lecturers with users where possible (by matching email addresses)
UPDATE l
SET l.UserId = u.Id
FROM Lecturers l
JOIN AspNetUsers u ON l.Email = u.Email
WHERE l.UserId IS NULL AND u.Role = 'Lecturer';

-- Link timetables with users (through lecturers)
UPDATE t
SET t.ApplicationUserId = l.UserId
FROM Timetables t
JOIN Lecturers l ON t.LecturerId = l.LecturerID
WHERE t.ApplicationUserId IS NULL AND l.UserId IS NOT NULL; 