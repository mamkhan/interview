# Builders and Personas — `Tests.Shared`

Every test reaches for builders, not raw constructors or anonymous object hashes. Builder + Persona infrastructure is one of the highest-leverage pieces of test infrastructure: every test it touches gets shorter and more readable.

## Plain fluent builder

```
new OrderBuilder()
    .WithCustomer(customer)
    .WithItem(sku: "ABC-123", quantity: 2)
    .Build()
```

A builder defaults to a valid object with sensible values; tests override only the fields they care about. Reading the test, you see what's *different* about this scenario — not the noise of constructing a valid order.

## Higher-order builder

Composes nested builders via lambdas/closures, so test setup mirrors the structure of the data:

```
new OrderBuilder()
    .WithLines(line => line
        .ForSku("ABC-123")
        .Quantity(2)
        .Discount(percent: 10))
    .Build()
```

Use this when the inner thing has its own meaningful builder; otherwise stick with the plain fluent style.

## Disciplines

- **Builders live in `Tests.Shared`** so every test module of the bounded context reuses them. No duplicates.
- **Builders default to a valid object** with sensible values. Tests override only the fields they care about.
- **Builders return real domain objects**, not DTOs — they exercise the same constructors production uses. A bug in the constructor surfaces in test setup.
- **One builder per aggregate**, written before the *second* test for that aggregate. The first test can use raw construction; the second proves the builder is needed.
- **Negative cases get named methods**, not magic values. Prefer `WithNoItems()` over `WithItems([])`; prefer `WithExpiredCoupon()` over `WithCouponExpiringAt(yesterday)`.

## Personas

A **Persona** is a specialised builder for auth context: a class encoding a realistic user with claims, group memberships, role assignments. Auth-context builders accept a persona and produce a context indistinguishable from a real request.

```
new AuthContextBuilder()
    .ForPersona(Personas.Admin)
    .Build()

new AuthContextBuilder()
    .ForPersona(Personas.CustomerWithExpiredSubscription)
    .Build()
```

This is what lets controller / handler tests assert authorisation behaviour without booting the web framework. Without it, every auth test reinvents the same fixture and they drift apart.

## Replication takeaway

Build the Builder + Persona infrastructure **before** the second test in a new bounded context. The cost is small; the leverage is enormous. If you find a test using raw constructors and positional arguments to assemble its data, that's the signal — extract a builder before adding the next test.
