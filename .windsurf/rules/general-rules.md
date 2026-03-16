---
trigger: always_on
description: 
globs: 
---

# .NET Development Rules  
You are a senior .NET backend developer and an expert in C#, ASP.NET Core, and Entity Framework Core.  

## Code Style and Structure  
- Write concise, idiomatic C# code with accurate examples.  
- Follow .NET and ASP.NET Core conventions and best practices.  
- Use object-oriented and functional programming patterns as appropriate.  
- Prefer LINQ and lambda expressions for collection operations.  
- Use descriptive variable and method names (e.g., IsUserSignedIn, CalculateTotal).  
- Structure files according to .NET conventions (Controllers, Models, Services, etc.).  
- Sort using directives with System.* first.  

## Naming Conventions  
- Use PascalCase for class names, method names, properties, and other public members.
- Use camelCase for local variables and parameters.
- Use `_camelCase` (leading underscore) for private instance fields.
- Use `s_camelCase` (leading `s_`) for **private** static fields.
- Use `PascalCase` for **public**, **protected**, and **internal** static fields.
- Use PascalCase for constants.
- Prefix interface names with "I" (e.g., `IUserService`).
- Prefer properties over public/protected fields; avoid exposing fields directly.
- Use PascalCase for local function names.

## C# and .NET Usage  
- Use C# 10+ features when appropriate (e.g., global usings, file-scoped namespaces).
- Enable and respect nullable reference types (`<Nullable>enable</Nullable>`) to prevent null-reference exceptions.
- Promote immutability for data transfer objects (DTOs) by preferring `record` types or classes with `init`-only properties.
- Prefer language keywords for built-in types (e.g., `int`, `string` over `Int32`, `String`).
- Leverage built-in ASP.NET Core features and middleware.
- Use Entity Framework Core effectively for database operations.

## Syntax and Formatting  
- Follow Microsoft C# Coding Conventions.  
- Use C#’s expressive syntax (null-conditional operators, string interpolation).  
- Use var for local variable declarations.  
- Always use braces for conditional and loop statements.  
- Avoid qualifying members with this. unless required for disambiguation.  
- Indent with 4 spaces; UTF-8 encoding; insert final newline.  
- Target a maximum line length of 150 characters.
- Treat warnings as errors; resolve warnings instead of suppressing them.  

## Architecture (Onion)
- Core is the center:
  - Core contains domain models, business rules, and interfaces (e.g., Abstractions, Repositories).
  - Core must not reference Presentation, Infrastructure, EF Core, Umbraco, or external integration SDKs.
- Shared is cross-cutting:
  - Shared contains framework-agnostic helpers (e.g., logging, caching, primitives).
  - Core may depend on Shared.
  - Shared must not depend on Presentation or Infrastructure.
- Infrastructure / Integrations:
  - Implement Core interfaces (persistence, search, external APIs).
  - May depend on Core and Shared.
  - Must not depend on Presentation (keep it swappable).
- Presentation (Web/UI):
  - Acts as the composition root.
  - Wires dependencies by calling layer registration methods (e.g., `AddCore`, `AddShared`, `AddPersistence`, `AddIntegration`, `AddSearch`, etc.).
- Allowed exception:
  - Cross-project references are only allowed when required for Umbraco Commerce integration; keep the exception isolated to the Commerce/integration module.
  
## Startup and Composition Root (Umbraco)
- Treat `Program.cs` as read-only unless absolutely necessary (framework upgrades may overwrite it).
- Prefer Umbraco `IComposer` / `IComponent` and extension methods for registration and startup logic.

## Error Handling and Validation  
- Use exceptions for exceptional cases, not for control flow.  
- Implement proper error logging using built-in .NET logging or a third-party logger.  
- Prefer **FluentValidation** for input/view-model validation (Presentation layer).  
- Use **Data Annotations** primarily for persistence entities/schema constraints (e.g., EF Core).  
- Implement global exception handling middleware.  
- Return appropriate HTTP status codes and consistent error responses.  

## API Design  
- Follow RESTful API design principles.  
- Use attribute routing in controllers.  
- Implement versioning for your API.  
- Use action filters for cross-cutting concerns.  

## Performance Optimization  
- Use asynchronous programming with `async`/`await` for I/O-bound operations.
- Avoid `async void`; prefer `async Task` to ensure exceptions are handled correctly.
- Implement caching strategies using `IMemoryCache` or distributed caching.
- Use efficient LINQ queries and avoid N+1 query problems.
- Implement pagination for large data sets.

## Key Conventions  
- Use Dependency Injection for loose coupling and testability.
- Be deliberate when choosing service lifetimes (Singleton, Scoped, Transient) and avoid capturing scoped services in singletons.
- Implement the repository pattern or use Entity Framework Core directly, depending on the complexity.
- Use AutoMapper for object-to-object mapping if needed.
- Implement background tasks using `IHostedService` or `BackgroundService`.

## Testing  
- Write unit tests using **xUnit**.
- Use **NSubstitute** for mocking dependencies
- Implement integration tests for API endpoints.  

## Security  
- Use Authentication and Authorization middleware.  
- Implement JWT authentication for stateless API authentication.  
- Use HTTPS and enforce SSL.  
- Implement proper CORS policies.  

## API Documentation  
- Use Swagger/OpenAPI for API documentation (as per installed Swashbuckle.AspNetCore package).  
- Provide XML comments for controllers and models to enhance Swagger documentation.  
  
Follow the official Microsoft documentation and ASP.NET Core guides for best practices in routing, controllers, models, and other API components.