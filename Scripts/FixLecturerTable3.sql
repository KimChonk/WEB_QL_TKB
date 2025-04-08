-- Add UserId column to Lecturers table
ALTER TABLE Lecturers ADD UserId nvarchar(450) NULL;

-- Create index for the foreign key
CREATE INDEX IX_Lecturers_UserId ON Lecturers(UserId);

-- Add foreign key constraint
ALTER TABLE Lecturers ADD CONSTRAINT FK_Lecturers_AspNetUsers_UserId
FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE SET NULL; 