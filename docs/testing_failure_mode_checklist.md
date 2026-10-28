# Test Failure-Mode Checklist

Use this checklist to find test cases.
Use it when you write tests yourself.
Also use it when you review tests that an AI writes.
The checklist has two parts.
Part 1 lists generic failure modes.
Part 2 lists failure modes for the render tree DSL in this repo.

## How to use this checklist

1. Name the target. The target is the function or component that you test.
2. Read Part 1. Ask how the target can fail in a generic way.
3. Read Part 2. Ask how the target can fail in this repo.
4. Write one test for each failure mode that applies.
5. Give each failure mode a status. Use `Covered`, `Not applicable`, or `Open`.
6. Do the work for each mode with status `Open`.

Never skip the empty case.
Never skip the error case.

## Part 1: Generic failure modes

### 1.1 Count boundaries

- Test the case with zero items.
- Test the case with one item.
- Test the case with many items.

### 1.2 Value boundaries

- Test null input.
- Test empty strings.
- Test strings with only whitespace.
- Test values at the type limits. Example: `Int32.MinValue` and `Int32.MaxValue`.
- Test very large inputs. Example: a list with 10 000 items.

### 1.3 Order and time

- Test the call before initialization.
- Test the call after initialization.
- Test the call after dispose.
- Test calls from many threads at the same time.
- Test the same call two times. Decide the contract for the second call first.

### 1.4 State transitions

- Test every state with every message.
- Test each illegal transition. It must throw the documented exception.

### 1.5 Wrong use

- Test null arguments. They must throw `ArgumentNullException`.
- Test double configuration. It must throw `InvalidOperationException`.
- Decide the contract before you write the test.

### 1.6 No-change cases

- Test that nothing changes when the input does not change.
- Test that no callback runs when no event happens.

### 1.7 Composition

- Test the identity element. Example: an empty node emits no frames.
- Test that a container keeps the order of its children.
- Test that two siblings share no state.

### 1.8 Second run

- Test the second run with changed input.
- Many bugs appear only on the second run. Do not test only the first render.

## Part 2: Render tree failure modes

The view DSL in this repo builds Blazor render trees.
A `Node` and an `Attr` are delegates.
They write frames into a `RenderTreeBuilder`.
They also thread a sequence number through the tree.
Read `docs/research/view_combinator_testing_guide.md` for the background.

### 2.1 Frame balance

- Each `OpenElement` needs one `CloseElement`.
- Each `OpenComponent` needs one `CloseComponent`.
- Each `OpenRegion` needs one `CloseRegion`.
- Assert the full frame list. Do not assert only the first frame.

### 2.2 Sequence numbers

- Sibling frames must use distinct sequence numbers.
- A `Node` must return the first free sequence number after its frames.
- Test a re-render that adds one child.
- Test a re-render that removes one child.
- Test a re-render that moves one child.
- Wrong sequence numbers corrupt the diff. The first render still looks correct.

### 2.3 Attribute order

- Attributes must come before content inside an element.
- Test one element that has attributes and children.

### 2.4 Region isolation

- Every dynamic collection must sit in an `OpenRegion`.
- Test a collection with 0, 1, and 3 items.
- Test an update that changes the item count.

### 2.5 Event callbacks

- Test that the callback runs after the event.
- Test the callback input. Example: `ChangeEventArgs.Value` can be null.
- Test that the callback reads fresh state. Old captures break after updates.

### 2.6 Component state

- Test `UpdateState` before `SetInitialState`. Decide its contract first.
- Test dispatch after dispose.
- Test dispatch from many threads at the same time.
- Test the default `ShouldRender` with a value-equal model. It re-renders on a new reference.

### 2.7 Values at framework boundaries

- Test boxed `obj` values. Example: an `EventCallback` in an attribute.
- Test string conversion of `obj` values. Decide the contract for null first.

## After you write tests

Do a kill check for each new behavior.

1. Break the code on purpose.
2. Run the tests. At least one test must fail.
3. Restore the code.

Use property tests when a list is not enough.

- Generate random trees with FsCheck.
- Assert invariants. Example: output sequence numbers strictly increase.
- Property tests cover cases that you do not list.

## Test style rules

- One behavior per test.
- The test name states the behavior and the result. Read the name as a spec sentence.
- Keep each test short. Invest in test helpers instead of copy-paste plumbing.

## Tracking table template

| Target | Failure mode | Status | Test name or reason |
| --- | --- | --- | --- |

Status values: `Covered`, `Not applicable`, `Open`.
