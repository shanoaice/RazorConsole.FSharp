# 测试集现状分析

工作快照，供换机续作时参考。

- 分析日期：2026-10-28
- 基线 commit：`5f2ac93`
- 配套文档：`docs/testing_failure_mode_checklist.md`（失败模式清单，ASD-STE100 英语）

## 1. 状态快照

- 测试总数 22：15 绿，7 红。
- 7 红全部位于 `test/ViewCombinatorTests.fs`。根因是 `src/RazorConsole.FSharp/View.fs` 的组合子是 TDD stub（`notImplemented` 抛异常）。这是设计好的练习红。
- `View.empty` 已实现。它是 View 测试里唯一绿的一个。
- 运行方式：`dotnet run --project test`（xUnit v3，Microsoft.Testing.Platform 宿主）。
- 已知问题：`dotnet test` 报 "Handshake failures" 并跑零个测试。MTP 与 `dotnet test` 桥接故障，待修。它拖慢红绿反馈，对 TDD 工作流有实际伤害。

## 2. 测试理念盘点

结论：骨架理念正确——内存渲染树、测试快而确定、测试名即规格。主要问题是单次渲染偏差和断言过浅。

### 2.1 优点，值得保持

- `test/TestRenderer.fs` 内存渲染架构。毫秒级、无终端、确定性。测试跑得便宜，人才愿意写测试。
- Unquote 的 `test <@ @>` 断言可读。失败信息就是 F# 表达式。
- 负空间测试齐：`SetDispatch` 两次抛 `InvalidOperationException`、`InitializeMvu` 传 null 抛 `ArgumentNullException`、初始化后再调用抛异常。
- 时序类有覆盖：`Concurrent dispatches from multiple background threads are synchronized`。
- 测试名是完整行为句，可当规格读。例如 `ShouldRender suppression prevents view re-evaluation and re-rendering`。
- `View.fs` stub + `docs/reference/View.fs` 的红绿设计。红先行是字面强制的，不需要自律。

### 2.2 理念隐患

测试从参考实现反推（commit `d81cd04`），属于特征化测试。参考实现的行为会被固化成规格，包括它的规格决策。这些决策需要人批准，不能默认继承。典型例子见第 5 节。

## 3. 缺口清单，按价值排序

- [ ] **1. 重渲染测试（最高价值）。** 所有 View 测试只渲染一次。seq 线程化 bug 只在 diff 时显形，第一次渲染看不出问题。补三类：`rows [a;b]` 更新为 `rows [a;b;c]`（加子节点）、更新为 `rows [b;a]`（移子节点）、`forEach` 增删项。断言更新后的完整帧序列。参考 `docs/reference/View.fs` 的 seq 线程化实现。
- [ ] **2. 补浅断言。** 三处测试名过度承诺：
  - `View.rows emits div with rows class and multiple children`（`test/ViewCombinatorTests.fs:96`）只断言头两帧，不断言 children。空实现也能绿。
  - `View.panel emits div with panel class and header`（`test/ViewCombinatorTests.fs:63`）不断言子内容。
  - `View.forEach isolates items inside a Region`（`test/ViewCombinatorTests.fs:144`）只断言 frame[0] 是 Region，不断言三个条目真的渲染。
  测试名撒谎比无测试更危险。人靠名字审查测试，名字失真时审查失效。
- [ ] **3. 零例与边界。** `rows []`、`forEach []`、`text ""`、null 字符串、`panel` 空 border（参考实现有 `String.IsNullOrEmpty` 分支，无测试）、`textInput` 空 placeholder。
- [ ] **4. 未覆盖 API。** `element` 与 `attr`（`panel`/`rows` 的底层原语）、`textInput`（`ChangeEventArgs.Value` 可为 null，最藏边缘情况）、`buttonAction`、`fragment`、`component'`、`simplePanel` 全部无测试。
- [ ] **5. 沉默错误契约。** `src/RazorConsole.FSharp/MvuComponentBase.fs` 的 `UpdateState` 在 `SetInitialState` 之前被调用时走 `| _ ->` 分支，静默丢弃渲染。先决策契约（抛异常还是允许），再写测试钉住。
- [ ] **6. 生命周期。** renderer 销毁后 dispatch 的行为无测试。
- [ ] **7. 反直觉契约钉住。** 默认 `ShouldRender` 是引用不等。值等但新建引用的 record 会触发重渲染。值得一个显式测试把这个行为写成契约。

## 4. 测试风格改进

- 删掉 `if count > 0 then ... else RenderTreeFrameType.None` 防御模式。它把"什么都没渲染"变成一个奇怪的断言失败。
- 提取测试助手：`firstFrame`、`attrsOf`、`textContentAt`。放 `TestRenderer.fs` 或测试模块顶部。目标是单个测试 3 到 5 行。
- 测试仪式感越低，人手写测试的意愿越高。这条直接服务于"人写测试、AI 写实现"的工作流。

## 5. 待决策事项，规格决定权在人

- `UpdateState` 先于 `SetInitialState` 的行为（见缺口 5）。
- `panel` 空 border 时省略 `data-border`，是否定为正式契约。
- `textInput` 对 null `ChangeEventArgs.Value` 的转换。参考实现选了空串。
- `View.text ""` 与 `View.text null` 是否发帧，发什么样的帧。

## 6. 建议续作顺序

1. 修 `dotnet test` 握手问题，或统一改用 `dotnet run --project test`。
2. 实现 `View.fs` 让 7 红转绿。这是现成的红绿循环练习。
3. 做缺口 1、2（重渲染 + 补断言）。它们覆盖核心不变量。
4. 做缺口 3、4（零例 + 未覆盖 API）。拿失败模式清单逐条过。
5. 每个新测试做一次杀死检查：故意破坏实现，确认测试会红，再恢复。
