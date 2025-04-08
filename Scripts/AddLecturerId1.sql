-- Script to add LecturerId1 column to Timetables and update data
BEGIN TRANSACTION;

-- Step 1: Check if LecturerId1 column already exists, if not add it
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Timetables' AND COLUMN_NAME = 'LecturerId1')
BEGIN
    ALTER TABLE Timetables ADD LecturerId1 nvarchar(10) NULL;
    PRINT 'Added LecturerId1 column to Timetables table';
END
ELSE
BEGIN
    PRINT 'LecturerId1 column already exists in Timetables table';
END

-- Step 2: Update LecturerId1 values from LecturerId
UPDATE Timetables
SET LecturerId1 = LecturerId
WHERE LecturerId1 IS NULL AND LecturerId IS NOT NULL;
PRINT 'Updated LecturerId1 values from LecturerId';

-- Step 3: Check if DEFAULT lecturer exists, if not create it
IF NOT EXISTS (SELECT * FROM Lecturers WHERE LecturerID = 'DEFAULT')
BEGIN
    INSERT INTO Lecturers (LecturerID, FullName, Name, Email, Phone, Department)
    VALUES ('DEFAULT', 'Default Lecturer', 'Default', 'default@university.com', 'N/A', 'Default Department');
    PRINT 'Created DEFAULT lecturer';
END
ELSE
BEGIN
    PRINT 'DEFAULT lecturer already exists';
END

-- Step 4: Update any remaining null LecturerId1 values with DEFAULT
UPDATE Timetables
SET LecturerId1 = 'DEFAULT'
WHERE LecturerId1 IS NULL;
PRINT 'Updated remaining null LecturerId1 values with DEFAULT';

-- Step 5: Add foreign key constraint if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
               WHERE CONSTRAINT_NAME = 'FK_Timetables_Lecturers_LecturerId1')
BEGIN
    ALTER TABLE Timetables
    ADD CONSTRAINT FK_Timetables_Lecturers_LecturerId1
    FOREIGN KEY (LecturerId1) 
    REFERENCES Lecturers(LecturerID)
    ON DELETE SET NULL;
    PRINT 'Added foreign key constraint for LecturerId1';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint for LecturerId1 already exists';
END

COMMIT;
PRINT 'Successfully completed all operations'; 