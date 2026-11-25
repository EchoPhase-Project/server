# Tests

This file tells how to write and run the tests.

The tests use xUnit v3 on Microsoft Testing Platform. Each source project has
one test project.

```sh
mise run test                                   # all tests
mise run test -- --filter-class '*Argon2*'      # some tests
mise run test:coverage                          # artifacts/coverage
```

Name test classes `<Subject>Tests`. Name test methods as a sentence:
`Member_Condition_Outcome` (`Failure_SkipsTheStepsAndKeepsItsError`). Test the
behaviour that a caller sees, including the failures.

The expression engine has allocation budgets (`AllocationBudgetTests`). If a
change makes more allocations, these tests fail.
