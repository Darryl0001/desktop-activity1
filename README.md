| **Repository Interfaces** | Persistence Contracts | `IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository` |
| **Domain Models** | Business Entities & Rules | `Student`, `Equipment`, `Borrowing` |
| **Infrastructure** | Concrete Data Access | `InMemoryStudentRepository`, `InMemoryEquipmentRepository`, `InMemoryBorrowingRepository` |

---

## 4. Architecture Reflection

### Question 1: How does Clean Architecture enforce the Dependency Inversion Principle here?
**Answer:** High-level policy classes like `BorrowEquipmentService` inside the Application layer do not depend on low-level data access implementations. Instead, they depend on interface abstractions (`IStudentRepository`, `IEquipmentRepository`, `IBorrowingRepository`) defined in the Application layer itself. The concrete implementations reside in the Infrastructure layer and are injected at runtime.

### Question 2: What are the benefits of decoupling domain models from storage mechanisms?
**Answer:** Decoupling domain logic from persistence ensures that business rules remain completely agnostic to storage technologies. You can replace the in-memory repository implementation with Entity Framework Core, SQL Server, or a document database without modifying a single line of code in `EquipmentBorrowing.Domain` or `BorrowEquipmentService`.

### Question 3: Why are validation checks placed in the Application Service rather than the ConsoleApp?
**Answer:** Placing validation logic inside `BorrowEquipmentService` centralizes business rule enforcement. If the application expands to support a Web API, Desktop UI, or Mobile frontend in the future, the exact same validation rules automatically apply without duplicating code across user interfaces.

### Question 4: How do asynchronous interfaces (`Task<T>`) prepare the application for real-world persistence?
**Answer:** Real-world databases and network operations require non-blocking I/O operations. Defining repository methods as asynchronous (`Task<T>`) from the start ensures the application layer is architecture-ready for async database callers (like EF Core or Dapper) without breaking method signatures.

### Question 5: What role do mock/in-memory repositories play during software development?
**Answer:** In-memory repositories allow developers to build, test, and validate core application workflows and domain rules immediately, long before database schemas, connection strings, or cloud infrastructure are set up. They also enable fast, reliable unit testing without external database dependencies.