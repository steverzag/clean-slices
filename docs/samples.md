# Sample features

This solution was created with the sample features: a small CRUD over two entities, **Tasks** and **Users**. They show the conventions of the solution, and of every option it was created with, on more than one entity: a relationship, business rules, conflicts and domain event handlers that change other entities.

Use them as a reference for your own features, then delete them when you no longer need them.

## Domain

- **`TaskItem`** and **`User`** are the entities. They:
  - create instances through `Create(...)` factories that validate the input
  - change state only through methods (`UpdateTitle`, `Complete`, `AssignUser`, `Delete`, ...); setters are private
  - raise domain events (`Created`, `Completed`, `Deleted`)
- **`TaskItemUserAssignment`** is the join between tasks and users. It is *not* an `Entity`: it has a composite key `(TaskItemId, UserId)` plus `AssignedOn`. `AssignUser` and `UnassignUser` are all-or-nothing. Every id is validated before the collection changes.
- **Errors**: `TaskItemErrors` and `UserErrors` in Core hold the field errors (empty title, invalid email, ...). The API's own errors are in `Features/Users/UserErrors.cs` (`NotFound`, `EmailInUse`) and `Features/Tasks/TaskItemErrors.cs` (`NotFound`, `UserNotFound`). How failures reach the client is in [error-handling.md](error-handling.md).

## Persistence

- **`AppDbContext`** has two `DbSet`s, `Tasks` and `Users`, which also give the table names. Features you add use `context.Set<T>()` instead, so `AppDbContext` doesn't change.
- **Type configurations** hold only what EF can't work out by convention:
  - maximum lengths, which reuse the domain constants (`TaskItem.TitleMaxLength`, ...)
  - a unique index on `User.Email`
  - the composite key and the `UserId` index on assignments

  EF already finds backing fields and foreign keys on its own, so there is no `ToTable`, `Navigation` or `HasMany` for those.
- **Event handlers**:
  - logging handlers for the created, completed and deleted events
  - `UnassignDeletedUserFromTasksHandler`, which removes a deleted user from every task it was assigned to, in the same save as the delete

## Endpoints

| Method | Route                             | Description                     |
|--------|-----------------------------------|---------------------------------|
| GET    | `/tasks?pageNumber=&pageSize=`    | List tasks, newest first, paged |
| GET    | `/tasks/{id}`                     | Get a task (includes `UserIds`) |
| POST   | `/tasks`                          | Create `{ title, description }` |
| PATCH  | `/tasks/{id}/title`               | `{ title }`                     |
| PATCH  | `/tasks/{id}/description`         | `{ description }`               |
| PATCH  | `/tasks/{id}/not-started`         | Set status                      |
| PATCH  | `/tasks/{id}/in-progress`         | Set status                      |
| PATCH  | `/tasks/{id}/completed`           | Set status                      |
| POST   | `/tasks/{id}/users/{userId}`      | Assign a user                   |
| DELETE | `/tasks/{id}/users/{userId}`      | Unassign a user                 |
| DELETE | `/tasks/{id}`                     | Delete a task                   |
| GET    | `/users?pageNumber=&pageSize=`    | List users, newest first, paged |
| GET    | `/users/{id}`                     | Get a user                      |
| POST   | `/users`                          | Create `{ name, email }` (409 if the email is used) |
| PATCH  | `/users/{id}/name`                | `{ name }`                      |
| PATCH  | `/users/{id}/email`               | `{ email }` (409 if the email is used) |
| DELETE | `/users/{id}`                     | Delete a user (also unassigns them from their tasks) |

`src/CleanSlices.Api/CleanSlices.Api.http` has ready-made requests for all of them.

## Tests

`tests/CleanSlices.Core.Tests/Domain/Tasks`, `Domain/Users` and `tests/CleanSlices.Api.Tests/Features/Tasks`, `Features/Users`.

## Known trade-offs

- **Any status change is allowed**, including `Completed → NotStarted`.
- **Concurrent duplicates** (two identical assigns, or the same email created twice at once) get past the pre-checks. The database constraint stops them, which results in a logged 500.
