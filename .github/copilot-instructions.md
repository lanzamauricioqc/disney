## Coding principles

These rules primarily apply to domain and application business logic.
Data transfer objects, configuration models, persistence mappings, migrations,
and tests may deviate when required by their frameworks.

1. Keep methods focused and minimize nesting.
   - Each method should perform one clearly defined responsibility.
   - Prefer one level of indentation when practical.
2. Prefer guard clauses and early returns over `else` when they improve clarity.
   - Use polymorphism for complex conditional behavior when appropriate.
   - Retain `else` when the branches are genuinely symmetrical and clearer together.
3. Introduce value objects for domain concepts that require validation or behavior.
   - Avoid primitive obsession.
   - Do not wrap primitives mechanically when they carry no domain meaning.
4. Encapsulate domain collections when they enforce invariants or behavior.
   - Data transfer objects and persistence models may expose collections when required.
5. Follow the Law of Demeter and communicate only with immediate collaborators.
   - Avoid train-wreck call chains that navigate through unrelated objects.
   - Cohesive Language Integrated Query expressions and framework fluent interfaces are allowed.
6. Don't abbreviate.
   - Established technical acronyms such as API, SQL, HTTP, JSON, URL, DTO, and ID are allowed.
   - Do not shorten ordinary names such as `configuration`, `request`, `response`,
     `exception`, `repository`, or `service`.
7. Keep classes cohesive and focused on one responsibility.
   - Refactor classes when their size indicates multiple responsibilities.
   - Do not enforce arbitrary line-count limits.
8. Minimize instance variables while preserving cohesion and readability.
   - Do not enforce an arbitrary maximum number of fields.
9. Favor behavior-rich domain objects over anemic models.
   - Prefer immutable properties, records, and private setters where appropriate.
   - Properties are allowed for data transfer objects, configuration, serialization,
     and persistence models.