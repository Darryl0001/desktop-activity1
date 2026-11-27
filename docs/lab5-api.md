# Lab 5 API contract

Use UTF-8 JSON under `/api/v1`. IDs are opaque resource identifiers returned by the API. Resource IDs and display identifiers belong to separate fields. Calendar dates use `YYYY-MM-DD` in Asia/Manila. The API sets checkout and return dates. Machine-readable schemas and statuses are in the [OpenAPI document](../src/EquipmentBorrowing.Api/openapi.json), served at `GET /openapi/v1.json`.

| ID | Method and path | Request | Success |
| --- | --- | --- | --- |
| API-01 | `GET /equipment` | Optional `available=true` or `false` | 200 equipment array |
| API-02 | `GET /students` | Optional `search` by name or student number | 200 student array |
| API-03 | `GET /students/{id}` | Resource ID | 200 student |
| API-04 | `POST /students` | `studentNumber`, `fullName`, optional `contact` | 201 student and `Location` |
| API-05 | `PUT /students/{id}` | All editable student fields, including nullable `contact` | 200 student |
| API-06 | `PATCH /students/{id}/eligibility` | `isEligible` boolean | 200 student |
| API-07 | `GET /equipment/{id}` | Resource ID | 200 equipment |
| API-08 | `POST /equipment` | `equipmentCode`, `name`, optional `description` | 201 equipment and `Location` |
| API-09 | `PUT /equipment/{id}` | All editable equipment fields, including nullable `description` | 200 equipment |
| API-10 | `POST /borrowings` | `studentId`, `equipmentId`, `expectedReturnDate` | 201 borrowing and `Location` |
| API-11 | `POST /checkout-requests` | `studentId`, distinct `equipmentIds` (at least two), `expectedReturnDate` | 201 object containing a `borrowings` array |
| API-12 | `POST /borrowings/{id}/return` | Empty JSON object `{}` | 200 returned borrowing |
| API-13 | `GET /borrowings/{id}` | Resource ID | 200 borrowing |
| API-14 | `GET /borrowings` | Optional `search`, `status`, `sort`, `direction` | 200 borrowing array |
| API-15 | `GET /students/{id}/borrowings` | Student resource ID | 200 borrowing array |
| API-16 | `GET /equipment/{id}/borrowings` | Equipment resource ID | 200 borrowing array |

Borrowing search matches partial student names, student numbers, or equipment names. Whitespace around the query is trimmed and casing is ignored. Status is `Active`, `Returned`, or `All`. Sort by `borrowedDate` or `expectedReturnDate` using `asc` or `desc`. The defaults are `All`, `borrowedDate`, and `asc`. Borrowing ID ascending breaks date ties. Lists and histories return `[]` when there are no results.

Student creation defaults to eligible. Equipment creation defaults to available. Setting the current eligibility is a no-op. Use JSON `null` to clear contact or description. The student and equipment detail and write responses send `ETag`. Clients can send that tag in `If-Match` on updates to detect edits made since selection. Checkout revalidates eligibility, availability, the three-item limit, and dates inside the database transaction.

## Errors

| Status | Code | Meaning |
| --- | --- | --- |
| 400 | `invalid_request` | Malformed JSON, invalid fields, invalid date, or unsupported query value |
| 404 | `not_found` | Missing route or resource |
| 405 | `method_not_allowed` | Unsupported method for the route |
| 409 | `conflict` | Duplicate identifier, stale edit, business-rule conflict, or returned borrowing |
| 500 | `server_error` | Server or database failure |
| 500 | `outcome_unknown` | The server could not confirm a write during commit |

Each error body has a `code` and `message`. A client gets no HTTP status when the API is unreachable. If a write loses its response, refresh affected records and history before retrying. Check persisted state separately when verifying rollback.

## Example

Get valid IDs first. For an acceptance run on the fixed date 2026-11-10, the due date below is valid.

```json
{
  "studentId": 4,
  "equipmentIds": [2, 6],
  "expectedReturnDate": "2026-11-12"
}
```

Send this payload to `POST /api/v1/checkout-requests`. Use the IDs returned by your server in place of the example values.
