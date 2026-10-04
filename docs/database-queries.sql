-- ITSD 81 - Laboratory Activity 3
-- Database Queries
-- Campus Equipment Borrowing System

-- 1. Basic Retrieval
-- Retrieve all equipment.
SELECT *
FROM Equipment;


-- 2. Filtering
-- Retrieve only equipment that is currently available.
SELECT *
FROM Equipment
WHERE IsAvailable = 1
ORDER BY Name;


-- 3. Join
-- Retrieve active borrowings together with the corresponding
-- student and equipment information.
SELECT
    s.Name AS Student,
    e.Name AS Equipment,
    b.BorrowedDate AS Borrowed,
    b.ExpectedReturnDate AS Due
FROM Borrowings AS b
INNER JOIN Students AS s
    ON b.StudentId = s.Id
INNER JOIN Equipment AS e
    ON b.EquipmentId = e.Id
WHERE b.Status = 0
ORDER BY b.BorrowedDate DESC;


-- 4. Aggregate
-- Count the number of active borrowings for each student.
SELECT
    s.Name AS Student,
    COUNT(b.Id) AS ActiveBorrowings
FROM Students AS s
LEFT JOIN Borrowings AS b
    ON s.Id = b.StudentId
    AND b.Status = 0
GROUP BY s.Id, s.Name
ORDER BY ActiveBorrowings DESC;


-- 5. Update
-- Mark a piece of equipment as available.
-- This example uses Equipment ID 1 (Laptop).
UPDATE Equipment
SET IsAvailable = 1
WHERE Id = 1;