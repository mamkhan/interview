# Refactor Candidates

After TDD cycle, look for:

- **Duplication** → Extract function/class
- **Long methods** → Break into private helpers (keep tests on public interface)
- **Shallow modules** → Combine or deepen
- **Feature envy** → Move logic to where data lives
- **Primitive obsession** → Introduce value objects
- **Leaked I/O in the domain** → Push it out behind a repository or clock interface (see [architecture.md](architecture.md)); if a test wanted to stub a domain object, that's the signal
- **Existing code** the new code reveals as problematic
