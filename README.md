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


### 2. Architecture Reflection
Question 1: How does Clean Architecture enforce the Dependency Inversion Principle here?
- High-level policy classes like BorrowEquipmentService inside the Application layer do not depend on low-level data access implementations. Instead, they depend on interface abstractions (IStudentRepository, IEquipmentRepository, IBorrowingRepository) defined in the Application layer itself. The concrete implementations reside in the Infrastructure layer and are injected at runtime.

Question 2: What are the benefits of decoupling domain models from storage mechanisms?
- Decoupling domain logic from persistence ensures that business rules remain completely agnostic to storage technologies. You can replace the in-memory repository implementation with Entity Framework Core, SQL Server, or a document database without modifying a single line of code in EquipmentBorrowing.Domain or BorrowEquipmentService.

Question 3: Why are validation checks placed in the Application Service rather than the ConsoleApp?
Answer: Placing validation logic inside BorrowEquipmentService centralizes business rule enforcement. If the application expands to support a Web API, Desktop UI, or Mobile frontend in the future, the exact same validation rules automatically apply without duplicating code across user interfaces.

Question 4: How do asynchronous interfaces (Task<T>) prepare the application for real-world persistence?
- Real-world databases and network operations require non-blocking I/O operations. Defining repository methods as asynchronous (Task<T>) from the start ensures the application layer is architecture-ready for async database callers (like EF Core or Dapper) without breaking method signatures.

Question 5: What role do mock/in-memory repositories play during software development?
- In-memory repositories allow developers to build, test, and validate core application workflows and domain rules immediately, long before database schemas, connection strings, or cloud infrastructure are set up. They also enable fast, reliable unit testing without external database dependencies.

---

## Part L: Desktop Application (Activity 2)

### 1. Desktop Project

`EquipmentBorrowing.Desktop` is an Avalonia UI desktop application that adds a graphical presentation layer on top of the architecture from Activity 1. It follows the Model-View-ViewModel (MVVM) pattern using **CommunityToolkit.Mvvm** for observable properties and commands.

The project references only `EquipmentBorrowing.Application` and `EquipmentBorrowing.Infrastructure` directly (Domain types are available transitively through Application). Neither `EquipmentBorrowing.Domain` nor `EquipmentBorrowing.Application` reference Avalonia in any way — the UI is layered strictly on top of the existing architecture, not merged into it.

Dependency injection is configured via **Microsoft.Extensions.DependencyInjection**, wired in `App.axaml.cs`. Repositories are registered as `Singleton` (so borrowed/returned state is preserved consistently across the Equipment and Active Borrowings screens), while services and ViewModels are registered as `Transient`.

### 2. Updated Architecture

Avalonia View (EquipmentView, BorrowingsView)
│
│ Binding / Command ({Binding BorrowCommand})
▼
ViewModel (EquipmentViewModel, BorrowingsViewModel)
│
│ Application Operation (ExecuteAsync)
▼
Application Service (BorrowEquipmentService, ReturnEquipmentService)
│
├──────────► Domain (Student, Equipment, Borrowing)
│
▼
Repository Interface (IStudentRepository, IEquipmentRepository, IBorrowingRepository)
▲
│
Infrastructure Implementation (InMemoryStudentRepository, etc.)


All wiring between layers is assembled once, at startup, inside `App.axaml.cs`'s `ConfigureServices` method — the single composition root for the Desktop application.

### 3. Borrow Equipment Flow

1. The user selects an equipment item from the list in `EquipmentView` (bound to `SelectedEquipment`), enters a Student ID, and picks an expected return date.
2. Clicking **Borrow Equipment** triggers `BorrowCommand`, generated automatically by `[RelayCommand]` on `EquipmentViewModel.BorrowAsync()`.
3. The ViewModel first performs **presentation validation only**: is an equipment item selected, is the Student ID a valid number, is the return date today or later. These are input-format checks, not business rules.
4. If presentation validation passes, the ViewModel calls `_borrowEquipmentService.ExecuteAsync(studentId, equipmentId, expectedReturnDate)`.
5. `BorrowEquipmentService` performs all actual **business validation** (student allowed to borrow, borrowing limit not exceeded, equipment available) using the injected repositories, then creates and persists a new `Borrowing` record if every check passes.
6. The service returns a `BorrowResult` indicating success or failure with a message.
7. The ViewModel updates `StatusMessage` with the result and, on success, reloads the equipment list so the UI reflects the item's new `IsAvailable` state — the `ObservableCollection` and data bindings handle the visual refresh automatically.

### 4. Return Equipment Flow

1. The user switches to the Active Borrowings screen, which loads all borrowings with `Status == Active` via `IBorrowingRepository.GetActiveBorrowingsAsync()`.
2. Selecting a borrowing binds it to `BorrowingsViewModel.SelectedBorrowing`.
3. Clicking **Return Equipment** triggers `ReturnCommand`, calling `ReturnEquipmentService.ExecuteAsync(borrowingId)`.
4. The service locates the borrowing, confirms it hasn't already been returned, marks its status as `Returned`, sets the related equipment's `IsAvailable` back to `true`, and decrements the student's `ActiveBorrowingsCount` — coordinating all three repositories within one service method.
5. A `ReturnResult` is returned to the ViewModel, which updates `StatusMessage` and reloads the active borrowings list, removing the now-returned item from the visible collection.

### 5. Architectural Reflection

**1. Why should the View not call a repository directly?**
The View is only responsible for layout, controls, and bindings — it has no knowledge of business rules or data access. Letting it call a repository directly would scatter business logic across the UI layer and break the separation Activity 1 was built around, making the same rules impossible to reuse if a different UI (web, mobile) were added later.

**2. Why should business rules not be implemented in the ViewModel?**
The ViewModel's job is to manage presentation state and translate user actions into calls to the Application layer — not to decide *whether* an operation is allowed. If borrowing rules were duplicated inside `EquipmentViewModel`, they could drift out of sync with the rules already correctly implemented and tested in `BorrowEquipmentService`, creating two sources of truth for the same business decision.

**3. What is the responsibility of the ViewModel?**
To hold presentation state (selected items, input values, status messages), expose commands the View can bind to, perform lightweight presentation validation (is a field filled in, is input the right format), and delegate any real business operation to the appropriate Application service — then reflect whatever result comes back in the UI.

**4. Why can the existing Application layer work without knowing that Avalonia is being used?**
`BorrowEquipmentService` and `ReturnEquipmentService` depend only on repository interfaces and domain types defined in `EquipmentBorrowing.Application` and `EquipmentBorrowing.Domain` — neither project references Avalonia. The Desktop project depends *on* Application, not the other way around, so the dependency points strictly in one direction, and the same services could be reused unmodified by a console app, web API, or any other frontend.

**5. What advantage is gained from registering dependencies in one composition point?**
All object construction and wiring happens in a single place (`App.axaml.cs`'s `ConfigureServices`), instead of being scattered across every class that happens to need a dependency. Adding a new ViewModel or swapping an implementation (e.g., a different repository) means changing one registration line, not hunting down every `new SomeClass(...)` call throughout the codebase.

**6. If the in-memory repository were replaced by SQLite later, which parts of the current interface should remain largely unchanged?**
The repository interfaces (`IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository`), the domain models, the application services (`BorrowEquipmentService`, `ReturnEquipmentService`), and every ViewModel and View would remain unchanged. Only the Infrastructure layer's concrete repository classes would need new SQLite-based implementations — the dependency inversion established in Activity 1 is exactly what makes this swap possible without touching the rest of the application.
