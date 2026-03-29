# Langoose Architecture Guidance

## Recommended Principle

Use onion architecture as the mental model:

- core business concepts at the center
- infrastructure and delivery on the outside
- dependencies point inward

Implement it lightly. Do not import a full enterprise clean-architecture template unless the repo truly needs that complexity.

## Recommended Shapes

### `API + Data`

Use when:

- persistence is growing
- EF Core needs a clearer home
- the domain is still small enough that a separate core project would mostly add ceremony

### `API + Domain + Data`

Use when:

- core models need to be shared cleanly between API behavior and persistence
- you want a durable home for business concepts outside ASP.NET Core and EF Core
- you expect ongoing growth in both business logic and persistence structure

## Anti-Goals

- Do not add repository-per-entity by default.
- Do not add mediator/CQRS by default.
- Do not split application logic into many thin pass-through layers without a strong reason.
- Do not make the code harder to trace than the product complexity requires.

## Review Checklist

- Is the dependency direction cleaner than before?
- Did the new project boundary remove a real ownership problem?
- Are business rules still easy to find?
- Is the refactor proportional to the repo's current size and expected growth?



