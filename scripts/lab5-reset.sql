-- Run only on the private lab5_acceptance database created by the verification script.
BEGIN;
DO $$
BEGIN
    IF current_database() <> 'lab5_acceptance' THEN
        RAISE EXCEPTION 'This reset is only for the disposable Lab 5 database';
    END IF;
END $$;

TRUNCATE "Borrowings", "Students", "Equipment" RESTART IDENTITY;

INSERT INTO "Students" ("StudentNumber", "Name", "IsAllowedToBorrow") VALUES
    ('ST-100', 'Ada Cruz', true),
    ('ST-200', 'Ben Diaz', false),
    ('ST-300', 'Cara Lim', true),
    ('ST-400', 'Dani Ong', true);

INSERT INTO "Equipment" ("Identifier", "Name", "IsAvailable") VALUES
    ('EQ-001', 'Camera', false),
    ('EQ-002', 'Laptop', true),
    ('EQ-003', 'Projector', true),
    ('EQ-004', 'Tablet', false),
    ('EQ-005', 'Microphone', false),
    ('EQ-006', 'Tripod', true),
    ('EQ-007', 'Speaker', true);

INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "BorrowedDate", "ExpectedReturnDate", "ActualReturnDate", "Status")
SELECT s."Id", e."Id", dates.borrowed_date, dates.due_date, dates.returned_date, dates.status
FROM (VALUES
    ('ST-100', 'EQ-001', DATE '2026-11-01', DATE '2026-11-09', NULL::date, 0),
    ('ST-100', 'EQ-003', DATE '2026-11-02', DATE '2026-11-12', DATE '2026-11-08', 1),
    ('ST-300', 'EQ-004', DATE '2026-11-03', DATE '2026-11-12', NULL::date, 0),
    ('ST-300', 'EQ-005', DATE '2026-11-03', DATE '2026-11-12', NULL::date, 0)
) AS dates(student_number, equipment_code, borrowed_date, due_date, returned_date, status)
JOIN "Students" s ON s."StudentNumber" = dates.student_number
JOIN "Equipment" e ON e."Identifier" = dates.equipment_code
ORDER BY dates.equipment_code;

COMMIT;
