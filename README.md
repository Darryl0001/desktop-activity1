# Campus Equipment Borrowing System

This repository contains a C#/.NET implementation of a Campus Equipment Borrowing System built using Clean Architecture principles. It demonstrates strict separation of concerns, repository pattern abstractions, dependency inversion, and domain-driven design without depending on external databases or UI frameworks.

---


### Members
- Brent Marcus Ocaya
- Darryl Macarandan


## Part A: Requirements & System Analysis

### 1. Actors

* **Student (Primary Actor):** An authorized student who requests to borrow equipment for academic use and expects the system to process their request, validate eligibility, and record active borrowing records.
* **Laboratory Manager / System Administrator:** Responsible for overseeing equipment inventory and relies on the system to enforce borrowing limits, block ineligible students, and track item availability.

---

### 2. Major Use Cases

#### Use Case 1: Borrow Equipment (Implemented)
| Item | Description |
|---|---|
| **Use Case** | Borrow Equipment |
| **Primary Actor** | Student |
| **Preconditions** | The student is registered in the system, and the equipment exists in the lab catalog. |
| **Main Action** | The student submits a borrowing request for an available piece of equipment. The system validates student borrowing privileges, borrowing capacity limits, and equipment availability, then records the checkout transaction. |
| **Expected Result** | A new `Borrowing` record is created with `Active` status, the equipment `IsAvailable` state becomes `false`, and the student's `ActiveBorrowingsCount` increases by 1. |
| **Possible Failure** | Student is blocked (`IsAllowedToBorrow == false`), student reached borrowing limit (`ActiveBorrowingsCount >= MaxAllowedBorrowings`), or equipment is currently unavailable. |

#### Use Case 2: Return Equipment
| Item | Description |
|---|---|
| **Use Case** | Return Equipment |
| **Primary Actor** | Student |
| **Preconditions** | An active borrowing transaction exists for the student and equipment item. |
| **Main Action** | The student returns the borrowed equipment to the laboratory. The system marks the borrowing record as returned and updates the item availability. |
| **Expected Result** | Borrowing status changes to `Returned`, the equipment `IsAvailable` state becomes `true`, and the student's `ActiveBorrowingsCount` decreases by 1. |
| **Possible Failure** | No active borrowing record is found matching the provided student and equipment IDs. |

#### Use Case 3: Find Available Equipment
| Item | Description |
|---|---|
| **Use Case** | Find Available Equipment |
| **Primary Actor** | Student / Laboratory Manager |
| **Preconditions** | Equipment records exist in the repository catalog. |
| **Main Action** | The actor requests a list of all equipment currently available for checkout. The system queries storage and filters items where `IsAvailable == true`. |
| **Expected Result** | A list of available equipment items is returned and displayed to the actor. |
| **Possible Failure** | No equipment items match the search query, or all equipment items are currently checked out. |

---

### 3. Domain Concepts

* **Student**
  * **Information:** `Id`, `Name`, `IsAllowedToBorrow`, `ActiveBorrowingsCount`, `MaxAllowedBorrowings`.
  * **Rules/State:** Tracks whether the student is eligible to borrow and if they have reached their maximum allowed active borrowings.
  * **Non-Responsibilities:** Does not track equipment inventory or save student data to a database.

* **Equipment**
  * **Information:** `Id`, `Name`, `IsAvailable`.
  * **Rules/State:** Tracks whether the physical equipment item is currently free to be borrowed.
  * **Non-Responsibilities:** Does not track who borrowed the item or calculate due dates.

* **Borrowing**
  * **Information:** `Id`, `StudentId`, `EquipmentId`, `BorrowedDate`, `ExpectedReturnDate`, `Status`.
  * **Rules/State:** Represents an active or completed transaction connecting a student to an item with an expected return timeline.
  * **Non-Responsibilities:** Does not directly modify database tables or handle user input formatting.

---

## Part I: Architecture Explanation

### 1. Solution Structure

* **`EquipmentBorrowing.Domain` (Class Library):** Holds the core enterprise logic, entity models (`Student`, `Equipment`, `Borrowing`), and domain enums (`BorrowingStatus`). It has **zero dependencies** on external libraries or other projects.
* **`EquipmentBorrowing.Application` (Class Library):** Contains application use cases (`BorrowEquipmentService`), result DTOs (`BorrowResult`), and repository interfaces (`IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository`). Coordinates domain models and defines required data access contracts.
* **`EquipmentBorrowing.Infrastructure` (Class Library):** Implements technical mechanisms and persistence defined by the Application layer. Houses in-memory mock repositories (`InMemoryStudentRepository`, `InMemoryEquipmentRepository`, `InMemoryBorrowingRepository`) using standard C# collections (`List<T>`).
* **`EquipmentBorrowing.ConsoleApp` (Console Application):** The executable entry point (`Program.cs`). Assembles dependencies via manual Dependency Injection and executes both successful and failure demonstration scenarios.
* **`EquipmentBorrowing.Tests` (xUnit Project):** Contains automated tests verifying domain entity constraints and application service validation logic.

---

### 2. Dependency Direction

Dependencies point strictly inward toward the core Domain layer:

```text
       EquipmentBorrowing.ConsoleApp / Tests
                         │
        ┌────────────────┴────────────────┐
        ▼                                 ▼
EquipmentBorrowing.               EquipmentBorrowing.
  Infrastructure                    Application
        │                                 │
        └────────────────┬────────────────┘
                         ▼
               EquipmentBorrowing.
                     Domain

