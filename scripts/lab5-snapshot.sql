-- Each call opens a fresh connection. Do not include credentials or connection metadata.
SELECT json_build_object(
    'students', (SELECT COALESCE(json_agg(row ORDER BY "Id"), '[]') FROM
        (SELECT "Id", "StudentNumber", "Name", "Contact", "IsAllowedToBorrow" FROM "Students") AS row),
    'equipment', (SELECT COALESCE(json_agg(row ORDER BY "Id"), '[]') FROM
        (SELECT "Id", "Identifier", "Name", "Description", "IsAvailable" FROM "Equipment") AS row),
    'borrowings', (SELECT COALESCE(json_agg(row ORDER BY "Id"), '[]') FROM
        (SELECT "Id", "StudentId", "EquipmentId", "BorrowedDate", "ExpectedReturnDate", "ActualReturnDate", "Status" FROM "Borrowings") AS row)
)::text;
