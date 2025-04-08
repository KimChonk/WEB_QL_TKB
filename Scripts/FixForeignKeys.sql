-- Script to fix foreign key constraint issues in Timetables table
BEGIN TRANSACTION;

PRINT 'Starting to fix foreign key issues in Timetables table';

-- Check if FK_Timetables_AspNetUsers_LecturerId exists and drop it
IF EXISTS (
    SELECT * 
    FROM sys.foreign_keys 
    WHERE name = 'FK_Timetables_AspNetUsers_LecturerId'
)
BEGIN
    ALTER TABLE Timetables DROP CONSTRAINT FK_Timetables_AspNetUsers_LecturerId;
    PRINT 'Dropped incorrect foreign key FK_Timetables_AspNetUsers_LecturerId';
END
ELSE
BEGIN
    PRINT 'Foreign key FK_Timetables_AspNetUsers_LecturerId not found';
END

-- Check if FK_Timetables_Lecturers_LecturerId exists and drop it
IF EXISTS (
    SELECT * 
    FROM sys.foreign_keys 
    WHERE name = 'FK_Timetables_Lecturers_LecturerId'
)
BEGIN
    ALTER TABLE Timetables DROP CONSTRAINT FK_Timetables_Lecturers_LecturerId;
    PRINT 'Dropped foreign key FK_Timetables_Lecturers_LecturerId to recreate it';
END

-- Check if FK_Timetables_Lecturers_LecturerId1 exists and drop it
IF EXISTS (
    SELECT * 
    FROM sys.foreign_keys 
    WHERE name = 'FK_Timetables_Lecturers_LecturerId1'
)
BEGIN
    ALTER TABLE Timetables DROP CONSTRAINT FK_Timetables_Lecturers_LecturerId1;
    PRINT 'Dropped foreign key FK_Timetables_Lecturers_LecturerId1 to recreate it';
END

-- Check if FK_Timetables_AspNetUsers_ApplicationUserId exists and drop it
IF EXISTS (
    SELECT * 
    FROM sys.foreign_keys 
    WHERE name = 'FK_Timetables_AspNetUsers_ApplicationUserId'
)
BEGIN
    ALTER TABLE Timetables DROP CONSTRAINT FK_Timetables_AspNetUsers_ApplicationUserId;
    PRINT 'Dropped foreign key FK_Timetables_AspNetUsers_ApplicationUserId to recreate it';
END

-- Ensure LecturerId1 column exists
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Timetables' AND COLUMN_NAME = 'LecturerId1'
)
BEGIN
    ALTER TABLE Timetables ADD LecturerId1 nvarchar(10) NULL;
    PRINT 'Added LecturerId1 column to Timetables table';
END

-- Ensure the ApplicationUserId column exists
IF NOT EXISTS (
    SELECT * 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Timetables' AND COLUMN_NAME = 'ApplicationUserId'
)
BEGIN
    ALTER TABLE Timetables ADD ApplicationUserId nvarchar(450) NULL;
    PRINT 'Added ApplicationUserId column to Timetables table';
END

-- Create DEFAULT lecturer if it doesn't exist
IF NOT EXISTS (
    SELECT * 
    FROM Lecturers 
    WHERE LecturerID = 'DEFAULT'
)
BEGIN
    INSERT INTO Lecturers (LecturerID, FullName, Name, Email, Phone, Department)
    VALUES ('DEFAULT', 'Default Lecturer', 'Default', 'default@university.com', 'N/A', 'Default Department');
    PRINT 'Created DEFAULT lecturer';
END

-- Update LecturerId1 from LecturerId for NULL values
UPDATE Timetables
SET LecturerId1 = LecturerId
WHERE LecturerId1 IS NULL AND LecturerId IS NOT NULL;
PRINT 'Updated LecturerId1 from LecturerId for NULL values';

-- Update any remaining NULL LecturerId1 to DEFAULT
UPDATE Timetables
SET LecturerId1 = 'DEFAULT'
WHERE LecturerId1 IS NULL;
PRINT 'Updated remaining NULL LecturerId1 values to DEFAULT';

-- Create proper foreign key from LecturerId to Lecturers
ALTER TABLE Timetables
ADD CONSTRAINT FK_Timetables_Lecturers_LecturerId
FOREIGN KEY (LecturerId) REFERENCES Lecturers(LecturerID)
ON DELETE CASCADE;
PRINT 'Created proper FK_Timetables_Lecturers_LecturerId constraint';

-- Create proper foreign key from LecturerId1 to Lecturers
ALTER TABLE Timetables
ADD CONSTRAINT FK_Timetables_Lecturers_LecturerId1
FOREIGN KEY (LecturerId1) REFERENCES Lecturers(LecturerID)
ON DELETE SET NULL;
PRINT 'Created proper FK_Timetables_Lecturers_LecturerId1 constraint';

-- Create proper foreign key from ApplicationUserId to AspNetUsers
ALTER TABLE Timetables
ADD CONSTRAINT FK_Timetables_AspNetUsers_ApplicationUserId
FOREIGN KEY (ApplicationUserId) REFERENCES AspNetUsers(Id)
ON DELETE SET NULL;
PRINT 'Created proper FK_Timetables_AspNetUsers_ApplicationUserId constraint';

COMMIT;
PRINT 'Successfully fixed all foreign key constraints'; 