-- This transaction-local fault only runs in the disposable acceptance database.
DO $$
BEGIN
    IF current_database() <> 'lab5_acceptance' THEN
        RAISE EXCEPTION 'Fault injection requires the disposable Lab 5 database';
    END IF;
END $$;

CREATE FUNCTION lab5_reject_second_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (
        SELECT FROM "Students" s JOIN "Borrowings" b ON b."StudentId" = s."Id"
        WHERE s."StudentNumber" = 'ST-400' AND s."Id" = NEW."StudentId"
    ) THEN
        RAISE LOG 'LAB5_T05 observed the first borrowing in the transaction before the second insert';
        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Lab 5 controlled rollback fault';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER lab5_checkout_failure BEFORE INSERT ON "Borrowings"
FOR EACH ROW EXECUTE FUNCTION lab5_reject_second_insert();
