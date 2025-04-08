-- Script khắc phục lỗi cột LecturerId1 trong bảng Timetables
BEGIN TRANSACTION;

PRINT 'Bắt đầu sửa lỗi cột LecturerId1 trong bảng Timetables';

-- 1. Kiểm tra xem cột LecturerId1 có tồn tại hay không
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE name = 'LecturerId1' AND object_id = OBJECT_ID('Timetables')
)
BEGIN
    PRINT 'Cột LecturerId1 tồn tại trong bảng Timetables';

    -- 2. Kiểm tra foreign key constraint liên quan đến LecturerId1
    DECLARE @fkName NVARCHAR(256);
    SELECT @fkName = name
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('Timetables')
    AND OBJECT_NAME(referenced_object_id) = 'Lecturers'
    AND COL_NAME(parent_object_id, parent_column_id) = 'LecturerId1';

    -- 3. Xóa foreign key constraint nếu tồn tại
    IF @fkName IS NOT NULL
    BEGIN
        DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE Timetables DROP CONSTRAINT ' + @fkName;
        EXEC sp_executesql @sql;
        PRINT 'Đã xóa foreign key constraint: ' + @fkName;
    END

    -- 4. Cập nhật giá trị cho cột LecturerId1, lấy từ cột LecturerId
    UPDATE Timetables 
    SET LecturerId1 = LecturerId
    WHERE LecturerId1 IS NULL AND LecturerId IS NOT NULL;
    PRINT 'Đã cập nhật dữ liệu từ LecturerId sang LecturerId1 cho các bản ghi NULL';

    -- 5. Kiểm tra xem còn bản ghi nào có LecturerId1 là NULL không
    DECLARE @nullCount INT;
    SELECT @nullCount = COUNT(*) FROM Timetables WHERE LecturerId1 IS NULL;
    
    IF @nullCount > 0
    BEGIN
        PRINT 'Vẫn còn ' + CAST(@nullCount AS NVARCHAR) + ' bản ghi có LecturerId1 = NULL';
        
        -- 6. Tạo một lecturer mặc định nếu chưa có
        DECLARE @defaultLecturerID VARCHAR(10) = 'DEFAULT';
        
        IF NOT EXISTS (SELECT 1 FROM Lecturers WHERE LecturerID = @defaultLecturerID)
        BEGIN
            INSERT INTO Lecturers (LecturerID, FullName, Name, Email, Phone, Department)
            VALUES (@defaultLecturerID, 'Default Lecturer', 'Default', 'default@university.com', 'N/A', 'Default Department');
            PRINT 'Đã tạo lecturer mặc định với ID: ' + @defaultLecturerID;
        END
        
        -- 7. Cập nhật các bản ghi còn lại với lecturer mặc định
        UPDATE Timetables
        SET LecturerId1 = @defaultLecturerID
        WHERE LecturerId1 IS NULL;
        PRINT 'Đã cập nhật các bản ghi còn lại với lecturer mặc định';
    END

    -- 8. Thay đổi định nghĩa cột để cho phép NULL
    ALTER TABLE Timetables ALTER COLUMN LecturerId1 VARCHAR(10) NULL;
    PRINT 'Đã thay đổi cột LecturerId1 để cho phép NULL';

    -- 9. Tái tạo foreign key constraint với DELETE CASCADE
    ALTER TABLE Timetables
    ADD CONSTRAINT FK_Timetables_Lecturers_LecturerId1
    FOREIGN KEY (LecturerId1) REFERENCES Lecturers(LecturerID) ON DELETE CASCADE;
    PRINT 'Đã tạo lại constraint foreign key cho LecturerId1';
END
ELSE
BEGIN
    PRINT 'Cột LecturerId1 không tồn tại trong bảng Timetables';
END

COMMIT;
PRINT 'Hoàn tất sửa lỗi'; 