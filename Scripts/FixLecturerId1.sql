-- Script để sửa lỗi với cột LecturerId1 trong bảng Timetables
BEGIN TRANSACTION;

-- 1. Kiểm tra xem cột LecturerId1 có tồn tại trong bảng Timetables không
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE name = 'LecturerId1' AND object_id = OBJECT_ID('Timetables')
)
BEGIN
    -- 2. Cập nhật giá trị cho cột LecturerId1 từ cột LecturerId
    UPDATE Timetables 
    SET LecturerId1 = LecturerId
    WHERE LecturerId1 IS NULL AND LecturerId IS NOT NULL;
    
    -- 3. Thống kê
    DECLARE @updatedCount INT;
    SET @updatedCount = @@ROWCOUNT;
    
    -- 4. Kiểm tra xem còn bản ghi nào có LecturerId1 là NULL không
    DECLARE @nullCount INT;
    SELECT @nullCount = COUNT(*) FROM Timetables WHERE LecturerId1 IS NULL;
    
    -- 5. Nếu còn, cập nhật với giá trị mặc định 'DEFAULT'
    IF @nullCount > 0
    BEGIN
        -- Kiểm tra xem có Lecturer với ID 'DEFAULT' không
        IF NOT EXISTS (SELECT 1 FROM Lecturers WHERE LecturerID = 'DEFAULT')
        BEGIN
            -- Tạo Lecturer mặc định nếu không tồn tại
            INSERT INTO Lecturers (LecturerID, FullName, Name, Email, Phone, Department)
            VALUES ('DEFAULT', 'Default Lecturer', 'Default', 'default@university.com', 'N/A', 'Default Department');
        END
        
        -- Cập nhật các bản ghi NULL còn lại
        UPDATE Timetables
        SET LecturerId1 = 'DEFAULT'
        WHERE LecturerId1 IS NULL;
    END

    -- 6. Thay đổi cột để cho phép NULL (nếu hiện tại không cho phép)
    IF EXISTS (
        SELECT 1 FROM sys.columns 
        WHERE name = 'LecturerId1' 
        AND object_id = OBJECT_ID('Timetables')
        AND is_nullable = 0  -- Cột không cho phép NULL
    )
    BEGIN
        -- Kiểm tra và xóa ràng buộc khóa ngoại trước
        DECLARE @fkName NVARCHAR(128);
        SELECT @fkName = name
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('Timetables')
        AND referenced_object_id = OBJECT_ID('Lecturers')
        AND COL_NAME(parent_object_id, parent_column_id) = 'LecturerId1';
        
        IF @fkName IS NOT NULL
        BEGIN
            DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE Timetables DROP CONSTRAINT ' + @fkName;
            EXEC sp_executesql @sql;
        END

        -- Thay đổi cột để cho phép NULL
        ALTER TABLE Timetables ALTER COLUMN LecturerId1 VARCHAR(10) NULL;
        
        -- Tái tạo khóa ngoại (nếu cần)
        IF @fkName IS NOT NULL
        BEGIN
            ALTER TABLE Timetables
            ADD CONSTRAINT FK_Timetables_Lecturers_LecturerId1
            FOREIGN KEY (LecturerId1) REFERENCES Lecturers(LecturerID) ON DELETE CASCADE;
        END
    END
END

COMMIT; 